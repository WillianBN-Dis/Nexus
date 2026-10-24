using System.Data;
using Microsoft.Data.Sqlite;
namespace Nexus.Core;
public sealed record ColumnInfo(string Name,string Type,bool Required,object? Default,int Key,int Hidden);
public sealed record ColumnSpec(string Name,string Type,bool Required=false);
public sealed record TableInfo(IReadOnlyList<ColumnInfo> Columns,IReadOnlyList<string> Identity);
public sealed record RowSnapshot(object?[] Key,object?[] Values);
public sealed record TablePage(TableInfo Info,DataTable Table,IReadOnlyList<RowSnapshot> Rows,bool HasMore);
public sealed class RecordConflictException(string message):ArgumentException(message);

public sealed partial class NexusStore
{
    private static readonly string[] Types=["TEXT","INTEGER","REAL","BLOB"];
    private static object? Value(object value)=>value is DBNull?null:value;
    private static TableInfo Schema(SqliteConnection c,string table)
    {
        using(var cmd=Command(c,"SELECT 1 FROM sqlite_schema WHERE type='table' AND name=$p0 AND substr(name,1,7)!='sqlite_'",table))
            if(cmd.ExecuteScalar() is null)throw new ArgumentException("Tabela não encontrada. Atualize a lista.");
        var columns=new List<ColumnInfo>();
        using(var cmd=Command(c,$"PRAGMA table_xinfo({Quote(table)})"))
        using(var r=cmd.ExecuteReader())while(r.Read())
            if(r.GetInt32(6)!=1)columns.Add(new(r.GetString(1),r.GetString(2),r.GetInt32(3)!=0,Value(r[4]),r.GetInt32(5),r.GetInt32(6)));
        var identity=new List<string>();
        foreach(var alias in new[]{"rowid","_rowid_","oid"})
        {
            if(columns.Any(col=>string.Equals(col.Name,alias,StringComparison.OrdinalIgnoreCase)))continue;
            try{using var cmd=Command(c,$"SELECT {alias} FROM {Quote(table)} LIMIT 0");using var reader=cmd.ExecuteReader();identity.Add(alias);break;}
            catch(SqliteException){break;}
        }
        if(identity.Count==0)identity.AddRange(columns.Where(col=>col.Key>0).OrderBy(col=>col.Key).Select(col=>col.Name));
        return new(columns,identity);
    }
    public TableInfo TableInfo(string id,string table){using var c=Database(id);return Schema(c,table);}
    public void CreateTable(string id,string table,IReadOnlyList<ColumnSpec> columns)
    {
        table=table.Trim();var definitions=new List<string>{"\"id\" INTEGER PRIMARY KEY"};var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"id"};
        if(table.StartsWith("sqlite_",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("O prefixo sqlite_ é reservado.");
        if(columns.Count==0)throw new ArgumentException("Adicione pelo menos uma coluna além do ID automático.");
        foreach(var column in columns)
        {
            var name=column.Name.Trim();if(!names.Add(name))throw new ArgumentException("Nome de coluna repetido: "+name);
            if(!Types.Contains(column.Type))throw new ArgumentException("Tipo de coluna inválido.");
            definitions.Add(Quote(name)+" "+column.Type+(column.Required?" NOT NULL":""));
        }
        using var c=Database(id);using var cmd=Command(c,$"CREATE TABLE {Quote(table)} ({string.Join(", ",definitions)})");cmd.ExecuteNonQuery();
    }
    public void AddColumn(string id,string table,string name,string type)
    {
        if(!Types.Contains(type))throw new ArgumentException("Tipo inválido.");
        using var c=Database(id);Schema(c,table);using var cmd=Command(c,$"ALTER TABLE {Quote(table)} ADD COLUMN {Quote(name.Trim())} {type}");cmd.ExecuteNonQuery();
    }
    public void DropTable(string id,string table){using var c=Database(id);Schema(c,table);using var cmd=Command(c,$"DROP TABLE {Quote(table)}");cmd.ExecuteNonQuery();}
    public TablePage BrowsePage(string id,string table,string? sort=null,bool descending=false,int offset=0,string? filterColumn=null,string? filterValue=null)
    {
        using var c=Database(id);var info=Schema(c,table);var names=info.Columns.Select(col=>col.Name).ToList();
        if(sort is not null&&!names.Contains(sort))throw new ArgumentException("Coluna de ordenação não encontrada.");
        if(filterColumn is not null&&!names.Contains(filterColumn))throw new ArgumentException("Coluna do filtro não encontrada.");
        var filter=filterColumn is null?"":$" WHERE {Quote(filterColumn)} IS $p1";
        var order=sort is null?"":$" ORDER BY {Quote(sort)} "+(descending?"DESC":"ASC");
        var fields=info.Identity.Concat(names).Select(Quote);
        using var cmd=filterColumn is null?Command(c,$"SELECT {string.Join(", ",fields)} FROM {Quote(table)}{order} LIMIT 201 OFFSET $p0",Math.Max(0,offset))
            :Command(c,$"SELECT {string.Join(", ",fields)} FROM {Quote(table)}{filter}{order} LIMIT 201 OFFSET $p0",Math.Max(0,offset),filterValue);
        using var reader=cmd.ExecuteReader();var result=new DataTable();
        foreach(var name in names)result.Columns.Add(name,typeof(object));
        var records=new List<RowSnapshot>();bool more=false;
        while(reader.Read())
        {
            if(records.Count==200){more=true;break;}
            var keys=Enumerable.Range(0,info.Identity.Count).Select(n=>Value(reader[n])).ToArray();
            var values=Enumerable.Range(0,names.Count).Select(n=>Value(reader[n+info.Identity.Count])).ToArray();
            records.Add(new(keys,values));result.Rows.Add(values.Select(v=>v??DBNull.Value).ToArray());
        }
        return new(info,result,records,more);
    }
    public DataTable Browse(string id,string table)=>BrowsePage(id,table).Table;
    private static void ValidateValues(TableInfo info,IReadOnlyDictionary<string,object?> values)
    {
        if(values.Keys.Any(name=>!info.Columns.Any(col=>col.Name==name&&col.Hidden==0)))throw new ArgumentException("Coluna inexistente ou calculada. Atualize a tabela.");
    }
    public void InsertRecord(string id,string table,IReadOnlyDictionary<string,object?> values)
    {
        using var c=Database(id);ValidateValues(Schema(c,table),values);
        var sql=values.Count==0?$"INSERT INTO {Quote(table)} DEFAULT VALUES":$"INSERT INTO {Quote(table)} ({string.Join(", ",values.Keys.Select(Quote))}) VALUES ({string.Join(", ",Enumerable.Range(0,values.Count).Select(n=>"$p"+n))})";
        using var cmd=Command(c,sql,values.Values.ToArray());cmd.ExecuteNonQuery();
    }
    public void ChangeRecord(string id,string table,RowSnapshot original,IReadOnlyDictionary<string,object?>? values=null,bool delete=false)
    {
        using var c=Database(id);var info=Schema(c,table);using var tx=c.BeginTransaction();
        if(info.Identity.Count==0)throw new ArgumentException("Esta tabela não tem identificação segura para editar registros.");
        if(info.Identity.Count!=original.Key.Length||info.Columns.Count!=original.Values.Length)throw new ArgumentException("A estrutura mudou. Atualize a tabela.");
        values??=new Dictionary<string,object?>();ValidateValues(info,values);
        var args=delete?new List<object?>():values.Values.ToList();var where=new List<string>();
        foreach(var pair in info.Identity.Concat(info.Columns.Select(col=>col.Name)).Zip(original.Key.Concat(original.Values)))
        {where.Add(Quote(pair.First)+" IS $p"+args.Count);args.Add(pair.Second);}
        var assignments=string.Join(", ",values.Keys.Select((name,n)=>Quote(name)+"=$p"+n));
        if(!delete&&values.Count==0)return;
        var sql=delete?$"DELETE FROM {Quote(table)} WHERE {string.Join(" AND ",where)}":$"UPDATE {Quote(table)} SET {assignments} WHERE {string.Join(" AND ",where)}";
        using var cmd=Command(c,sql,args.ToArray());cmd.Transaction=tx;
        if(cmd.ExecuteNonQuery()!=1)throw new RecordConflictException("O registro foi alterado por outro processo. Atualize antes de tentar novamente.");
        tx.Commit();
    }
}
