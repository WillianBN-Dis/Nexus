using System.ComponentModel;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Nexus.Core;

namespace Nexus.Wpf;
public partial class MainWindow:Window
{
    private NexusStore? store;
    private readonly string? dataRoot;
    private DatabaseItem? selected;
    private List<DatabaseItem> databases=[];
    private TablePage? page;
    private string? displayedDatabase,displayedTable;
    private string? table,sort;
    private bool descending,busy,suppressSelection;
    private int offset,loginFailures;
    private DateTimeOffset retryLogin;
    private CancellationTokenSource? queryCancellation;
    public MainWindow():this(null){}
    public MainWindow(string? root)
    {
        dataRoot=root??Environment.GetEnvironmentVariable("NEXUS_DATA_DIR");InitializeComponent();
        LoginVersionLabel.Text="DATABASE STUDIO   /   "+AppVersion.Label;AccountVersionLabel.Text="Conta local · "+AppVersion.Label;
        SourceInitialized+=(s,e)=>WindowAppearance.Apply(this);
    }
    private async Task<bool> Run(string message,Func<Task> work,bool query=false)
    {
        if(busy)return false;
        var previousFocus=Keyboard.FocusedElement;
        busy=true;LoginView.IsEnabled=false;BusyInputShield.Visibility=Visibility.Visible;BusyText.Text=message;BusyStripe.Visibility=Visibility.Visible;
        BusyStripe.Focus();UpdateActions();
        CancelQueryButton.Visibility=query?Visibility.Visible:Visibility.Collapsed;
        if(query)queryCancellation=new();
        try
        {
            store??=await Task.Run(()=>new NexusStore(dataRoot));
            await work();return true;
        }
        catch(Exception error)
        {
            var messageText=queryCancellation?.IsCancellationRequested==true?"Consulta cancelada.":error.Message;
            if(LoginView.Visibility==Visibility.Visible)LoginFeedback.Text=messageText;
            else SetStatus(messageText,queryCancellation?.IsCancellationRequested!=true);
            if(query){QueryData.ItemsSource=null;QueryText.Text=messageText;QueryTabs.SelectedIndex=1;}
            return false;
        }
        finally
        {
            queryCancellation?.Dispose();queryCancellation=null;
            LoginView.IsEnabled=true;BusyInputShield.Visibility=Visibility.Collapsed;BusyStripe.Visibility=Visibility.Collapsed;busy=false;UpdateActions();
            if(previousFocus is UIElement element&&element.IsVisible&&element.IsEnabled)element.Focus();
        }
    }
    private void SetStatus(string message,bool error=false)
    {
        Status.Text=message;Status.ToolTip=message;
        Status.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(error?"#FFADB9":"#9EE3C7"));
        StatusBadge.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(error?"#381E2D":"#1A302D"));
    }
    private string PasswordValue=>ShowPassword.IsChecked==true?VisiblePassword.Text:Password.Password;
    private async void Login_Click(object sender,RoutedEventArgs e)
    {
        var name=Username.Text;var password=PasswordValue;
        await Run("Abrindo seu espaço…",async()=>
        {
            if(DateTimeOffset.UtcNow<retryLogin)throw new InvalidOperationException("Aguarde 30 segundos antes de tentar novamente.");
            try{await Task.Run(()=>store!.Login(name,password));}
            catch{if(++loginFailures>=5)retryLogin=DateTimeOffset.UtcNow.AddSeconds(30);throw;}
            loginFailures=0;Password.Clear();VisiblePassword.Clear();LoginFeedback.Text="";
            AccountName.Text=name;LoginView.Visibility=Visibility.Collapsed;WorkspaceView.Visibility=Visibility.Visible;
            await RefreshDatabases();
        });
    }
    private async void Register_Click(object sender,RoutedEventArgs e)
    {
        var name=Username.Text;var password=PasswordValue;
        await Run("Criando sua conta…",async()=>{await Task.Run(()=>store!.Register(name,password));LoginFeedback.Text="Conta criada. Agora clique em Entrar.";});
    }
    private void PasswordVisibility_Changed(object sender,RoutedEventArgs e)
    {
        if(VisiblePassword is null||Password is null)return;
        bool visible=ShowPassword.IsChecked==true;
        if(visible)VisiblePassword.Text=Password.Password;else Password.Password=VisiblePassword.Text;
        VisiblePassword.Visibility=visible?Visibility.Visible:Visibility.Collapsed;Password.Visibility=visible?Visibility.Collapsed:Visibility.Visible;
    }
    private async Task RefreshDatabases(string? selectId=null)
    {
        databases=await Task.Run(()=>store!.List());var wanted=selectId??selected?.Id;
        var filtered=databases.Where(item=>item.Name.Contains(DatabaseSearch.Text,StringComparison.CurrentCultureIgnoreCase)).ToList();
        suppressSelection=true;
        try{Databases.ItemsSource=filtered;selected=filtered.FirstOrDefault(item=>item.Id==wanted);Databases.SelectedItem=selected;}
        finally{suppressSelection=false;}
        await LoadSelected();
    }
    private async Task LoadSelected()
    {
        page=null;table=null;sort=null;offset=0;descending=false;
        TableData.ItemsSource=null;StructureData.ItemsSource=null;QueryData.ItemsSource=null;
        QueryText.Text="Execute uma consulta para visualizar o resultado.";
        DatabaseTitle.Text=selected?.Name??"Vamos criar algo novo?";DatabasePath.Text=selected?.Path??"Selecione um banco ou crie seu primeiro projeto.";
        await ReloadTables();
    }
    private async Task ReloadTables()
    {
        var id=selected?.Id;var requestedTable=table;var requestedSort=sort;var requestedOffset=offset;var reverse=descending;
        try
        {
            // Fetch a complete snapshot first. No collection is cleared while I/O runs.
            var next=await Task.Run(()=>
            {
                var names=id is null?new List<string>():store!.Tables(id);
                var target=requestedTable is not null&&names.Contains(requestedTable)?requestedTable:names.FirstOrDefault();
                var order=target==requestedTable?requestedSort:null;var skip=target==requestedTable?requestedOffset:0;
                var data=id is null||target is null?null:FetchPage(id,target,order,reverse,skip);
                return (names,target,data);
            });
            table=next.target;
            suppressSelection=true;
            try
            {
                if(!Tables.Items.Cast<string>().SequenceEqual(next.names))Tables.ItemsSource=next.names;
                Tables.SelectedItem=table;
                ApplyPage(next.data);
            }
            finally{suppressSelection=false;}
        }
        catch{page=null;UpdateActions();throw;}
    }
    private sealed record PageSnapshot(TablePage Page,string? Sort,int Offset);
    private PageSnapshot FetchPage(string id,string name,string? order,bool reverse,int skip)
    {
        var info=store!.TableInfo(id,name);
        if(order is not null&&!info.Columns.Any(col=>col.Name==order))order=null;
        var data=store.BrowsePage(id,name,order,reverse,skip);
        if(skip>0&&data.Rows.Count==0){skip=Math.Max(0,skip-200);data=store.BrowsePage(id,name,order,reverse,skip);}
        return new(data,order,skip);
    }
    private async Task ReloadPage()
    {
        var id=selected?.Id;var name=table;var order=sort;var reverse=descending;var skip=offset;
        try
        {
            var next=id is null||name is null?null:await Task.Run(()=>FetchPage(id,name,order,reverse,skip));
            ApplyPage(next);
        }
        catch{page=null;UpdateActions();throw;}
    }
    private void ApplyPage(PageSnapshot? snapshot)
    {
        page=snapshot?.Page;sort=snapshot?.Sort;offset=snapshot?.Offset??0;
        displayedDatabase=selected?.Id;displayedTable=table;
        // All UI changes happen together, after the background read has finished.
        TableData.ItemsSource=page?.Table.DefaultView;
        StructureData.ItemsSource=page?.Info.Columns.Select(col=>new{Coluna=col.Name,Tipo=col.Type,Obrigatória=col.Required?"Sim":"Opcional",Chave=col.Key>0?"Primária":"—",Calculada=col.Hidden!=0?"Sim":"Não"}).ToList();
        if(page is not null)
        {
            TableSummary.Text=page.Info.Columns.Count+" colunas · SQLite";
            RowsSummary.Text=(page.Rows.Count==0?"0":(offset+1).ToString())+"–"+(offset+page.Rows.Count)+" nesta página"+(sort is null?" · Clique no cabeçalho para ordenar":" · "+sort+(descending?" ↓":" ↑"));
            SetStatus("Tabela "+table+" carregada");
        }
        else{TableSummary.Text="Nenhuma tabela";RowsSummary.Text="Selecione uma tabela";SetStatus(selected is null?"Selecione um banco":"Banco carregado · nenhuma tabela");}
        PageLabel.Text=(offset/200+1).ToString("D2");UpdateEmpty();UpdateActions();
    }
    private void UpdateEmpty()
    {
        bool empty=page is null||page.Rows.Count==0;EmptyState.Visibility=empty?Visibility.Visible:Visibility.Collapsed;
        EmptyTitle.Text=selected is null?"Seus dados começam aqui":table is null?"Um espaço para suas ideias":"Sua tabela está pronta";
        EmptyHint.Text=selected is null?"Selecione um banco na barra lateral ou crie um novo.":table is null?"Crie uma tabela, defina as colunas e comece a organizar seus dados.":"Adicione o primeiro registro usando os campos do formulário.";
        EmptyAction.Content=table is null?"＋ Criar primeira tabela":"＋ Adicionar registro";
    }
    private void UpdateActions()
    {
        if(RenameDatabaseButton is null)return;
        var hasDatabase=selected is not null&&!busy;var hasTable=hasDatabase&&table is not null&&page is not null&&displayedDatabase==selected?.Id&&displayedTable==table;
        RenameDatabaseButton.IsEnabled=MoveDatabaseButton.IsEnabled=DeleteDatabaseButton.IsEnabled=NewTableButton.IsEnabled=ExecuteButton.IsEnabled=hasDatabase;
        Tables.IsEnabled=hasDatabase&&Tables.Items.Count>0;
        NewRecordButton.IsEnabled=AddColumnButton.IsEnabled=DropTableButton.IsEnabled=hasTable;
        var row=hasTable&&TableData.SelectedItem is DataRowView&&page!.Info.Identity.Count>0;
        EditRecordButton.IsEnabled=DeleteRecordButton.IsEnabled=row;
        PreviousPage.IsEnabled=hasTable&&offset>0;NextPage.IsEnabled=hasTable&&page!.HasMore;EmptyAction.IsEnabled=hasDatabase;
    }
    private async void Database_Selected(object sender,SelectionChangedEventArgs e)
    {
        if(busy||suppressSelection)return;selected=Databases.SelectedItem as DatabaseItem;await Run("Carregando banco…",LoadSelected);
    }
    private async void DatabaseSearch_Changed(object sender,TextChangedEventArgs e)
    {
        if(busy||suppressSelection||WorkspaceView?.Visibility!=Visibility.Visible)return;
        await Run("Filtrando bancos…",()=>RefreshDatabases());
    }
    private async void Table_Selected(object sender,SelectionChangedEventArgs e)
    {
        if(busy||suppressSelection||selected is null)return;table=Tables.SelectedItem as string;sort=null;offset=0;descending=false;
        await Run("Carregando tabela…",ReloadPage);
    }
    private async void WorkspaceTabs_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!ReferenceEquals(e.OriginalSource,WorkspaceTabs)||busy||store is null||selected is null)return;
        if(WorkspaceTabs.SelectedItem==VisualTab)await Run("Atualizando tabelas…",ReloadTables);
    }
    private void Record_Selected(object sender,SelectionChangedEventArgs e)=>UpdateActions();
    private async void Table_Sorting(object sender,DataGridSortingEventArgs e)
    {
        e.Handled=true;if(busy||page is null)return;
        var name=e.Column.Header.ToString()!;descending=sort==name?!descending:false;sort=name;offset=0;
        if(await Run("Ordenando dados…",ReloadPage))
        {foreach(var column in TableData.Columns)column.SortDirection=column.Header.ToString()==sort?(descending?ListSortDirection.Descending:ListSortDirection.Ascending):null;}
    }
    private void Grid_GeneratingColumn(object sender,DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.MinWidth=120;
        e.Column.Width=e.PropertyName.Equals("id",StringComparison.OrdinalIgnoreCase)?new DataGridLength(100):new DataGridLength(1,DataGridLengthUnitType.Star);
        if(e.Column is DataGridTextColumn text)
        {
            text.ElementStyle=new Style(typeof(TextBlock))
            {Setters={new Setter(TextBlock.MarginProperty,new Thickness(12,0,12,0)),new Setter(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center),new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis)}};
            if(text.Binding is System.Windows.Data.Binding binding)binding.Converter=new CellDisplayConverter();
        }
    }
    private void Grid_GeneratedColumns(object? sender,EventArgs e)
    {
        if(sender is not DataGrid grid)return;
        foreach(var column in grid.Columns)
        {
            bool id=string.Equals(column.Header?.ToString(),"id",StringComparison.OrdinalIgnoreCase);
            column.MinWidth=id?88:130;
            column.Width=id?new DataGridLength(96):new DataGridLength(1,DataGridLengthUnitType.Star);
        }
    }
    private RowSnapshot CurrentRow()
    {
        if(page is null||TableData.SelectedItem is not DataRowView row)throw new ArgumentException("Selecione uma linha para continuar.");
        return page.Rows[page.Table.Rows.IndexOf(row.Row)];
    }
    private async void NewTable_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||busy)return;var id=selected.Id;string? created=null;
        var dialog=new TableDesignerDialog(this,async(name,columns)=>{await Task.Run(()=>store!.CreateTable(id,name,columns));created=name.Trim();});
        if(dialog.ShowDialog()==true){table=created;offset=0;sort=null;await Run("Carregando nova tabela…",ReloadTables);}
    }
    private async void NewRecord_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||table is null||page is null||busy)return;var id=selected.Id;var name=table;
        var dialog=new RecordFormDialog(this,page.Info,null,values=>Task.Run(()=>store!.InsertRecord(id,name,values)));
        if(dialog.ShowDialog()==true)await Run("Atualizando registros…",ReloadPage);
    }
    private async void EditRecord_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||table is null||page is null||TableData.SelectedItem is null||busy)return;
        var original=CurrentRow();var id=selected.Id;var name=table;
        var dialog=new RecordFormDialog(this,page.Info,original,values=>Task.Run(()=>store!.ChangeRecord(id,name,original,values)));
        if(dialog.ShowDialog()==true)await Run("Atualizando registro…",ReloadPage);
    }
    private async void DeleteRecord_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||table is null||TableData.SelectedItem is null||busy)return;
        var original=CurrentRow();var id=selected.Id;var name=table;
        var dialog=new ConfirmationDialog(this,"Excluir registro","Excluir a linha selecionada da tabela “"+name+"”?",()=>Task.Run(()=>store!.ChangeRecord(id,name,original,delete:true)));
        if(dialog.ShowDialog()==true)await Run("Atualizando registros…",ReloadPage);
    }
    private async void AddColumn_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||table is null||busy)return;var id=selected.Id;var name=table;
        var dialog=new AddColumnDialog(this,(column,type)=>Task.Run(()=>store!.AddColumn(id,name,column,type)));
        if(dialog.ShowDialog()==true)await Run("Atualizando estrutura…",ReloadPage);
    }
    private async void DropTable_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||table is null||busy)return;var id=selected.Id;var name=table;
        var dialog=new TextPromptDialog(this,"Excluir tabela","Todos os registros serão removidos. Digite o nome da tabela para confirmar.","",_=>Task.Run(()=>store!.DropTable(id,name)),name);
        if(dialog.ShowDialog()==true){table=null;offset=0;sort=null;await Run("Atualizando tabelas…",ReloadTables);}
    }
    private void EmptyAction_Click(object sender,RoutedEventArgs e){if(table is null)NewTable_Click(sender,e);else NewRecord_Click(sender,e);}
    private async void NextPage_Click(object sender,RoutedEventArgs e){if(page?.HasMore!=true)return;offset+=200;await Run("Próxima página…",ReloadPage);}
    private async void PreviousPage_Click(object sender,RoutedEventArgs e){offset=Math.Max(0,offset-200);await Run("Página anterior…",ReloadPage);}
    private void Table_DoubleClick(object sender,MouseButtonEventArgs e)
    {
        var element=e.OriginalSource as DependencyObject;
        while(element is not null&&element is not DataGridRow)element=VisualTreeHelper.GetParent(element);
        if(element is DataGridRow&&EditRecordButton.IsEnabled)EditRecord_Click(sender,e);
    }
    private async void Query_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||busy)return;var id=selected.Id;var sql=Sql.Text;
        await Run("Executando SQL…",async()=>
        {
            bool readOnly=await Task.Run(()=>store!.IsReadOnly(id,sql));
            if(!readOnly&&MessageBox.Show(this,"Esta instrução pode alterar ou excluir dados. Executar?","Nexus · Confirmar SQL",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            QueryData.ItemsSource=null;QueryText.Text="";
            var token=queryCancellation!.Token;
            var result=await Task.Run(()=>store!.Execute(id,sql,token));
            QueryData.ItemsSource=result.Table.DefaultView;
            SetStatus(result.ReadOnly?result.Table.Rows.Count+" linha(s)"+(result.Truncated?" · primeiras 1.000":" · concluído"):result.Affected+" registro(s) afetado(s) · concluído");
            var text=new StringBuilder(Status.Text+Environment.NewLine+Environment.NewLine);
            for(int n=0;n<result.Table.Rows.Count;n++)
            {text.AppendLine("["+(n+1)+"]");for(int c=0;c<result.Table.Columns.Count;c++)text.AppendLine(result.Table.Columns[c].ColumnName+": "+CellDisplayConverter.Format(result.Table.Rows[n][c]));text.AppendLine();}
            QueryText.Text=text.ToString();if(result.Table.Columns.Count==0)QueryTabs.SelectedIndex=1;
        },query:true);
    }
    private void CancelQuery_Click(object sender,RoutedEventArgs e)=>queryCancellation?.Cancel();
    private async void Create_Click(object sender,RoutedEventArgs e)
    {
        if(busy)return;string? created=null;
        var dialog=new TextPromptDialog(this,"Novo banco de dados","Dê um nome ao seu próximo projeto.","",async name=>{created=(await Task.Run(()=>store!.Create(name))).Id;});
        if(dialog.ShowDialog()==true)await Run("Abrindo novo banco…",async()=>{DatabaseSearch.Clear();await RefreshDatabases(created);});
    }
    private async void Rename_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||busy)return;var id=selected.Id;
        var dialog=new TextPromptDialog(this,"Renomear banco","Altere o nome exibido no seu espaço.",selected.Name,name=>Task.Run(()=>store!.Rename(id,name)));
        if(dialog.ShowDialog()==true)await Run("Atualizando banco…",()=>RefreshDatabases(id));
    }
    private async void Move_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||busy)return;var id=selected.Id;
        var folder=new OpenFolderDialog{Title="Escolha a pasta no disco de destino"};if(folder.ShowDialog(this)!=true)return;
        if(MessageBox.Show(this,"Feche outros programas que estejam usando este banco. O Nexus verificará a cópia antes de remover a origem. Continuar?","Nexus · Mover banco",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes)return;
        await Run("Transferindo e verificando o banco…",async()=>{var result=await Task.Run(()=>store!.Move(id,folder.FolderName));await RefreshDatabases(id);SetStatus(result);});
    }
    private async void DeleteDatabase_Click(object sender,RoutedEventArgs e)
    {
        if(selected is null||busy)return;var item=selected;
        var dialog=new TextPromptDialog(this,"Excluir banco","O banco “"+item.Name+"” será enviado para a Lixeira. Digite DELETAR para confirmar.","",_=>Task.Run(()=>store!.Delete(item.Id)),"DELETAR",ignoreCase:true);
        if(dialog.ShowDialog()==true){selected=null;await Run("Atualizando seu espaço…",()=>RefreshDatabases());}
    }
    private async void Refresh_Click(object sender,RoutedEventArgs e)=>await Run("Atualizando bancos…",()=>RefreshDatabases());
    private void Logout_Click(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        suppressSelection=true;
        try
        {
            WorkspaceView.Visibility=Visibility.Collapsed;LoginView.Visibility=Visibility.Visible;
            store?.Logout();selected=null;page=null;table=null;databases=[];
            Databases.ItemsSource=null;Tables.ItemsSource=null;TableData.ItemsSource=null;QueryData.ItemsSource=null;StructureData.ItemsSource=null;DatabaseSearch.Clear();
        }
        finally{suppressSelection=false;}
        UpdateActions();
    }
    private void Window_SizeChanged(object sender,SizeChangedEventArgs e)
    {
        Resources["BodySize"]=Math.Clamp(ActualHeight/850*14,14,18);
        if(DatabaseHero is null)return;
        bool compact=ActualHeight<820;
        DatabaseHero.Padding=new Thickness(compact?14:20);
        HeroEyebrow.Visibility=Visibility.Visible;HeroBrandLabel.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        DatabasePath.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        DatabaseTitle.FontSize=compact?22:28;DatabaseTitle.Margin=compact?new Thickness(0):new Thickness(0,10,0,6);
        DatabaseTitle.ToolTip=DatabasePath.Text;
    }
    private void Window_Closing(object? sender,CancelEventArgs e)
    {
        if(busy){queryCancellation?.Cancel();e.Cancel=true;SetStatus("Aguarde a operação terminar antes de fechar.");return;}
        if(tray is not null&&!exiting){e.Cancel=true;HideToTray();}
    }
    private void Window_KeyDown(object sender,KeyEventArgs e)
    {
        if(busy){if(e.Key==Key.Escape)queryCancellation?.Cancel();e.Handled=true;return;}
        if(e.Key==Key.Enter&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&WorkspaceView.Visibility==Visibility.Visible&&WorkspaceTabs.SelectedItem==SqlTab){e.Handled=true;Query_Click(sender,e);}
    }
}
public sealed class CellDisplayConverter:System.Windows.Data.IValueConverter
{
    public static string Format(object? value)=>value is null or DBNull?"NULL":value is byte[] bytes?"0x"+System.Convert.ToHexString(bytes):RecordFormDialog.Display(value);
    public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Format(value);
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}
