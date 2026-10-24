using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Nexus.Api;
using Nexus.Core;

var root=Path.Combine(Path.GetTempPath(),"nexus-api-test-"+Guid.NewGuid().ToString("N"));int checks=0;
void Assert(bool condition,string label){if(!condition)throw new Exception("API: "+label);checks++;}
int Port(){var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();return port;}
async Task<JsonElement> Json(HttpResponseMessage response,HttpStatusCode expected)
{Assert(response.StatusCode==expected,"HTTP "+response.StatusCode+" esperado "+expected+" "+await response.Content.ReadAsStringAsync());return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();}
Directory.CreateDirectory(root);LocalApiHost? api=null;
try
{
    var store=new NexusStore(root);store.Register("owner","temporary12345");store.Login("owner","temporary12345");
    var shared=store.Create("Compartilhado");var secret=store.Create("Não compartilhar");
    store.CreateTable(shared.Id,"pessoas",[new("nome","TEXT",true),new("dados","BLOB")]);
    store.InsertRecord(shared.Id,"pessoas",new Dictionary<string,object?>{{"nome","Ana"},{"dados",new byte[]{1,2,3}}});
    var read=store.CreateApiKey("leitor",false,[shared.Id]);var readId=store.ApiKeys().Single().Id;
    var write=store.CreateApiKey("escritor",true,[shared.Id]);var writeId=store.ApiKeys().Single(k=>k.CanWrite).Id;
    store.Register("other","temporary12345");store.Login("other","temporary12345");
    Assert(store.ApiKeys().Count==0,"chaves vazaram entre contas");
    bool denied=false;try{store.CreateApiKey("indevida",true,[shared.Id]);}catch(ArgumentException){denied=true;}Assert(denied,"compartilhamento de banco alheio permitido");
    store.Login("owner","temporary12345");
    api=new LocalApiHost(root);var settings=new ApiSettings(true,"127.0.0.1",Port());await api.ConfigureAsync(settings);
    using var client=new HttpClient{BaseAddress=new Uri(api.Address),Timeout=TimeSpan.FromSeconds(15)};
    await Json(await client.GetAsync("/api/bancos"),HttpStatusCode.Unauthorized);
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",read);
    var banks=await Json(await client.GetAsync("/api/bancos"),HttpStatusCode.OK);
    Assert(banks.GetArrayLength()==1&&banks[0].GetProperty("id").GetString()==shared.Id&&!banks[0].TryGetProperty("path",out _),"escopo ou caminho de arquivo exposto");
    await Json(await client.GetAsync($"/api/bancos/{secret.Id}/tabelas"),HttpStatusCode.NotFound);
    var path=$"/api/bancos/{shared.Id}/tabelas/pessoas/registros";
    await Json(await client.PostAsJsonAsync(path,new{valores=new{nome="não autorizado"}}),HttpStatusCode.Forbidden);
    var page=await Json(await client.GetAsync(path+"?coluna=nome&valor=Ana&ordenarPor=nome"),HttpStatusCode.OK);
    Assert(page.GetProperty("registros").GetArrayLength()==1,"filtro parametrizado");
    await Json(await client.GetAsync(path+"?coluna=inexistente&valor=x"),HttpStatusCode.BadRequest);
    await Json(await client.GetAsync(path+"?offset=-1"),HttpStatusCode.BadRequest);
    var injection=await Json(await client.GetAsync(path+"?coluna=nome&valor="+Uri.EscapeDataString("' OR 1=1 --")),HttpStatusCode.OK);
    Assert(injection.GetProperty("registros").GetArrayLength()==0,"SQL injection no filtro");
    store.Logout();await Json(await client.GetAsync(path),HttpStatusCode.OK);store.Login("owner","temporary12345");
    var rotated=store.RotateApiKey(readId);
    await Json(await client.GetAsync(path),HttpStatusCode.Unauthorized);
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",rotated);
    await Json(await client.GetAsync(path),HttpStatusCode.OK);
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",write);
    await Json(await client.PostAsJsonAsync(path,new{valores=new{nome="Bruno",dados="BAUG"}}),HttpStatusCode.Created);
    page=await Json(await client.GetAsync(path+"?coluna=nome&valor=Bruno"),HttpStatusCode.OK);
    var row=page.GetProperty("registros")[0];var original=row.GetProperty("valores");var key=row.GetProperty("chave");
    await Json(await client.PatchAsJsonAsync(path,new{chave=key,anteriores=original,valores=new{nome="Bruna"}}),HttpStatusCode.OK);
    await Json(await client.PatchAsJsonAsync(path,new{chave=key,anteriores=original,valores=new{nome="obsoleto"}}),HttpStatusCode.Conflict);
    page=await Json(await client.GetAsync(path+"?coluna=nome&valor=Bruna"),HttpStatusCode.OK);row=page.GetProperty("registros")[0];
    using var deletion=new HttpRequestMessage(HttpMethod.Delete,path){Content=JsonContent.Create(new{chave=row.GetProperty("chave"),anteriores=row.GetProperty("valores")})};
    await Json(await client.SendAsync(deletion),HttpStatusCode.OK);
    Assert(store.BrowsePage(shared.Id,"pessoas").Rows.Count==1,"exclusão removeu linha indevida");
    store.Execute(shared.Id,"CREATE TABLE pagina(id INTEGER PRIMARY KEY)");
    store.Execute(shared.Id,"WITH RECURSIVE s(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM s WHERE n<201) INSERT INTO pagina SELECT n FROM s");
    var pagination=await Json(await client.GetAsync($"/api/bancos/{shared.Id}/tabelas/pagina/registros"),HttpStatusCode.OK);
    Assert(pagination.GetProperty("registros").GetArrayLength()==200&&pagination.GetProperty("temMais").GetBoolean(),"paginação não limitada");
    pagination=await Json(await client.GetAsync($"/api/bancos/{shared.Id}/tabelas/pagina/registros?offset=200"),HttpStatusCode.OK);
    Assert(pagination.GetProperty("registros").GetArrayLength()==1&&!pagination.GetProperty("temMais").GetBoolean(),"segunda página");
    store.RevokeApiKey(writeId);await Json(await client.GetAsync(path),HttpStatusCode.Unauthorized);
    await api.DisposeAsync();api=new LocalApiHost(root);await api.RestoreAsync();
    client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",rotated);
    await Json(await client.GetAsync(path),HttpStatusCode.OK);
    using(var occupied=new TcpListener(IPAddress.Loopback,0))
    {
        occupied.Start();bool rejected=false;
        try{await api.ConfigureAsync(settings with{Port=((IPEndPoint)occupied.LocalEndpoint).Port});}catch(IOException){rejected=true;}
        Assert(rejected,"porta ocupada aceita");await Json(await client.GetAsync(path),HttpStatusCode.OK);
    }
    Assert(!LocalApiHost.IsLocal(IPAddress.Parse("8.8.8.8"))&&LocalApiHost.IsLocal(IPAddress.Parse("192.168.1.2")),"restrição de rede local");
    await api.ConfigureAsync(settings with{Enabled=false});Assert(!api.Running&&!new LocalApiHost(root).Settings.Enabled,"desativação não persistida");
    var logs=File.ReadAllText(api.Log.Path);
    Assert(logs.Contains("servidor_iniciado")&&logs.Contains("servidor_parado")&&logs.Contains("requisicao")&&logs.Contains("duracaoMs")&&logs.Contains("401"),"eventos/status ausentes do log");
    Assert(!logs.Contains(read)&&!logs.Contains(write)&&!logs.Contains(rotated)&&!logs.Contains("temporary12345")&&!logs.Contains("Bruna"),"credenciais ou dados de registros vazaram no log");
    api.Log.Export(Path.Combine(root,"export.jsonl"));Assert(File.Exists(Path.Combine(root,"export.jsonl")),"exportação de log");
    for(int n=0;n<550;n++)api.Log.Write("teste_rotacao",new{conteudo=new string('x',10_000)});
    Assert(File.Exists(api.Log.Path+".1")&&new FileInfo(api.Log.Path).Length<5*1024*1024,"rotação de log");
    Console.WriteLine($"PASS: {checks} verificações HTTP reais: autorização, escopo, escrita, BLOB, conflitos, renovação/revogação, reinício, filtro, logs sem segredos e rotação.");
}
finally{if(api is not null)await api.DisposeAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
