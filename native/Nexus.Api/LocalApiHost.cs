using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Nexus.Core;

namespace Nexus.Api;
public sealed record ApiSettings(bool Enabled=false,string Address="127.0.0.1",int Port=5080,bool StartWithWindows=false);
public sealed record WriteRequest(Dictionary<string,JsonElement>? Valores,JsonElement[]? Chave=null,JsonElement[]? Anteriores=null);
public sealed class LocalApiHost:IAsyncDisposable
{
    private readonly string root,settingsFile;
    private readonly SemaphoreSlim lifecycle=new(1,1);
    private WebApplication? host;
    public ApiSettings Settings{get;private set;}=new();
    public ApiLog Log{get;}
    public bool Running=>host is not null;
    public string Address=>$"http://{Settings.Address}:{Settings.Port}";
    public LocalApiHost(string root)
    {
        this.root=root;settingsFile=Path.Combine(root,"lan-api.json");Log=new ApiLog(root);
        if(File.Exists(settingsFile))try{Settings=JsonSerializer.Deserialize<ApiSettings>(File.ReadAllText(settingsFile))??new();}catch{Settings=new();}
    }
    public static bool IsLocal(IPAddress? address)
    {
        if(address is null)return false;if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        if(IPAddress.IsLoopback(address))return true;var bytes=address.GetAddressBytes();
        return bytes.Length==4&&(bytes[0]==10||bytes[0]==172&&bytes[1]>=16&&bytes[1]<=31||bytes[0]==192&&bytes[1]==168);
    }
    public static string[] Addresses()=>NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)
        .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).Where(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&IsLocal(a)&&!IPAddress.IsLoopback(a))
        .Select(a=>a.ToString()).Distinct().Append("127.0.0.1").ToArray();
    public Task RestoreAsync()=>ConfigureAsync(Settings);
    public async Task ConfigureAsync(ApiSettings next)
    {
        if(next.Port<1024||next.Port>65535||!IPAddress.TryParse(next.Address,out var ip)||!IsLocal(ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)throw new ArgumentException("Escolha um IPv4 local e uma porta de 1024 a 65535.");
        if(next.Enabled&&!Addresses().Contains(next.Address))throw new ArgumentException("O endereço selecionado não está disponível neste computador.");
        await lifecycle.WaitAsync();var previous=Settings;
        try
        {
            if(host is not null){await host.StopAsync();await host.DisposeAsync();host=null;Log.Write("servidor_parado",new{motivo="configuracao"});}
            try
            {
                if(next.Enabled)host=await Start(next);
                Directory.CreateDirectory(root);var temp=settingsFile+".tmp";await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(next));File.Move(temp,settingsFile,true);Settings=next;
            }
            catch(Exception error)
            {
                Log.Write("erro_configuracao",new{tipo=error.GetType().Name});
                if(host is not null){await host.StopAsync();await host.DisposeAsync();host=null;}
                if(previous.Enabled)try{host=await Start(previous);}catch{ }
                throw;
            }
        }
        finally{lifecycle.Release();}
    }
    public async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync();try{if(host is not null){await host.StopAsync();await host.DisposeAsync();host=null;Log.Write("servidor_parado",new{motivo="encerramento"});}}finally{lifecycle.Release();}
    }
    private async Task<WebApplication> Start(ApiSettings settings)
    {
        var store=new NexusStore(root);
        var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions{Args=[],ApplicationName=typeof(LocalApiHost).Assembly.FullName});
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k=>{k.Listen(IPAddress.Parse(settings.Address),settings.Port);k.Limits.MaxRequestBodySize=1024*1024;k.Limits.MaxConcurrentConnections=64;k.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);});
        builder.Services.AddRateLimiter(options=>
        {
            options.RejectionStatusCode=429;
            options.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
        var app=builder.Build();
        app.Use(async(context,next)=>
        {
            var clock=Stopwatch.StartNew();
            try{await next(context);}
            finally
            {
                var access=context.Items["access"] as ApiAccess;
                var db=context.Request.RouteValues["id"]?.ToString();
                Log.Write("requisicao",new{ip=context.Connection.RemoteIpAddress?.ToString(),metodo=context.Request.Method,rota=(context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText??"(nao mapeada)",banco=Guid.TryParseExact(db,"N",out _)?db:null,chave=access?.KeyName,chaveId=access?.KeyId,status=context.Response.StatusCode,duracaoMs=clock.ElapsedMilliseconds});
            }
        });
        app.UseRateLimiter();
        var requests=new SemaphoreSlim(4,4);
        app.Use(async(context,next)=>
        {
            if(!await requests.WaitAsync(0,context.RequestAborted)){context.Response.StatusCode=429;return;}
            try{await next(context);}finally{requests.Release();}
        });
        app.Use(async(context,next)=>
        {
            context.Response.Headers.CacheControl="no-store";
            if(!IsLocal(context.Connection.RemoteIpAddress)){context.Response.StatusCode=403;return;}
            try
            {
                var authorization=context.Request.Headers.Authorization.ToString();
                if(!authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException();
                var access=store.AuthenticateApiKey(authorization[7..]);context.Items["access"]=access;
                if(context.Request.Method is not ("GET" or "HEAD")&&!access.CanWrite){context.Response.StatusCode=403;await context.Response.WriteAsJsonAsync(new{erro="Esta chave permite somente leitura."});return;}
                await next(context);
            }
            catch(UnauthorizedAccessException){context.Response.StatusCode=401;await context.Response.WriteAsJsonAsync(new{erro="Chave de acesso inválida ou revogada."});}
            catch(KeyNotFoundException){context.Response.StatusCode=404;await context.Response.WriteAsJsonAsync(new{erro="Banco não compartilhado com esta chave."});}
            catch(RecordConflictException error){context.Response.StatusCode=409;await context.Response.WriteAsJsonAsync(new{erro=error.Message});}
            catch(ArgumentException error){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{erro=error.Message});}
            catch(JsonException){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{erro="JSON inválido."});}
            catch(BadHttpRequestException error){context.Response.StatusCode=error.StatusCode;await context.Response.WriteAsJsonAsync(new{erro="Solicitação inválida ou maior que o limite de 1 MB."});}
            catch(FormatException){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{erro="Valor base64 inválido."});}
            catch(SqliteException error){context.Response.StatusCode=error.SqliteErrorCode is 5 or 6?503:error.SqliteErrorCode==9?504:409;await context.Response.WriteAsJsonAsync(new{erro="Não foi possível concluir a operação. Atualize os registros e confira os valores."});}
            catch(IOException){context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new{erro="Banco ou disco indisponível."});}
            catch(Exception){context.Response.StatusCode=500;await context.Response.WriteAsJsonAsync(new{erro="Não foi possível concluir a solicitação."});}
        });
        app.MapGet("/api/bancos",(HttpContext c)=>Access(c).Store.List().Where(db=>Access(c).DatabaseIds.Contains(db.Id)).Select(db=>new{id=db.Id,nome=db.Name}));
        app.MapGet("/api/bancos/{id}/tabelas",(HttpContext c,string id)=>Scoped(c,id).Tables(id));
        app.MapGet("/api/bancos/{id}/tabelas/{table}/estrutura",(HttpContext c,string id,string table)=>Scoped(c,id).TableInfo(id,table));
        app.MapGet("/api/bancos/{id}/tabelas/{table}/registros",(HttpContext c,string id,string table)=>
        {
            int offset=0;if(c.Request.Query.TryGetValue("offset",out var offsetText)&&(!int.TryParse(offsetText,out offset)||offset<0||offset>1_000_000))throw new ArgumentException("Offset inválido (0 a 1000000).");
            string? Query(string name)=>c.Request.Query.TryGetValue(name,out var text)?text.ToString():null;
            var page=Scoped(c,id).BrowsePage(id,table,Query("ordenarPor"),Query("desc")=="true",offset,Query("coluna"),Query("valor"));
            return new{colunas=page.Info.Columns.Select(col=>col.Name),identidade=page.Info.Identity,offset,limite=200,temMais=page.HasMore,registros=page.Rows.Select(row=>new{chave=row.Key,valores=row.Values})};
        });
        app.MapMethods("/api/bancos/{id}/tabelas/{table}/registros",["POST","PATCH","DELETE"],async(HttpContext c,string id,string table)=>
        {
            if(!c.Request.HasJsonContentType())return Results.StatusCode(415);
            var scoped=Scoped(c,id);var body=await c.Request.ReadFromJsonAsync<WriteRequest>()??throw new ArgumentException("Informe o corpo JSON.");
            var info=scoped.TableInfo(id,table);
            var values=(body.Valores??[]).ToDictionary(pair=>pair.Key,pair=>Decode(pair.Value,info.Columns.FirstOrDefault(col=>col.Name==pair.Key)));
            if(c.Request.Method=="POST")
            {
                if(body.Valores is null)throw new ArgumentException("Informe valores.");scoped.InsertRecord(id,table,values);return Results.Json(new{sucesso=true},statusCode:201);
            }
            if(body.Chave is null||body.Anteriores is null||body.Chave.Length!=info.Identity.Count||body.Anteriores.Length!=info.Columns.Count)throw new ArgumentException("Envie chave e anteriores obtidos na última leitura.");
            if(c.Request.Method=="PATCH"&&(body.Valores is null||values.Count==0))throw new ArgumentException("Informe os valores que serão alterados.");
            var original=new RowSnapshot(body.Chave.Select((v,n)=>Decode(v,info.Columns.FirstOrDefault(col=>col.Name==info.Identity[n]))).ToArray(),body.Anteriores.Select((v,n)=>Decode(v,info.Columns[n])).ToArray());
            scoped.ChangeRecord(id,table,original,values,c.Request.Method=="DELETE");return Results.Ok(new{sucesso=true});
        });
        try{await app.StartAsync();Log.Write("servidor_iniciado",new{endereco=settings.Address,porta=settings.Port});return app;}catch{await app.DisposeAsync();throw;}
    }
    private static ApiAccess Access(HttpContext c)=>(ApiAccess)c.Items["access"]!;
    private static NexusStore Scoped(HttpContext c,string id)
    {var access=Access(c);if(!access.DatabaseIds.Contains(id))throw new KeyNotFoundException();return access.Store;}
    private static object? Decode(JsonElement value,ColumnInfo? column)=>value.ValueKind switch
    {
        JsonValueKind.Null=>null,
        JsonValueKind.String when column?.Type.Contains("BLOB",StringComparison.OrdinalIgnoreCase)==true=>Convert.FromBase64String(value.GetString()!),
        JsonValueKind.String=>value.GetString(),JsonValueKind.Number=>value.TryGetInt64(out var number)?(object)number:value.GetDouble(),
        _=>throw new ArgumentException("Use texto, número, NULL ou base64 para BLOB.")
    };
}
