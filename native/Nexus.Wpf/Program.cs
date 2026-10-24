using Velopack;

namespace Nexus.Wpf;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool diagnostic=args.Contains("--ui-test")||args.Contains("--startup-check");
        bool hook=args.Any(a=>a.StartsWith("--veloapp-",StringComparison.Ordinal));
        using var instance=new Mutex(false,"Local\\Nexus.DatabaseStudio");
        bool acquired=false;
        if(!diagnostic&&!hook)
        {
            try{acquired=instance.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
            if(!acquired){System.Windows.MessageBox.Show("O Nexus já está aberto. Use a janela existente.","Nexus");return;}
        }
        // Update handling must run before WPF creates any windows or opens user data.
        try
        {
            VelopackApp.Build().SetAutoApplyOnStartup(!diagnostic).Run();
            var app=new App();app.InitializeComponent();app.Run();
        }
        finally{if(acquired)instance.ReleaseMutex();}
    }
}
