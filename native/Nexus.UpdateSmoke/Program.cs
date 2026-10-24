using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Nexus.Core;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

if(args.Length<2||args.Length>3)throw new ArgumentException("Informe a pasta de release, a versão e opcionalmente a URL do GitHub.");
var feed=Path.GetFullPath(args[0]);var version=args[1];
var root=Path.Combine(Path.GetTempPath(),"nexus-update-test-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);int checks=0;
void Assert(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
async Task RunProcess(string executable,IEnumerable<string> arguments)
{
    var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true};
    foreach(var arg in arguments)start.ArgumentList.Add(arg);
    start.Environment["NEXUS_DATA_DIR"]=Path.Combine(root,"userdata");
    using var process=Process.Start(start)!;
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
    await process.WaitForExitAsync(timeout.Token);Assert(process.ExitCode==0,"Processo falhou: "+executable+" exit="+process.ExitCode);
}
try
{
    var install=Path.Combine(root,"app");
    ZipFile.ExtractToDirectory(Directory.GetFiles(feed,"*Portable.zip").Single(),install);
    var manifest=Path.Combine(install,"current","sq.version");
    var old=XDocument.Load(manifest);var versionNode=old.Descendants().Single(e=>e.Name.LocalName=="version");
    versionNode.Value="0.0.1";old.Save(manifest);
    var userdata=Path.Combine(root,"userdata");var store=new NexusStore(userdata);
    store.Register("update-fixture","temporary12345");store.Login("update-fixture","temporary12345");
    var database=store.Create("Preservar na atualização");store.Execute(database.Id,"CREATE TABLE exemplo(id INTEGER PRIMARY KEY, nome TEXT)");
    store.Execute(database.Id,"INSERT INTO exemplo(nome) VALUES('preservado')");
    var preservedToken=store.CreateApiKey("preservar chave",false,[database.Id]);
    var before=Directory.GetFiles(userdata,"*",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
    UpdateManager Manager(string current,string source,string packageFolder)=>new(source,locator:new TestVelopackLocator("Nexus.DatabaseStudio",current,packageFolder,Path.Combine(install,"current"),install,Path.Combine(install,"Update.exe"),"win"));
    var packages=Path.Combine(install,"packages");Directory.CreateDirectory(packages);
    var manager=Manager("0.0.1",feed,packages);
    var update=await manager.CheckForUpdatesAsync();Assert(update?.TargetFullRelease.Version.ToString()==version,"Detecção de versão nova");
    await manager.DownloadUpdatesAsync(update!);
    Assert(manager.UpdatePendingRestart?.Version.ToString()==version,"Download não preparou atualização");
    Assert(await Manager(version,feed,packages).CheckForUpdatesAsync() is null,"Versão atual oferecida novamente");
    Assert(await Manager("999.0.0",feed,packages).CheckForUpdatesAsync() is null,"Downgrade indevido");
    var corrupt=Path.Combine(root,"corrupt");Directory.CreateDirectory(corrupt);
    var json=JsonNode.Parse(File.ReadAllText(Path.Combine(feed,"releases.win.json")))!;
    foreach(var asset in json["Assets"]!.AsArray())
    {
        asset!["SHA256"]=new string('0',64);asset["SHA1"]=new string('0',40);
        File.Copy(Path.Combine(feed,asset["FileName"]!.GetValue<string>()),Path.Combine(corrupt,asset["FileName"]!.GetValue<string>()));
    }
    File.WriteAllText(Path.Combine(corrupt,"releases.win.json"),json.ToJsonString());
    var corruptPackages=Path.Combine(root,"corrupt-packages");Directory.CreateDirectory(corruptPackages);
    var corruptManager=Manager("0.0.1",corrupt,corruptPackages);
    bool rejected=false;
    try{await corruptManager.DownloadUpdatesAsync((await corruptManager.CheckForUpdatesAsync())!);}catch{rejected=true;}
    Assert(rejected,"Pacote com hash inválido aceito");
    Assert(corruptManager.UpdatePendingRestart is null,"Pacote inválido ficou pronto para reiniciar");
    var package=Directory.GetFiles(packages,"*-full.nupkg").Single();
    await RunProcess(Path.Combine(install,"Update.exe"),["--silent","apply","--norestart","--package",package]);
    Assert(XDocument.Load(manifest).Descendants().Single(e=>e.Name.LocalName=="version").Value==version,"Aplicação real da atualização");
    await RunProcess(Path.Combine(install,"current","Nexus.exe"),["--startup-check"]);
    Assert(before.All(entry=>File.Exists(entry.Key)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry.Key)))==entry.Value),"Arquivos de usuário alterados pelo atualizador");
    var reopened=new NexusStore(userdata);reopened.Login("update-fixture","temporary12345");
    Assert(reopened.AuthenticateApiKey(preservedToken).DatabaseIds.Contains(database.Id),"Chave de API não preservada");
    Assert((string)reopened.Execute(database.Id,"SELECT nome FROM exemplo").Table.Rows[0][0]=="preservado","Login/dados não preservados");
    if(args.Length==3)
    {
        var remotePackages=Path.Combine(root,"remote-packages");Directory.CreateDirectory(remotePackages);
        var remote=new UpdateManager(new GithubSource(args[2],null,false),locator:new TestVelopackLocator("Nexus.DatabaseStudio","0.0.1",remotePackages,Path.Combine(install,"current"),install,Path.Combine(install,"Update.exe"),"win"));
        var remoteUpdate=await remote.CheckForUpdatesAsync();Assert(remoteUpdate?.TargetFullRelease.Version.ToString()==version,"Feed público do GitHub não oferece a versão publicada");
        await remote.DownloadUpdatesAsync(remoteUpdate!);
        Assert(remote.UpdatePendingRestart?.Version.ToString()==version,"Download público do GitHub não preparou a atualização");
    }
    Console.WriteLine($"PASS: {checks} verificações de atualização, hash inválido, downgrade, troca real de arquivos, abertura e preservação de dados.");
}
finally
{
    // Only this randomly created fixture directory is removed.
    if(Directory.Exists(root))Directory.Delete(root,true);
}
