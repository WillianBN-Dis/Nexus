using System.Security.Cryptography;
using System.Text;

namespace Nexus.Core;
public sealed record ApiKeyInfo(string Id,string Name,bool CanWrite,string[] DatabaseIds);
public sealed record ApiAccess(NexusStore Store,bool CanWrite,IReadOnlySet<string> DatabaseIds,string KeyId,string KeyName);
public sealed partial class NexusStore
{
    private NexusStore(string root,string owner)
    {Root=root;catalog=System.IO.Path.Combine(root,"catalog.sqlite");user=owner;}
    private static string NewToken()=>"nxs_"+Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static byte[] TokenHash(string token)=>SHA256.HashData(Encoding.UTF8.GetBytes(token));
    public List<ApiKeyInfo> ApiKeys()
    {
        Require();using var c=Open(catalog);
        using var cmd=Command(c,"SELECT id,name,can_write FROM api_keys WHERE owner=$p0 ORDER BY name",user);
        using var reader=cmd.ExecuteReader();var keys=new List<(string Id,string Name,bool Write)>();
        while(reader.Read())keys.Add((reader.GetString(0),reader.GetString(1),reader.GetInt32(2)!=0));reader.Close();
        return keys.Select(key=>
        {
            using var grants=Command(c,"SELECT database_id FROM api_grants WHERE key_id=$p0",key.Id);using var r=grants.ExecuteReader();var ids=new List<string>();
            while(r.Read())ids.Add(r.GetString(0));return new ApiKeyInfo(key.Id,key.Name,key.Write,ids.ToArray());
        }).ToList();
    }
    public string CreateApiKey(string name,bool canWrite,IEnumerable<string> databaseIds)
    {
        Require();ValidateName(name);var ids=databaseIds.Distinct().ToArray();
        if(ids.Length==0||ids.Length>100)throw new ArgumentException("Selecione de 1 a 100 bancos para compartilhar.");
        foreach(var id in ids)Get(id);
        var token=NewToken();var key=Guid.NewGuid().ToString("N");using var c=Open(catalog);using var tx=c.BeginTransaction();
        using(var cmd=Command(c,"INSERT INTO api_keys(id,owner,name,hash,can_write) VALUES($p0,$p1,$p2,$p3,$p4)",key,user,name.Trim(),TokenHash(token),canWrite?1:0)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        foreach(var id in ids){using var cmd=Command(c,"INSERT INTO api_grants VALUES($p0,$p1)",key,id);cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        tx.Commit();return token;
    }
    public string RotateApiKey(string id)
    {
        Require();var token=NewToken();using var c=Open(catalog);using var cmd=Command(c,"UPDATE api_keys SET hash=$p0 WHERE id=$p1 AND owner=$p2",TokenHash(token),id,user);
        if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("Chave não encontrada.");return token;
    }
    public void RevokeApiKey(string id)
    {
        Require();using var c=Open(catalog);using var tx=c.BeginTransaction();
        using(var cmd=Command(c,"DELETE FROM api_grants WHERE key_id IN (SELECT id FROM api_keys WHERE id=$p0 AND owner=$p1)",id,user)){cmd.Transaction=tx;cmd.ExecuteNonQuery();}
        using(var cmd=Command(c,"DELETE FROM api_keys WHERE id=$p0 AND owner=$p1",id,user)){cmd.Transaction=tx;if(cmd.ExecuteNonQuery()!=1)throw new ArgumentException("Chave não encontrada.");}
        tx.Commit();
    }
    public ApiAccess AuthenticateApiKey(string token)
    {
        if(token.Length!=68||!token.StartsWith("nxs_",StringComparison.Ordinal))throw new UnauthorizedAccessException();
        using var c=Open(catalog);using var cmd=Command(c,"SELECT id,owner,can_write,name FROM api_keys WHERE hash=$p0",TokenHash(token));
        using var reader=cmd.ExecuteReader();if(!reader.Read())throw new UnauthorizedAccessException();
        var id=reader.GetString(0);var owner=reader.GetString(1);var write=reader.GetInt32(2)!=0;var name=reader.GetString(3);reader.Close();
        using var grants=Command(c,"SELECT g.database_id FROM api_grants g JOIN databases d ON d.id=g.database_id WHERE g.key_id=$p0 AND d.owner=$p1",id,owner);
        using var r=grants.ExecuteReader();var ids=new HashSet<string>();while(r.Read())ids.Add(r.GetString(0));
        return new(new NexusStore(Root,owner),write,ids,id,name);
    }
}
