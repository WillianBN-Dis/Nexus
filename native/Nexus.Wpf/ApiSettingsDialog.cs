using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Nexus.Api;
using Nexus.Core;

namespace Nexus.Wpf;
public sealed class ApiSettingsDialog:FormWindow
{
    private readonly NexusStore store;
    private readonly LocalApiHost api;
    private readonly CheckBox enabled=new(){Content="Ativar API na rede local"};
    private readonly CheckBox startup=new(){Content="Iniciar em segundo plano com o Windows"};
    private readonly ComboBox address=new();
    private readonly TextBox port=new();
    private readonly TextBox keyName=new(){Text="Meu dispositivo"};
    private readonly CheckBox write=new(){Content="Permitir escrita (inserir, editar e excluir registros)"};
    private readonly ListBox keys=new(){MinHeight=85,MaxHeight=180,DisplayMemberPath="Name"};
    private readonly TextBox token=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap};
    private readonly List<(DatabaseItem Db,CheckBox Check)> shares=[];
    internal ApiSettingsDialog(Window owner,NexusStore store,LocalApiHost api):base(owner,"API e rede","Compartilhe somente os bancos que seus dispositivos precisam acessar.",820,900)
    {
        this.store=store;this.api=api;SaveButton.Content="Salvar servidor";
        FieldsScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Hidden;FieldsScroll.Padding=new Thickness(0);
        enabled.IsChecked=api.Settings.Enabled;startup.IsChecked=api.Settings.StartWithWindows;
        address.ItemsSource=LocalApiHost.Addresses();address.SelectedItem=api.Settings.Address;
        if(address.SelectedItem is null)address.SelectedIndex=0;port.Text=api.Settings.Port.ToString();
        FieldsPanel.Children.Add(Note(api.Running?"● API em execução · "+api.Address:"○ API desligada"));
        FieldsPanel.Children.Add(enabled);FieldsPanel.Children.Add(Caption("Endereço deste computador"));FieldsPanel.Children.Add(address);
        FieldsPanel.Children.Add(Caption("Porta"));FieldsPanel.Children.Add(port);startup.Margin=new Thickness(0,14,0,0);FieldsPanel.Children.Add(startup);
        FieldsPanel.Children.Add(Note("HTTP para rede local confiável: chaves e dados não são criptografados no transporte. Não encaminhe esta porta no roteador. O servidor é compartilhado neste computador; cada conta gerencia apenas suas próprias chaves."));
        var utilities=new WrapPanel();AddButton(utilities,"Copiar endereço",()=>Clipboard.SetText($"http://{address.SelectedItem}:{Port()}/api/bancos"));
        AddButton(utilities,"Liberar no Firewall (rede privada)",Firewall);FieldsPanel.Children.Add(utilities);
        AddButton(utilities,"Ver log da API",()=>new ApiLogWindow(this,api.Log).ShowDialog());
        FieldsPanel.Children.Add(Caption("Nova chave de acesso"));FieldsPanel.Children.Add(keyName);write.Margin=new Thickness(0,12,0,12);FieldsPanel.Children.Add(write);
        FieldsPanel.Children.Add(Note("Selecione os bancos permitidos. A chave não dá acesso aos demais bancos nem permite criar ou excluir tabelas."));
        foreach(var db in store.List())
        {
            var check=new CheckBox{Content=db.Name,Margin=new Thickness(0,5,0,5)};shares.Add((db,check));FieldsPanel.Children.Add(check);
        }
        var create=new WrapPanel();AddButton(create,"Gerar chave",()=>
        {token.Text=store.CreateApiKey(keyName.Text,write.IsChecked==true,shares.Where(s=>s.Check.IsChecked==true).Select(s=>s.Db.Id));api.Log.Write("chave_criada",new{nome=keyName.Text,escrita=write.IsChecked==true});RefreshKeys();Feedback.Text="Copie a chave agora. Ela não será exibida novamente.";});FieldsPanel.Children.Add(create);
        FieldsPanel.Children.Add(token);var copy=new WrapPanel();AddButton(copy,"Copiar chave",()=>{if(token.Text.Length==0)throw new ArgumentException("Gere ou renove uma chave primeiro.");Clipboard.SetText(token.Text);});FieldsPanel.Children.Add(copy);
        FieldsPanel.Children.Add(Caption("Chaves desta conta"));FieldsPanel.Children.Add(keys);RefreshKeys();
        keys.SelectionChanged+=(s,e)=>
        {
            if(keys.SelectedItem is ApiKeyInfo selected)Feedback.Text=(selected.CanWrite?"Leitura e escrita":"Somente leitura")+" · "+selected.DatabaseIds.Length+" banco(s).";
        };
        var manage=new WrapPanel();AddButton(manage,"Renovar chave",()=>
        {
            var selected=SelectedKey();var dialog=new ConfirmationDialog(this,"Renovar chave","A chave antiga deixará de funcionar. Copie a nova e atualize seus dispositivos.",()=>{token.Text=store.RotateApiKey(selected.Id);return Task.CompletedTask;});
            if(dialog.ShowDialog()==true){api.Log.Write("chave_renovada",new{id=selected.Id,nome=selected.Name});Feedback.Text="Chave renovada. Copie a nova chave agora.";}
        });
        AddButton(manage,"Revogar chave",()=>
        {
            var selected=SelectedKey();var dialog=new ConfirmationDialog(this,"Revogar acesso","Os dispositivos que usam esta chave perderão o acesso nas próximas solicitações.",()=>{store.RevokeApiKey(selected.Id);return Task.CompletedTask;});
            if(dialog.ShowDialog()==true){api.Log.Write("chave_revogada",new{id=selected.Id,nome=selected.Name});token.Clear();RefreshKeys();Feedback.Text="Chave revogada.";}
        });FieldsPanel.Children.Add(manage);
        FieldsPanel.Children.Add(Note("Exemplo: GET /api/bancos com o cabeçalho Authorization: Bearer SUA_CHAVE. Consulte API.md no repositório para filtros e escrita de registros. Ao fechar a janela principal, o Nexus continua pela bandeja; use Encerrar Nexus e API para desligar tudo."));
    }
    private int Port()=>int.TryParse(port.Text,out var value)&&value>=1024&&value<=65535?value:throw new ArgumentException("Porta: 1024 a 65535.");
    private ApiKeyInfo SelectedKey()=>keys.SelectedItem as ApiKeyInfo??throw new ArgumentException("Selecione uma chave.");
    private void RefreshKeys()=>keys.ItemsSource=store.ApiKeys();
    private void AddButton(Panel parent,string label,Action action)
    {
        var button=new Button{Content=label,Margin=new Thickness(0,8,8,8)};
        button.Click+=(s,e)=>{try{action();}catch(Exception error){Feedback.Text=error.Message;}};parent.Children.Add(button);
    }
    private void Firewall()
    {
        var ip=address.SelectedItem?.ToString()??"127.0.0.1";
        if(ip=="127.0.0.1")throw new ArgumentException("Escolha o IP da rede antes de liberar o Firewall.");
        var executable=Environment.ProcessPath!.Replace("'","''");
        var command=$"Get-NetFirewallRule -Name 'Nexus.LocalApi' -ErrorAction SilentlyContinue | Remove-NetFirewallRule; New-NetFirewallRule -Name 'Nexus.LocalApi' -DisplayName 'Nexus API (rede privada)' -Direction Inbound -Action Allow -Protocol TCP -LocalAddress '{ip}' -LocalPort {Port()} -RemoteAddress LocalSubnet -Profile Private -Program '{executable}'";
        Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(command))){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
        Feedback.Text="Confirme a solicitação do Windows para liberar a porta somente na rede privada.";
    }
    protected override async Task Save()
    {
        var previous=api.Settings;var next=new ApiSettings(enabled.IsChecked==true,address.SelectedItem?.ToString()??"127.0.0.1",Port(),startup.IsChecked==true);
        if(previous.StartWithWindows!=next.StartWithWindows)MainWindow.SetWindowsStartup(next.StartWithWindows);
        try{await api.ConfigureAsync(next);}
        catch{if(previous.StartWithWindows!=next.StartWithWindows)MainWindow.SetWindowsStartup(previous.StartWithWindows);throw;}
    }
}
