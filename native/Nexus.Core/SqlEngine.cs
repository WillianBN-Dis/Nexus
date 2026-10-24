using System.Data;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Nexus.Core;
public sealed partial class NexusStore
{
    private SqliteConnection Database(string id)
    {
        var item=Get(id);
        if(!File.Exists(item.Path))throw new IOException("Banco indisponível. Conecte o disco onde ele está salvo.");
        var c=Open(item.Path);
        using var cmd=Command(c,"PRAGMA foreign_keys=ON");cmd.ExecuteNonQuery();
        var started=Stopwatch.StartNew();
        raw.sqlite3_progress_handler(c.Handle,1000,_=>started.Elapsed>TimeSpan.FromSeconds(5)?1:0,null);
        return c;
    }
    public static string Quote(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Contains('\0')||name.Length>128)throw new ArgumentException("Informe um nome de 1 a 128 caracteres.");
        return "\""+name.Replace("\"","\"\"")+"\"";
    }
    private static void Check(int code,SqliteConnection c)
    {
        if(code==raw.SQLITE_INTERRUPT)throw new TimeoutException("Consulta interrompida. O limite de execução é de 5 segundos.");
        if(code!=raw.SQLITE_OK)throw new InvalidOperationException(raw.sqlite3_errmsg(c.Handle).utf8_to_string());
    }
    private static void Secure(SqliteConnection c,CancellationToken token)
    {
        var started=Stopwatch.StartNew();
        raw.sqlite3_progress_handler(c.Handle,1000,_=>token.IsCancellationRequested||started.Elapsed>TimeSpan.FromSeconds(5)?1:0,null);
        raw.sqlite3_set_authorizer(c.Handle,(strdelegate_authorizer)((_,action,a,b,db,trigger)=>
            action is raw.SQLITE_ATTACH or raw.SQLITE_DETACH or raw.SQLITE_PRAGMA or raw.SQLITE_TRANSACTION or raw.SQLITE_SAVEPOINT
            ||(action==raw.SQLITE_FUNCTION&&string.Equals(b,"load_extension",StringComparison.OrdinalIgnoreCase))?raw.SQLITE_DENY:raw.SQLITE_OK),null);
    }
    private static sqlite3_stmt Prepare(SqliteConnection c,string sql)
    {
        if(string.IsNullOrWhiteSpace(sql))throw new ArgumentException("Digite uma instrução SQL.");
        var code=raw.sqlite3_prepare_v2(c.Handle,sql,out var statement,out string tail);
        if(code!=raw.SQLITE_OK){statement?.Dispose();Check(code,c);}
        if(statement is null||statement.IsInvalid){statement?.Dispose();throw new ArgumentException("A consulta não contém uma instrução SQL.");}
        try
        {
            if(!string.IsNullOrWhiteSpace(tail))
            {
                var nextCode=raw.sqlite3_prepare_v2(c.Handle,tail,out var extra);
                using(extra)
                {
                    Check(nextCode,c);
                    if(extra is not null&&!extra.IsInvalid)throw new ArgumentException("Execute uma instrução SQL por vez.");
                }
            }
            return statement;
        }
        catch{statement.Dispose();throw;}
    }
    public bool IsReadOnly(string id,string sql)
    {
        using var c=Database(id);Secure(c,CancellationToken.None);using var statement=Prepare(c,sql);
        return raw.sqlite3_stmt_readonly(statement)!=0;
    }
    public QueryResult Execute(string id,string sql,CancellationToken token=default)
    {
        using var c=Database(id);
        using var transaction=c.BeginTransaction(deferred:true);
        Secure(c,token);
        var result=new DataTable();bool more=false,readOnly;int affected=0;
        try
        {
            using(var statement=Prepare(c,sql))
            {
                readOnly=raw.sqlite3_stmt_readonly(statement)!=0;
                var fields=raw.sqlite3_column_count(statement);
                for(int n=0;n<fields;n++)
                {
                    var name=raw.sqlite3_column_name(statement,n).utf8_to_string();if(string.IsNullOrEmpty(name))name="Coluna"+(n+1);
                    while(result.Columns.Contains(name))name+="_";
                    result.Columns.Add(name,typeof(object));
                }
                int code;
                while((code=raw.sqlite3_step(statement))==raw.SQLITE_ROW)
                {
                    if(result.Rows.Count==1000){more=true;break;}
                    var row=new object[fields];
                    for(int n=0;n<fields;n++)row[n]=raw.sqlite3_column_type(statement,n) switch
                    {
                        raw.SQLITE_INTEGER=>raw.sqlite3_column_int64(statement,n),
                        raw.SQLITE_FLOAT=>raw.sqlite3_column_double(statement,n),
                        raw.SQLITE_BLOB=>raw.sqlite3_column_blob(statement,n).ToArray(),
                        raw.SQLITE_NULL=>DBNull.Value,
                        _=>raw.sqlite3_column_text(statement,n).utf8_to_string()
                    };
                    result.Rows.Add(row);
                }
                if(!more&&code!=raw.SQLITE_DONE)Check(code,c);
                affected=readOnly?0:raw.sqlite3_changes(c.Handle);
            }
            raw.sqlite3_set_authorizer(c.Handle,(strdelegate_authorizer)null!,null);
            transaction.Commit();
            return new(result,more,readOnly,affected);
        }
        finally
        {
            raw.sqlite3_set_authorizer(c.Handle,(strdelegate_authorizer)null!,null);
            raw.sqlite3_progress_handler(c.Handle,0,null!,null);
        }
    }
}
