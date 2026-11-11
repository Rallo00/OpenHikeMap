namespace OpenHikeMap.Services;

internal static class OpenRouteServiceCredentials
{
    internal static string Load(IConfiguration configuration)
    {
        var configuredKey = configuration["OpenRouteService:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey)) return configuredKey.Trim();
        var environmentKey = Environment.GetEnvironmentVariable("OPENHIKEMAP_ORS_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey)) return environmentKey.Trim();
        var keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMap", "ors.key");
        return File.Exists(keyPath) ? File.ReadAllText(keyPath).Trim() : string.Empty;
    }
}
