using System.IO;
using System.Windows;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Nexus.Wpf;
public partial class MainWindow
{
    private UpdateManager? updater;
    private bool checkingUpdates;
    private string? notifiedUpdate;
    private readonly DispatcherTimer updateTimer=new(){Interval=TimeSpan.FromHours(6)};
    internal void StartUpdateChecks()
    {
        updater=new UpdateManager(new GithubSource("https://github.com/WillianBN-Dis/Nexus",null,false));
        updateTimer.Tick+=async(s,e)=>await CheckUpdates(false);
        Closed+=(s,e)=>updateTimer.Stop();
        updateTimer.Start();
        _=CheckUpdates(false);
    }
    private async void CheckUpdates_Click(object sender,RoutedEventArgs e)=>await CheckUpdates(true);
    private async Task CheckUpdates(bool manual)
    {
        if(checkingUpdates)return;
        checkingUpdates=true;CheckUpdatesButton.IsEnabled=false;
        try
        {
            updater??=new UpdateManager(new GithubSource("https://github.com/WillianBN-Dis/Nexus",null,false));
            if(!updater.IsInstalled)
            {
                if(manual)ShowUpdateMessage("Use o instalador Nexus-Setup.exe para habilitar as atualizações automáticas.");
                return;
            }
            if(updater.UpdatePendingRestart is not null){ShowUpdateReady();return;}
            if(manual)ShowUpdateMessage("Verificando atualizações…");
            var update=await Task.Run(()=>updater.CheckForUpdatesAsync());
            if(update is null)
            {
                if(manual)ShowUpdateMessage("Seu Nexus está atualizado · "+AppVersion.Label);
                return;
            }
            ShowUpdateMessage("Baixando Nexus "+update.TargetFullRelease.Version+"…");
            await Task.Run(()=>updater.DownloadUpdatesAsync(update,percent=>Dispatcher.BeginInvoke(()=>UpdateMessage.Text="Baixando atualização · "+percent+"%")));
            ShowUpdateReady();
        }
        catch(Exception error)
        {
            try
            {
                var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nexus");Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder,"updates.log"),DateTimeOffset.Now+" "+error+Environment.NewLine);
            }
            catch { /* Update failures must not prevent database work. */ }
            if(manual)ShowUpdateMessage("Não foi possível verificar ou baixar a atualização. Tente novamente mais tarde.");
            else UpdateNotice.Visibility=Visibility.Collapsed;
        }
        finally{checkingUpdates=false;CheckUpdatesButton.IsEnabled=true;}
    }
    private void ShowUpdateMessage(string message)
    {
        UpdateMessage.Text=message;RestartUpdateButton.Visibility=Visibility.Collapsed;UpdateNotice.Visibility=Visibility.Visible;
    }
    private void ShowUpdateReady()
    {
        UpdateMessage.Text="Nexus "+updater!.UpdatePendingRestart!.Version+" pronto. Será instalado na próxima abertura.";
        RestartUpdateButton.Visibility=Visibility.Visible;UpdateNotice.Visibility=Visibility.Visible;
        if(!IsVisible&&tray is not null&&notifiedUpdate!=updater.UpdatePendingRestart.Version.ToString())
        {
            notifiedUpdate=updater.UpdatePendingRestart.Version.ToString();
            tray.ShowBalloonTip(5000,"Nexus · Atualização pronta","Use Atualizar e reiniciar no menu da bandeja quando quiser aplicar a atualização.",System.Windows.Forms.ToolTipIcon.Info);
        }
    }
    private void DismissUpdate_Click(object sender,RoutedEventArgs e)=>UpdateNotice.Visibility=Visibility.Collapsed;
    private async void RestartUpdate_Click(object sender,RoutedEventArgs e)
    {
        if(busy||OwnedWindows.Count>0)
        {
            UpdateMessage.Text="Conclua a operação e feche os formulários antes de reiniciar.";return;
        }
        try
        {
            var pending=updater?.UpdatePendingRestart;
            if(pending is not null)
            {
                if(ApiHost is not null)await ApiHost.DisposeAsync();
                if(tray is not null)tray.Visible=false;
                updater!.ApplyUpdatesAndRestart(pending);
            }
        }
        catch
        {
            if(tray is not null)tray.Visible=true;
            if(ApiHost is not null)try{await ApiHost.RestoreAsync();}catch{ }
            ShowUpdateMessage("Não foi possível reiniciar. Encerre o Nexus pela bandeja e abra novamente para aplicar a atualização.");
        }
    }
}
