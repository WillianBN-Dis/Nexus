using System.Text.Json;

namespace Nexus.Api;
public sealed class ApiLog
{
    private readonly object sync=new();
    public string Folder{get;}
    public string Path=>System.IO.Path.Combine(Folder,"api.log");
    public string? LastError{get;private set;}
    public event Action? Failed;
    public ApiLog(string root)=>Folder=System.IO.Path.Combine(root,"logs");
    public void Write(string eventName,object details)
    {
        bool failed=false;
        lock(sync)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                if(File.Exists(Path)&&new FileInfo(Path).Length>=5*1024*1024)
                {
                    for(int n=3;n>=0;n--)
                    {
                        var source=n==0?Path:Path+"."+n;
                        if(File.Exists(source))File.Move(source,Path+"."+(n+1),true);
                    }
                }
                File.AppendAllText(Path,JsonSerializer.Serialize(new{horario=DateTimeOffset.Now,evento=eventName,detalhes=details})+Environment.NewLine);
                LastError=null;
            }
            catch(Exception error){failed=LastError is null;LastError=error.Message;}
        }
        if(failed)Failed?.Invoke();
    }
    public string Tail()
    {
        lock(sync)
        {
            try
            {
                if(!File.Exists(Path))return "Nenhum evento registrado ainda.";
                var lines=new Queue<string>();foreach(var line in File.ReadLines(Path)){lines.Enqueue(line);if(lines.Count>300)lines.Dequeue();}
                return string.Join(Environment.NewLine,lines);
            }
            catch(Exception error){return "Não foi possível ler o log: "+error.Message;}
        }
    }
    public void Export(string destination)
    {lock(sync){if(!File.Exists(Path))throw new IOException("O log ainda está vazio.");File.Copy(Path,destination,true);}}
}
