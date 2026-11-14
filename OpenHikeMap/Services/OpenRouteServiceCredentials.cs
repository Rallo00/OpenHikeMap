namespace OpenHikeMap.Services;

internal static class OpenRouteServiceCredentials
{
    internal const string DefaultApiKey = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6IjQyMzI4MDY2ZmY0YzRjNGFhNTYwYTU1OTE5NmE3YTJjIiwiaCI6Im11cm11cjY0In0=";
    private static string KeyPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMap", "ors.key");
    internal static bool IsConfigured(IConfiguration configuration)
    {
        try { return !string.IsNullOrWhiteSpace(Load(configuration)); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    internal static void Save(string value)
    {
        value = value.Trim();
        if (value.Any(char.IsControl) || value.Length > 4096) throw new ArgumentException("Invalid API key.");
        if (value.Length == 0) { File.Delete(KeyPath); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
        var temporary = KeyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, value); File.Move(temporary, KeyPath, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static string Load(IConfiguration configuration)
    {
        if (File.Exists(KeyPath) && File.ReadAllText(KeyPath).Trim() is { Length: > 0 } localKey) return localKey;
        var configuredKey = configuration["OpenRouteService:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey)) return configuredKey.Trim();
        var environmentKey = Environment.GetEnvironmentVariable("OPENHIKEMAP_ORS_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey)) return environmentKey.Trim();
        return DefaultApiKey;
    }
}
