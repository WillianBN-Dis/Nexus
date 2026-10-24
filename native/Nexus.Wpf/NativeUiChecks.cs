using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Nexus.Core;

namespace Nexus.Wpf;
internal static class NativeUiChecks
{
    internal static async Task Run(App app,string[] args)
    {
        var data=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"nexus-wpf-ui-"+Guid.NewGuid().ToString("N"));
        var output=System.IO.Path.GetFullPath("native/qa");System.IO.Directory.CreateDirectory(output);
        try
        {
            var window=new MainWindow(data);app.MainWindow=window;window.Show();
            await window.ExerciseAsync(output);
            await window.InitializeBackground(false);
            var auth=new NexusStore(data);auth.Login("interface","temporary12345");
            var token=auth.CreateApiKey("bandeja",false,[auth.List().First().Id]);
            var socket=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);socket.Start();
            int port=((System.Net.IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
            await window.ApiHost!.ConfigureAsync(new Nexus.Api.ApiSettings(true,"127.0.0.1",port));
            window.Close();
            AssertBackground(!window.IsVisible,"fechar não ocultou a janela na bandeja");
            using var client=new System.Net.Http.HttpClient();client.DefaultRequestHeaders.Authorization=new("Bearer",token);
            using var response=await client.GetAsync(window.ApiHost.Address+"/api/bancos");
            AssertBackground(response.IsSuccessStatusCode,"API não continuou atendendo com a janela fechada");
            window.ShowFromTray();AssertBackground(window.IsVisible,"não restaurou a janela da bandeja");
            if(window.ApiHost is not null)await window.ApiHost.DisposeAsync();
            window.AllowTestExit();
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"ui-result.txt"),"PASS: WPF real. Login, tamanhos, CRUD visual, SQL, cancelamento, layout, API e rede, bandeja e atendimento HTTP com janela fechada.\n");
            window.Close();app.Shutdown(0);
        }
        catch(Exception error)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"ui-result.txt"),"FAIL: "+error);
            app.Shutdown(1);
        }
        finally{if(System.IO.Directory.Exists(data))System.IO.Directory.Delete(data,true);}
    }
    private static void AssertBackground(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
public partial class MainWindow
{
    internal void AllowTestExit()=>exiting=true;
    private static void AssertUi(bool condition,string message){if(!condition)throw new InvalidOperationException("UI check: "+message);}
    private async Task Settle()
    {await Dispatcher.InvokeAsync(()=>UpdateLayout(),DispatcherPriority.ApplicationIdle);await Task.Delay(200);}
    private async Task Idle()
    {
        var clock=Stopwatch.StartNew();while(busy&&clock.Elapsed<TimeSpan.FromSeconds(15))await Task.Delay(20);
        AssertUi(!busy,"operação excedeu o prazo");await Settle();
    }
    private void Snapshot(string folder,string name)
    {
        UpdateLayout();var target=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
        target.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));
        using var file=System.IO.File.Create(System.IO.Path.Combine(folder,name+".png"));encoder.Save(file);
    }
    private async Task Modal(FormWindow dialog,Func<Task> steps)
    {
        Exception? failure=null;
        dialog.Loaded+=async(s,e)=>
        {
            try{await Task.Delay(100);await steps();}
            catch(Exception error){failure=error;while(dialog.Saving)await Task.Delay(20);dialog.Close();}
        };
        AssertUi(dialog.ShowDialog()==true||failure is not null,"formulário foi cancelado inesperadamente");
        if(failure is not null)throw failure;await Settle();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(int n=0;n<VisualTreeHelper.GetChildrenCount(root);n++)
        {
            var child=VisualTreeHelper.GetChild(root,n);yield return child;
            foreach(var descendant in Descendants(child))yield return descendant;
        }
    }
    internal async Task ExerciseAsync(string output)
    {
        foreach(var size in new[]{(1040d,740d),(1280d,880d),(1920d,1080d)})
        {
            Width=size.Item1;Height=size.Item2;await Settle();
            var bounds=Username.TransformToAncestor(this).TransformBounds(new Rect(0,0,Username.ActualWidth,Username.ActualHeight));
            AssertUi(bounds.Left>=0&&bounds.Right<ActualWidth&&bounds.Top>=0&&bounds.Bottom<ActualHeight,"campo de login fora da janela");
        }
        Width=1280;Height=880;await Settle();Snapshot(output,"01-login");
        WindowState=WindowState.Maximized;await Settle();Snapshot(output,"02-login-maximizado");WindowState=WindowState.Normal;await Settle();
        Username.Text="interface";Password.Password="temporary12345";
        Register_Click(this,new());await Idle();AssertUi(LoginFeedback.Text.Contains("Conta criada"),"cadastro");
        Login_Click(this,new());await Idle();AssertUi(WorkspaceView.Visibility==Visibility.Visible,"login");
        await Run("Teste",async()=>{var db=await Task.Run(()=>store!.Create("Projeto de demonstração"));await RefreshDatabases(db.Id);});
        AssertUi(selected is not null&&EmptyState.Visibility==Visibility.Visible,"banco vazio");
        var id=selected!.Id;
        var designer=new TableDesignerDialog(this,(name,columns)=>Task.Run(()=>store!.CreateTable(id,name,columns)));
        await Modal(designer,async()=>
        {
            designer.TableName.Text="clientes";designer.SubmitForTest();
            while(designer.Saving)await Task.Delay(20);
            AssertUi(!string.IsNullOrEmpty(designer.ValidationMessage)&&designer.IsVisible,"validação da coluna vazia");
            designer.Columns[0].Name.Text="nome";designer.Columns[0].Required.IsChecked=true;
            designer.AddField();designer.Columns[1].Name.Text="idade";designer.Columns[1].Type.SelectedItem="INTEGER";
            designer.AddField();designer.Columns[2].Name.Text="contato";
            designer.UpdateLayout();
            var target=new RenderTargetBitmap((int)designer.ActualWidth,(int)designer.ActualHeight,96,96,PixelFormats.Pbgra32);target.Render((Visual)designer.Content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using(var file=System.IO.File.Create(System.IO.Path.Combine(output,"03-criar-tabela.png")))encoder.Save(file);
            designer.SubmitForTest();
        });
        table="clientes";await Run("Teste",ReloadTables);AssertUi(page?.Info.Columns.Count==4,"nova tabela");
        var record=new RecordFormDialog(this,page!.Info,null,values=>Task.Run(()=>store!.InsertRecord(id,table,values)));
        await Modal(record,async()=>
        {
            record.Fields.Single(f=>f.Column.Name=="nome").Input.Text="Ana";
            var age=record.Fields.Single(f=>f.Column.Name=="idade");age.Input.Text="inválido";
            record.SubmitForTest();while(record.Saving)await Task.Delay(20);
            AssertUi(record.IsVisible&&record.ValidationMessage.Contains("inválido"),"validação de inteiro");
            age.Input.Text="30";record.Fields.Single(f=>f.Column.Name=="contato").Input.Text="ana@example.test";
            record.SubmitForTest();
        });
        await Run("Teste",ReloadPage);AssertUi(page!.Rows.Count==1,"inserção");
        TableData.SelectedIndex=0;await Settle();AssertUi(EditRecordButton.IsEnabled,"seleção habilita edição");
        var original=CurrentRow();var edit=new RecordFormDialog(this,page.Info,original,values=>Task.Run(()=>store!.ChangeRecord(id,table,original,values)));
        await Modal(edit,()=>{edit.Fields.Single(f=>f.Column.Name=="nome").Input.Text="Ana Maria";edit.SubmitForTest();return Task.CompletedTask;});
        await Run("Teste",ReloadPage);AssertUi(Equals(page!.Rows[0].Values[1],"Ana Maria"),"edição");
        var column=new AddColumnDialog(this,(name,type)=>Task.Run(()=>store!.AddColumn(id,table,name,type)));
        await Modal(column,()=>{column.ColumnName.Text="cidade";column.SubmitForTest();return Task.CompletedTask;});
        await Run("Teste",ReloadPage);AssertUi(page!.Info.Columns.Count==5,"coluna");
        await Task.Run(()=>store!.InsertRecord(id,table,new Dictionary<string,object?>{{"nome","Bruno"},{"idade",22L},{"contato","bruno@example.test"},{"cidade","Curitiba"}}));
        sort="idade";descending=true;await Run("Teste",ReloadPage);AssertUi(Convert.ToInt64(page!.Rows[0].Values[2])==30,"ordenação");
        int blankSources=0,disabledFrames=0,renderedFrames=0;
        var sourceDescriptor=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty,typeof(DataGrid));
        EventHandler sourceChanged=(s,e)=>{if(TableData.ItemsSource is null)blankSources++;};
        EventHandler frame=(s,e)=>{renderedFrames++;if(!Databases.IsEnabled||!WorkspaceTabs.IsEnabled)disabledFrames++;};
        sourceDescriptor.AddValueChanged(TableData,sourceChanged);CompositionTarget.Rendering+=frame;
        try
        {
            await Run("Teste de recarga",async()=>{await Task.Delay(100);await ReloadPage();});
            await Run("Teste de criação",async()=>
            {
                await Task.Run(()=>store!.CreateTable(id,"transitoria",new[]{new ColumnSpec("descricao","TEXT")}));
                table="transitoria";offset=0;sort=null;await Task.Delay(100);await ReloadTables();
            });
            await Run("Teste de exclusão",async()=>
            {
                await Task.Run(()=>store!.DropTable(id,"transitoria"));
                table=null;await Task.Delay(100);await ReloadTables();
            });
            AssertUi(blankSources==0,"a grade foi limpa durante uma recarga");
            AssertUi(renderedFrames>0&&disabledFrames==0,"a navegação mudou para o tema desabilitado durante a recarga");
            AssertUi(TableData.IsVisible&&page?.Rows.Count==2,"a tabela não reapareceu após criar/excluir");
        }
        finally{sourceDescriptor.RemoveValueChanged(TableData,sourceChanged);CompositionTarget.Rendering-=frame;}
        AssertUi(Status.Parent==StatusBadge,"status fora do cabeçalho");
        AssertUi(MoveDatabaseButton.Content.ToString()=="Mover de Pasta","texto de mover");
        var bodyWidth=new System.Windows.Media.FormattedText(CreateDatabaseButton.Content.ToString(),System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,
            new Typeface(CreateDatabaseButton.FontFamily,CreateDatabaseButton.FontStyle,CreateDatabaseButton.FontWeight,CreateDatabaseButton.FontStretch),CreateDatabaseButton.FontSize,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip).Width;
        AssertUi(CreateDatabaseButton.ActualWidth>=bodyWidth+CreateDatabaseButton.Padding.Left+CreateDatabaseButton.Padding.Right,"texto do botão novo banco cortado");
        await Task.Run(()=>store!.InsertRecord(id,table!,new Dictionary<string,object?>{{"nome","Excluir fixture"},{"idade",99L}}));
        await Run("Teste",ReloadPage);
        var removable=page!.Rows.Single(r=>Equals(r.Values[1],"Excluir fixture"));
        var confirm=new ConfirmationDialog(this,"Excluir registro","Remover o registro selecionado?",()=>Task.Run(()=>store!.ChangeRecord(id,table!,removable,delete:true)));
        await Modal(confirm,()=>{confirm.SubmitForTest();return Task.CompletedTask;});
        await Run("Teste",ReloadPage);AssertUi(page!.Rows.Count==2,"popup de exclusão");
        foreach(var word in new[]{"DELETAR","deletar"})
        {
            bool accepted=false;
            var deletion=new TextPromptDialog(this,"Confirmação","Digite DELETAR para confirmar.","",value=>{accepted=true;return Task.CompletedTask;},"DELETAR",ignoreCase:true);
            await Modal(deletion,async()=>
            {
                deletion.Input.Text="errado";deletion.SubmitForTest();while(deletion.Saving)await Task.Delay(20);
                AssertUi(!accepted&&deletion.IsVisible,"confirmação incorreta aceita");
                deletion.Input.Text=word;deletion.SubmitForTest();
            });
            AssertUi(accepted,"confirmação DELETAR sem diferenciar maiúsculas");
        }
        Snapshot(output,"04-visual");
        WindowState=WindowState.Maximized;await Settle();Snapshot(output,"05-visual-maximizado");WindowState=WindowState.Normal;await Settle();
        WorkspaceTabs.SelectedItem=SqlTab;Sql.Text="SELECT nome, idade, cidade FROM clientes ORDER BY idade DESC;";Query_Click(this,new());await Idle();
        AssertUi(QueryData.ItemsSource is DataView view&&view.Count==2,"resultado SQL");Snapshot(output,"06-sql");
        AssertUi(Sql.ToolTip is null,"tooltip do SQL ainda aparece");
        AssertUi(ExecuteButton.ActualHeight<=48,"botão executar esticado");
        AssertUi(!Descendants(Root).OfType<GridSplitter>().Any(),"editor SQL permite arraste manual");
        Sql.Text="SELECT * FROM tabela_inexistente";Query_Click(this,new());await Idle();
        AssertUi(((SolidColorBrush)Status.Foreground).Color==Color.FromRgb(255,173,185),"erro não ficou vermelho no cabeçalho");
        await Run("Teste",ReloadPage);
        AssertUi(((SolidColorBrush)Status.Foreground).Color==Color.FromRgb(158,227,199),"sucesso não ficou verde no cabeçalho");
        int ticks=0;var heartbeat=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};heartbeat.Tick+=(s,e)=>ticks++;heartbeat.Start();
        Sql.Text="WITH RECURSIVE seq(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM seq WHERE n<1000000000) SELECT SUM(n) FROM seq";
        Query_Click(this,new());await Task.Delay(300);CancelQuery_Click(this,new());await Idle();heartbeat.Stop();
        AssertUi(ticks>=3&&Status.Text.Contains("cancelada"),"consulta bloqueou a UI ou não cancelou");
        WorkspaceTabs.SelectedItem=VisualTab;await Idle();AssertUi(TableData.IsVisible,"alternância de modos");
        await Run("Teste",async()=>{var disposable=await Task.Run(()=>store!.Create("Fixture de exclusão"));await Task.Run(()=>store!.Delete(disposable.Id));});
        AssertUi(store!.List().Count==1,"exclusão para Lixeira");
        foreach(var size in new[]{(1040d,740d),(1920d,1080d),(2560d,1440d),(1280d,880d)})
        {
            Width=size.Item1;Height=size.Item2;await Settle();
            foreach(var tab in new[]{VisualTab,SqlTab}.Concat(QueryTabs.Items.Cast<TabItem>()))
            {
                var header=new FormattedText(tab.Header.ToString(),System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,
                    new Typeface(tab.FontFamily,tab.FontStyle,tab.FontWeight,tab.FontStretch),tab.FontSize,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                if(tab.IsVisible)AssertUi(tab.ActualWidth+1>=header.Width+tab.Padding.Left+tab.Padding.Right,"cabeçalho cortado: "+tab.Header);
            }
            var visualContent=(FrameworkElement)VisualTab.Content;
            var contentTop=visualContent.TranslatePoint(new Point(),WorkspaceTabs).Y;
            AssertUi(contentTop<100,"conteúdo visual centralizado em vez de ocupar o painel: "+contentTop);
            AssertUi(visualContent.ActualHeight>WorkspaceTabs.ActualHeight-120,"conteúdo visual não preenche a altura");
            AssertUi(ScrollViewer.GetVerticalScrollBarVisibility(Databases)==ScrollBarVisibility.Hidden,"barra de bancos visível");
            AssertUi(TableData.ActualHeight>50&&NextPage.IsVisible,"layout do workspace: "+TableData.ActualHeight+" visible="+NextPage.IsVisible+" mode="+WorkspaceTabs.SelectedIndex);
        }
        var apiDialog=new ApiSettingsDialog(this,store!,new Nexus.Api.LocalApiHost(store!.Root));
        await Modal(apiDialog,()=>
        {
            apiDialog.UpdateLayout();
            var image=new RenderTargetBitmap((int)apiDialog.ActualWidth,(int)apiDialog.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render((Visual)apiDialog.Content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=System.IO.File.Create(System.IO.Path.Combine(output,"07-api-rede.png")))encoder.Save(file);
            apiDialog.SubmitForTest();return Task.CompletedTask;
        });
        Logout_Click(this,new());AssertUi(LoginView.Visibility==Visibility.Visible,"logout");
    }
}
