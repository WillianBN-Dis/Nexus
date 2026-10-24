using System.Reflection;

namespace Nexus.Wpf;
internal static class AppVersion
{
    internal static string Label=>typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"1.0.0";
}
