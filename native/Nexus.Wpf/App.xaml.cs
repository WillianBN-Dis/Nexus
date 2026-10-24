using System.Windows;
using System.Windows.Threading;
namespace Nexus.Wpf;
public partial class App:Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException+=(s,args)=>
        {
            var folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nexus");System.IO.Directory.CreateDirectory(folder);
            System.IO.File.AppendAllText(System.IO.Path.Combine(folder,"wpf-errors.log"),args.Exception+Environment.NewLine);
            MessageBox.Show(args.Exception.Message,"Nexus");args.Handled=true;
        };
        if(e.Args.Contains("--ui-test"))
        {
            await NativeUiChecks.Run(this,e.Args);return;
        }
        var window=new MainWindow();MainWindow=window;window.Show();
        if(e.Args.Contains("--startup-check"))
        {
            var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
            timer.Tick+=(s,args)=>{timer.Stop();Shutdown(MainWindow?.IsVisible==true?0:1);};timer.Start();
        }
    }
}
