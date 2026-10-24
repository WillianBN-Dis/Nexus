using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Nexus.Api;
using Forms=System.Windows.Forms;

namespace Nexus.Wpf;
public partial class MainWindow
{
    internal LocalApiHost? ApiHost;
    private Forms.NotifyIcon? tray;
    private TrayMenu? trayMenu;
    private bool exiting,trayHintShown;
    private EventWaitHandle? openSignal;
    private RegisteredWaitHandle? openWait;
    internal async Task InitializeBackground(bool background)
    {
        ApiHost=new LocalApiHost(dataRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nexus"));
        ApiHost.Log.Failed+=()=>Dispatcher.BeginInvoke(()=>tray?.ShowBalloonTip(6000,"Nexus · Log da API","Não foi possível gravar o log. Confira o espaço em disco e as permissões da pasta.",Forms.ToolTipIcon.Warning));
        var resource=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/nexus.ico"));
        using(var stream=resource.Stream)
        using(var icon=new System.Drawing.Icon(stream))tray=new Forms.NotifyIcon{Icon=(System.Drawing.Icon)icon.Clone(),Text="Nexus · Em segundo plano",Visible=true};
        trayMenu=new TrayMenu();
        trayMenu.OpenItem.Click+=(s,e)=>ShowFromTray();
        trayMenu.StopApiItem.Click+=async(s,e)=>
        {
            try{await ApiHost.ConfigureAsync(ApiHost.Settings with{Enabled=false});UpdateTrayStatus();tray.ShowBalloonTip(3000,"Nexus","API desligada.",Forms.ToolTipIcon.Info);}catch{ShowFromTray();}
        };
        trayMenu.UpdateItem.Click+=(s,e)=>{ShowFromTray();RestartUpdate_Click(this,new());};
        trayMenu.ExitItem.Click+=async(s,e)=>await ExitApplication();
        tray.MouseUp+=(s,e)=>{if(e.Button==Forms.MouseButtons.Right)Dispatcher.BeginInvoke(OpenTrayMenu);};
        tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(ShowFromTray);tray.BalloonTipClicked+=(s,e)=>Dispatcher.Invoke(ShowFromTray);
        openSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Nexus.Open"+(dataRoot is null?"":".test."+Guid.NewGuid().ToString("N")));
        openWait=ThreadPool.RegisterWaitForSingleObject(openSignal,(s,t)=>Dispatcher.BeginInvoke(ShowFromTray),null,Timeout.Infinite,false);
        StateChanged+=(s,e)=>{if(WindowState==WindowState.Minimized)HideToTray();};
        Application.Current.SessionEnding+=(s,e)=>exiting=true;
        Closed+=(s,e)=>{if(trayMenu is not null)trayMenu.IsOpen=false;openWait?.Unregister(null);openSignal?.Dispose();tray?.Icon?.Dispose();tray?.Dispose();};
        if(background)Hide();else Show();
        try{await ApiHost.RestoreAsync();UpdateTrayStatus();}
        catch{tray.ShowBalloonTip(6000,"Nexus · API","Não foi possível iniciar a API. Abra API e rede para conferir endereço e porta.",Forms.ToolTipIcon.Warning);}
    }
    internal void ShowFromTray()
    {if(trayMenu is not null)trayMenu.IsOpen=false;Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();}
    internal void OpenTrayMenu()
    {
        if(trayMenu is null)return;
        trayMenu.ApiStatus=ApiHost?.Running==true?"● API em execução":"● API desligada";
        trayMenu.StatusBrush=new System.Windows.Media.SolidColorBrush(ApiHost?.Running==true?System.Windows.Media.Color.FromRgb(158,227,199):System.Windows.Media.Color.FromRgb(155,166,195));
        trayMenu.StopApiItem.IsEnabled=ApiHost?.Running==true;
        trayMenu.UpdateItem.IsEnabled=updater?.UpdatePendingRestart is not null&&!busy&&OwnedWindows.Count==0;
        trayMenu.IsOpen=false;trayMenu.IsOpen=true;
    }
    internal void HideToTray()
    {
        Hide();if(trayHintShown||tray is null)return;trayHintShown=true;
        tray.ShowBalloonTip(4000,"Nexus","Continuo em segundo plano. Use este ícone para abrir ou encerrar o app e a API.",Forms.ToolTipIcon.Info);
    }
    private async Task ExitApplication()
    {
        ShowFromTray();if(busy||OwnedWindows.Count>0){SetStatus("Conclua a operação e feche os formulários antes de encerrar.",true);return;}
        if(exiting)return;exiting=true;
        try{if(ApiHost is not null)await ApiHost.DisposeAsync();Close();Application.Current.Shutdown();}
        catch{exiting=false;SetStatus("Não foi possível encerrar. Tente novamente.",true);}
    }
    private void ApiSettings_Click(object sender,RoutedEventArgs e)
    {
        if(busy||store is null)return;
        ApiHost??=new LocalApiHost(store.Root);
        new ApiSettingsDialog(this,store,ApiHost).ShowDialog();UpdateTrayStatus();
    }
    private void UpdateTrayStatus()
    {if(tray is not null)tray.Text=ApiHost?.Running==true?"Nexus · API em "+ApiHost.Address:"Nexus · API desligada";}
    internal static void SetWindowsStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)??throw new IOException("Não foi possível acessar a inicialização do Windows.");
        if(!enabled){key.DeleteValue("Nexus",false);return;}
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        var launcher=folder.Name=="current"?Path.Combine(folder.Parent!.FullName,"Nexus.exe"):"";
        if(!File.Exists(launcher))throw new ArgumentException("Use o Nexus instalado para ativar a inicialização com o Windows.");
        key.SetValue("Nexus","\""+launcher+"\" --background");
    }
}
