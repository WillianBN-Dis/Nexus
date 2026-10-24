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
        var menu=new Forms.ContextMenuStrip{BackColor=System.Drawing.Color.FromArgb(21,26,43),ForeColor=System.Drawing.Color.White};
        menu.Items.Add("Abrir Nexus",null,(s,e)=>Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Desligar API",null,async(s,e)=>
        {
            try{await ApiHost.ConfigureAsync(ApiHost.Settings with{Enabled=false});UpdateTrayStatus();tray.ShowBalloonTip(3000,"Nexus","API desligada.",Forms.ToolTipIcon.Info);}catch{ShowFromTray();}
        });
        menu.Items.Add("Atualizar e reiniciar",null,(s,e)=>{ShowFromTray();RestartUpdate_Click(this,new());});
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Encerrar Nexus e API",null,async(s,e)=>await ExitApplication());
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(ShowFromTray);tray.BalloonTipClicked+=(s,e)=>Dispatcher.Invoke(ShowFromTray);
        openSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\Nexus.Open"+(dataRoot is null?"":".test."+Guid.NewGuid().ToString("N")));
        openWait=ThreadPool.RegisterWaitForSingleObject(openSignal,(s,t)=>Dispatcher.BeginInvoke(ShowFromTray),null,Timeout.Infinite,false);
        StateChanged+=(s,e)=>{if(WindowState==WindowState.Minimized)HideToTray();};
        Application.Current.SessionEnding+=(s,e)=>exiting=true;
        Closed+=(s,e)=>{openWait?.Unregister(null);openSignal?.Dispose();tray?.Icon?.Dispose();tray?.Dispose();};
        if(background)Hide();else Show();
        try{await ApiHost.RestoreAsync();UpdateTrayStatus();}
        catch{tray.ShowBalloonTip(6000,"Nexus · API","Não foi possível iniciar a API. Abra API e rede para conferir endereço e porta.",Forms.ToolTipIcon.Warning);}
    }
    internal void ShowFromTray()
    {Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();}
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
