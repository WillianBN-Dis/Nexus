using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Org.BouncyCastle.Crypto.Generators;

namespace Nexus.Core;
public sealed record DatabaseItem(string Id,string Name,string Path);
public sealed record QueryResult(DataTable Table,bool Truncated,bool ReadOnly=true,int Affected=0);

public sealed partial class NexusStore
{
    public string Root { get; }
    private readonly string catalog;
    private string? user;
    public NexusStore(string? root=null)
    {
        Root=root??System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nexus");
        Directory.CreateDirectory(Root);catalog=System.IO.Path.Combine(Root,"catalog.sqlite");
        using var c=Open(catalog,true);
        using var cmd=c.CreateCommand();
        cmd.CommandText="CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY,name TEXT UNIQUE,salt BLOB,hash BLOB); CREATE TABLE IF NOT EXISTS databases(id TEXT PRIMARY KEY,owner TEXT,name TEXT,path TEXT UNIQUE);";
        cmd.ExecuteNonQuery();
    }
    private static SqliteConnection Open(string path,bool create=false)
    {
        var c=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=path,Mode=create?SqliteOpenMode.ReadWriteCreate:SqliteOpenMode.ReadWrite,Pooling=false,DefaultTimeout=5}.ToString());
        c.Open();return c;
    }
    private static SqliteCommand Command(SqliteConnection c,string sql,params object?[] values)
    {
        var cmd=c.CreateCommand();cmd.CommandText=sql;
        for(int n=0;n<values.Length;n++)cmd.Parameters.AddWithValue("$p"+n,values[n]??DBNull.Value);
        return cmd;
    }
    private void Require(){if(user is null)throw new InvalidOperationException("Entre na sua conta para continuar.");}
    public void Logout()=>user=null;
    public void Register(string name,string password)
    {
        name=name.Trim();
        if(!Regex.IsMatch(name,@"^[\w.@-]{3,64}$"))throw new ArgumentException("Usuário: 3 a 64 letras, números ou . @ _ -");
        if(password.Length<10)throw new ArgumentException("Use pelo menos 10 caracteres na senha.");
        var salt=RandomNumberGenerator.GetBytes(32);
        var hash=SCrypt.Generate(Encoding.UTF8.GetBytes(password),salt,16384,8,1,64);
        using var c=Open(catalog);using var cmd=Command(c,"INSERT INTO users VALUES($p0,$p1,$p2,$p3)",Guid.NewGuid().ToString("N"),name,salt,hash);
        try{cmd.ExecuteNonQuery();}catch(SqliteException e)when(e.SqliteErrorCode==19){throw new ArgumentException("Esse usuário já existe.");}
    }
    public void Login(string name,string password)
    {
        user=null;using var c=Open(catalog);using var cmd=Command(c,"SELECT id,salt,hash FROM users WHERE name=$p0",name.Trim());
        using var reader=cmd.ExecuteReader();bool found=reader.Read();
        var salt=found?(byte[])reader[1]:new byte[32];
        var computed=SCrypt.Generate(Encoding.UTF8.GetBytes(password),salt,16384,8,1,64);
        if(!found||!CryptographicOperations.FixedTimeEquals(computed,(byte[])reader[2]))throw new ArgumentException("Usuário ou senha incorretos.");
        user=reader.GetString(0);
    }
    public List<DatabaseItem> List()
    {
        Require();using var c=Open(catalog);using var cmd=Command(c,"SELECT id,name,path FROM databases WHERE owner=$p0 ORDER BY name",user);
        using var reader=cmd.ExecuteReader();var items=new List<DatabaseItem>();
        while(reader.Read())items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2)));return items;
    }
    public DatabaseItem Get(string id)
    {
        Require();using var c=Open(catalog);using var cmd=Command(c,"SELECT id,name,path FROM databases WHERE id=$p0 AND owner=$p1",id,user);
        using var reader=cmd.ExecuteReader();if(!reader.Read())throw new ArgumentException("Banco não encontrado para este usuário.");
        return new(reader.GetString(0),reader.GetString(1),reader.GetString(2));
    }
    public DatabaseItem Create(string name)
    {
        Require();ValidateName(name);var id=Guid.NewGuid().ToString("N");
        var folder=System.IO.Path.Combine(Root,"databases",user!);Directory.CreateDirectory(folder);
        var path=System.IO.Path.Combine(folder,id+".sqlite");
        using(var db=Open(path,true)){using var pragma=Command(db,"PRAGMA user_version=1");pragma.ExecuteNonQuery();}
        try{using var c=Open(catalog);using var cmd=Command(c,"INSERT INTO databases VALUES($p0,$p1,$p2,$p3)",id,user,name.Trim(),path);cmd.ExecuteNonQuery();}
        catch{File.Delete(path);throw;}
        return Get(id);
    }
    public void Rename(string id,string name)
    {
        Get(id);ValidateName(name);using var c=Open(catalog);using var cmd=Command(c,"UPDATE databases SET name=$p0 WHERE id=$p1 AND owner=$p2",name.Trim(),id,user);cmd.ExecuteNonQuery();
    }
    private static void ValidateName(string name){if(string.IsNullOrWhiteSpace(name)||name.Length>100)throw new ArgumentException("Informe um nome com até 100 caracteres.");}
    public List<string> Tables(string id)
    {
        var item=Get(id);using var c=Open(item.Path);using var cmd=Command(c,"SELECT name FROM sqlite_schema WHERE type='table' AND substr(name,1,7)!='sqlite_' ORDER BY name");
        using var r=cmd.ExecuteReader();var names=new List<string>();while(r.Read())names.Add(r.GetString(0));return names;
    }
}
