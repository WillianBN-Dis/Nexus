using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Nexus.Wpf;
internal static partial class WindowAppearance
{
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    public static void Apply(Window window)
    {
        if(!OperatingSystem.IsWindows())return;
        try
        {
            var handle=new WindowInteropHelper(window).Handle;
            void Attribute(int key,int value)=>DwmSetWindowAttribute(handle,key,ref value,4);
            Attribute(20,1);Attribute(33,2);Attribute(35,0x001B100D);Attribute(36,0x00FBF0F1);
        }
        catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
}
