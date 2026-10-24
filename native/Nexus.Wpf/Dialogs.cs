using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexus.Core;

namespace Nexus.Wpf;
public abstract class FormWindow:Window
{
    protected readonly StackPanel FieldsPanel=new();
    protected readonly ScrollViewer FieldsScroll=new();
    protected readonly Button SaveButton=new(){Content="Salvar  →",MinWidth=150};
    protected readonly Button CancelButton=new(){Content="Cancelar",IsCancel=true,MinWidth=110};
    protected readonly TextBlock Feedback=new(){TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(255,171,191))};
    private bool saving;
    internal bool Saving=>saving;
    internal string ValidationMessage=>Feedback.Text;
    internal void SubmitForTest()=>SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    protected FormWindow(Window owner,string heading,string description,double width=720,double height=650)
    {
        Owner=owner;Title="Nexus — "+heading;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Width=width;Height=Math.Min(height,SystemParameters.WorkArea.Height-70);MinWidth=540;MinHeight=400;
        Background=(Brush)Application.Current.FindResource("Bg");
        var root=new Grid{Margin=new Thickness(24),Background=(Brush)FindResource("Bg")};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var top=new StackPanel{Margin=new Thickness(0,0,0,20)};
        top.Children.Add(new TextBlock{Text="NEXUS  /  DATABASE STUDIO",Foreground=(Brush)FindResource("Accent"),FontSize=11,FontWeight=FontWeights.Bold});
        top.Children.Add(new TextBlock{Text=heading,FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,8)});
        top.Children.Add(new TextBlock{Text=description,Foreground=(Brush)FindResource("Muted"),TextWrapping=TextWrapping.Wrap});root.Children.Add(top);
        var scroll=FieldsScroll;scroll.Content=FieldsPanel;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.Padding=new Thickness(0,0,10,0);
        Grid.SetRow(scroll,1);root.Children.Add(scroll);
        var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};
        footer.Children.Add(Feedback);Feedback.Margin=new Thickness(0,0,0,12);
        var actions=new DockPanel();DockPanel.SetDock(SaveButton,Dock.Right);SaveButton.Style=(Style)FindResource("Primary");actions.Children.Add(SaveButton);CancelButton.HorizontalAlignment=HorizontalAlignment.Left;actions.Children.Add(CancelButton);footer.Children.Add(actions);
        Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
        SaveButton.Click+=async(s,e)=>
        {
            if(saving)return;saving=true;SaveButton.IsEnabled=false;CancelButton.IsEnabled=false;FieldsPanel.IsEnabled=false;
            try{Feedback.Text="Salvando…";await Save();DialogResult=true;}
            catch(Exception error){Feedback.Text=error.Message;}
            finally{saving=false;SaveButton.IsEnabled=true;CancelButton.IsEnabled=true;FieldsPanel.IsEnabled=true;}
        };
        Closing+=(s,e)=>{if(saving&&DialogResult!=true)e.Cancel=true;};
        SourceInitialized+=(s,e)=>WindowAppearance.Apply(this);
    }
    protected abstract Task Save();
    protected static TextBlock Caption(string text)=>new(){Text=text,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,8)};
    protected static TextBlock Note(string text)=>new(){Text=text,Foreground=(Brush)Application.Current.FindResource("Muted"),FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,12)};
    protected static Border Card(UIElement content)=>new(){Style=(Style)Application.Current.FindResource("Card"),Child=content,Margin=new Thickness(0,0,0,12)};
}
public sealed class TextPromptDialog:FormWindow
{
    public readonly TextBox Input=new();
    public string Value=>Input.Text.Trim();
    private readonly Func<string,Task> save;
    private readonly string? confirmation;
    private readonly bool ignoreCase;
    public TextPromptDialog(Window owner,string title,string description,string initial,Func<string,Task> save,string? confirm=null,bool ignoreCase=false)
        :base(owner,title,description,600,440)
    {
        this.save=save;confirmation=confirm;this.ignoreCase=ignoreCase;
        FieldsPanel.Children.Add(Caption(confirm is null?"Nome":ignoreCase?"Confirmação":"Digite o nome para confirmar"));Input.Text=initial;FieldsPanel.Children.Add(Input);
        if(confirm is not null){FieldsPanel.Children.Add(Note(ignoreCase?"Digite "+confirm+" (maiúsculas ou minúsculas).":"Nome esperado: "+confirm));SaveButton.Content="Confirmar exclusão";SaveButton.Style=(Style)FindResource("Danger");}
        Loaded+=(s,e)=>{Input.Focus();Input.SelectAll();};
    }
    protected override Task Save()
    {
        if(confirmation is not null&&!string.Equals(ignoreCase?Input.Text.Trim():Input.Text,confirmation,ignoreCase?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new ArgumentException(ignoreCase?"Digite "+confirmation+" para confirmar.":"Digite exatamente o nome informado para confirmar.");
        if(string.IsNullOrWhiteSpace(Input.Text))throw new ArgumentException("Informe um nome.");
        return save(Value);
    }
}
public sealed class ColumnDraft
{
    public TextBox Name {get;}=new(){MinWidth=150};
    public ComboBox Type {get;}=new(){ItemsSource=new[]{"TEXT","INTEGER","REAL","BLOB"},SelectedIndex=0,MinWidth=120};
    public CheckBox Required {get;}=new(){Content="Obrigatória",VerticalAlignment=VerticalAlignment.Center};
}
public sealed class TableDesignerDialog:FormWindow
{
    public TextBox TableName {get;}=new();
    public List<ColumnDraft> Columns {get;}=[];
    private readonly StackPanel rows=new();
    private readonly Func<string,IReadOnlyList<ColumnSpec>,Task> save;
    public TableDesignerDialog(Window owner,Func<string,IReadOnlyList<ColumnSpec>,Task> save):base(owner,"Uma nova tabela para suas ideias","Defina as colunas; o Nexus cuida do SQL para você.",800,700)
    {
        this.save=save;SaveButton.Content="Criar tabela  →";
        FieldsPanel.Children.Add(Caption("Nome da tabela"));FieldsPanel.Children.Add(TableName);
        FieldsPanel.Children.Add(Card(Note("◆  id · INTEGER · Automático\nCada registro recebe um identificador único ao salvar.")));
        FieldsPanel.Children.Add(Caption("Suas colunas"));FieldsPanel.Children.Add(rows);
        var add=new Button{Content="＋ Adicionar coluna",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,12)};add.Click+=(s,e)=>AddField();FieldsPanel.Children.Add(add);
        FieldsPanel.Children.Add(Note("TEXT: texto · INTEGER: inteiro · REAL: decimal · BLOB: binário em hexadecimal"));
        AddField();Loaded+=(s,e)=>TableName.Focus();
    }
    public void AddField()
    {
        var draft=new ColumnDraft();Columns.Add(draft);
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new(130)});row.ColumnDefinitions.Add(new(){Width=new(125)});row.ColumnDefinitions.Add(new(){Width=new(40)});
        row.Children.Add(draft.Name);draft.Name.Margin=new Thickness(0,0,10,0);
        Grid.SetColumn(draft.Type,1);draft.Type.Margin=new Thickness(0,0,10,0);row.Children.Add(draft.Type);
        Grid.SetColumn(draft.Required,2);row.Children.Add(draft.Required);
        var remove=new Button{Content="×",Padding=new Thickness(8)};Grid.SetColumn(remove,3);row.Children.Add(remove);
        var card=Card(row);card.Padding=new Thickness(12);
        remove.Click+=(s,e)=>{Columns.Remove(draft);rows.Children.Remove(card);};rows.Children.Add(card);
    }
    protected override Task Save()
    {
        var definitions=Columns.Select(c=>new ColumnSpec(c.Name.Text,(string)c.Type.SelectedItem,c.Required.IsChecked==true)).ToArray();
        return save(TableName.Text,definitions);
    }
}
public sealed class AddColumnDialog:FormWindow
{
    public TextBox ColumnName{get;}=new();
    public ComboBox Type{get;}=new(){ItemsSource=new[]{"TEXT","INTEGER","REAL","BLOB"},SelectedIndex=0};
    private readonly Func<string,string,Task> save;
    public AddColumnDialog(Window owner,Func<string,string,Task> save):base(owner,"Adicionar coluna","Os registros existentes receberão NULL nesta coluna.",600,490)
    {this.save=save;FieldsPanel.Children.Add(Caption("Nome da coluna"));FieldsPanel.Children.Add(ColumnName);FieldsPanel.Children.Add(Caption("Tipo de dado"));FieldsPanel.Children.Add(Type);}
    protected override Task Save()=>save(ColumnName.Text,(string)Type.SelectedItem);
}
public sealed record RecordField(ColumnInfo Column,TextBox Input,ComboBox Mode);
public sealed class RecordFormDialog:FormWindow
{
    public List<RecordField> Fields{get;}=[];
    private readonly Func<IReadOnlyDictionary<string,object?>,Task> save;
    public RecordFormDialog(Window owner,TableInfo info,RowSnapshot? record,Func<IReadOnlyDictionary<string,object?>,Task> save)
        :base(owner,record is null?"Adicionar registro":"Editar registro","Escolha Valor, NULL ou Padrão para cada campo. * indica campo obrigatório.",720,700)
    {
        this.save=save;SaveButton.Content="Salvar registro  →";
        FieldsScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Hidden;
        FieldsScroll.Padding=new Thickness(0);
        for(int n=0;n<info.Columns.Count;n++)
        {
            var column=info.Columns[n];if(column.Hidden!=0)continue;
            var content=new StackPanel();content.Children.Add(Caption(column.Name+(column.Required?" *":"")));
            content.Children.Add(Note(column.Type+(column.Key>0?" · Chave primária":column.Required?" · Obrigatório":" · Opcional")));
            var input=new TextBox{MinHeight=44};
            if(column.Type.Contains("TEXT",StringComparison.OrdinalIgnoreCase)){input.AcceptsReturn=true;input.TextWrapping=TextWrapping.Wrap;input.MinHeight=60;}
            var mode=new ComboBox{ItemsSource=record is null?new[]{"Valor","NULL","Padrão"}:new[]{"Valor","NULL"},SelectedIndex=0,Width=150,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0)};
            bool autoId=column.Key>0&&column.Type.Equals("INTEGER",StringComparison.OrdinalIgnoreCase)&&info.Columns.Count(c=>c.Key>0)==1&&!info.Columns.Any(c=>info.Identity.Contains(c.Name));
            if(record is not null){var value=record.Values[n];mode.SelectedIndex=value is null?1:0;input.Text=Display(value);}
            else if(column.Default is not null||autoId)mode.SelectedIndex=2;
            void ApplyMode(){input.IsEnabled=(string)mode.SelectedItem=="Valor";}
            mode.SelectionChanged+=(s,e)=>ApplyMode();ApplyMode();
            content.Children.Add(input);content.Children.Add(mode);FieldsPanel.Children.Add(Card(content));Fields.Add(new(column,input,mode));
        }
    }
    public static string Display(object? value)=>value switch{null or DBNull=>"",byte[] bytes=>Convert.ToHexString(bytes),IFormattable f=>f.ToString(null,CultureInfo.InvariantCulture),_=>value.ToString()??""};
    protected override Task Save()
    {
        var values=new Dictionary<string,object?>();
        foreach(var field in Fields)
        {
            var mode=(string)field.Mode.SelectedItem;if(mode=="Padrão")continue;
            if(mode=="NULL"){values[field.Column.Name]=null;continue;}
            var text=field.Input.Text;var kind=field.Column.Type.ToUpperInvariant();object value=text;
            try
            {
                if(kind.Contains("INT"))value=long.Parse(text,CultureInfo.InvariantCulture);
                else if(kind.Contains("REAL")||kind.Contains("FLOA")||kind.Contains("DOUB"))
                {var number=double.Parse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture);if(!double.IsFinite(number))throw new FormatException();value=number;}
                else if(kind=="BLOB")value=Convert.FromHexString(text.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?text[2..]:text);
            }
            catch(FormatException){throw new ArgumentException("Valor inválido em "+field.Column.Name+" ("+kind+").");}
            catch(OverflowException){throw new ArgumentException("Valor fora do intervalo em "+field.Column.Name+".");}
            values[field.Column.Name]=value;
        }
        return save(values);
    }
}

public sealed class ConfirmationDialog:FormWindow
{
    private readonly Func<Task> save;
    public ConfirmationDialog(Window owner,string title,string description,Func<Task> save):base(owner,title,description,610,410)
    {
        this.save=save;SaveButton.Content="Excluir registro";SaveButton.Style=(Style)FindResource("Danger");
        FieldsPanel.Children.Add(Card(Note("Somente o registro selecionado será removido. Os demais dados da tabela serão mantidos.")));
    }
    protected override Task Save()=>save();
}
