using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Nexus.Api;

namespace Nexus.Wpf;
internal sealed class ApiLogWindow:Window
{
    internal ApiLogWindow(Window owner,ApiLog log)
    {
        Owner=owner;Title="Nexus · Log da API";Width=1000;Height=700;MinWidth=650;MinHeight=450;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=(Brush)Application.Current.FindResource("Bg");
        var root=new Grid{Margin=new Thickness(24)};root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel{Margin=new Thickness(0,0,0,16)};header.Children.Add(new TextBlock{Text="Atividade da API",FontSize=26,FontWeight=FontWeights.SemiBold});
        var location=new TextBlock{Text=log.Path+"\nÚltimos 300 eventos · até 5 arquivos de aproximadamente 5 MB",FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=(Brush)Application.Current.FindResource("Muted"),Margin=new Thickness(0,8,0,0)};header.Children.Add(location);root.Children.Add(header);
        var text=new TextBox{Text=log.Tail(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,FontFamily=new FontFamily("Consolas"),FontSize=12,VerticalContentAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden};Grid.SetRow(text,1);root.Children.Add(text);
        var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
        void Button(string label,Action action){var button=new Button{Content=label,Margin=new Thickness(8,0,0,0)};button.Click+=(s,e)=>{try{action();}catch(Exception error){location.Text=error.Message;}};buttons.Children.Add(button);}
        Button("Atualizar",()=>{text.Text=log.Tail();text.ScrollToEnd();if(log.LastError is not null)location.Text="Falha ao gravar log: "+log.LastError;});
        Button("Copiar",()=>Clipboard.SetText(text.Text));
        Button("Exportar log",()=>{var dialog=new SaveFileDialog{FileName="nexus-api-log.jsonl",Filter="Log JSON Lines (*.jsonl)|*.jsonl|Todos os arquivos (*.*)|*.*"};if(dialog.ShowDialog(this)==true)log.Export(dialog.FileName);});
        Button("Fechar",Close);Grid.SetRow(buttons,2);root.Children.Add(buttons);Content=root;SourceInitialized+=(s,e)=>WindowAppearance.Apply(this);Loaded+=(s,e)=>text.ScrollToEnd();
    }
}
