using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Nexus.Wpf;
public partial class TrayMenu:ContextMenu
{
    public static readonly DependencyProperty ApiStatusProperty=DependencyProperty.Register(nameof(ApiStatus),typeof(string),typeof(TrayMenu),new PropertyMetadata("● API desligada"));
    public static readonly DependencyProperty StatusBrushProperty=DependencyProperty.Register(nameof(StatusBrush),typeof(Brush),typeof(TrayMenu),new PropertyMetadata(new SolidColorBrush(Color.FromRgb(155,166,195))));
    public string ApiStatus{get=>(string)GetValue(ApiStatusProperty);set=>SetValue(ApiStatusProperty,value);}
    public Brush StatusBrush{get=>(Brush)GetValue(StatusBrushProperty);set=>SetValue(StatusBrushProperty,value);}
    public TrayMenu()
    {
        InitializeComponent();
        Opened+=(s,e)=>
        {
            // The app window may be hidden; activate this popup so outside clicks dismiss it.
            if(PresentationSource.FromVisual(this) is HwndSource source)SetForegroundWindow(source.Handle);
            Focus();
        };
    }
    [LibraryImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);
}
