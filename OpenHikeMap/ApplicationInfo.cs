namespace OpenHikeMap;

public static class ApplicationInfo
{
    public static System.Version InstalledVersion { get; } = typeof(ApplicationInfo).Assembly.GetName().Version ?? throw new InvalidOperationException("Application version metadata is missing.");
    public static string Version => DisplayVersion(InstalledVersion);
    public static string WindowTitle => $"OpenHikeMap v{Version}";
    public static string UserAgent => $"OpenHikeMap/{Version} (+https://github.com/Rallo00/)";
    public static string DisplayVersion(System.Version version) => version.Revision > 0 ? version.ToString(4) : version.Build > 0 ? version.ToString(3) : version.ToString(2);
}
