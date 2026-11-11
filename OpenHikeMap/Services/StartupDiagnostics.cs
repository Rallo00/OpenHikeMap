namespace OpenHikeMap.Services;

public static class StartupDiagnostics
{
    private static readonly object Gate = new();
    public static string FilePath => Path.Combine(
        Environment.GetEnvironmentVariable("OPENHIKEMAP_DATA_DIRECTORY") is { Length: > 0 } directory
            ? directory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMap"),
        "standalone-startup.log");

    public static void Write(string message, Exception? error = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:O} {message}{(error is null ? "" : Environment.NewLine + error)}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
