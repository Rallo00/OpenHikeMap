using System.Reflection;

namespace OpenHikeMap;

public static class ApplicationInfo
{
    public static string Version { get; } = typeof(ApplicationInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.9";
    public static string WindowTitle => $"OpenHikeMap v{Version}";
    public static string UserAgent => $"OpenHikeMap/{Version} (+https://github.com/Rallo00/)";
}
