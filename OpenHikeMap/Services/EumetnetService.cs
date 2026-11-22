using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed class EumetnetService(HttpClient http, ILogger<EumetnetService> logger, string? cacheDirectory = null)
{
    public const string SourceUrl = "https://meteoalarm.org/";
    public const string Attribution = "EUMETNET – Meteoalarm";
    public const string Disclaimer = "Time delays between this application and the www.meteoalarm.org website are possible. For the most up-to-date awareness information as published by the participating National Meteorological and Hydrological Services, please refer to www.meteoalarm.org.";
    private const string HubUrl = "https://cms-visualization.meteoalarm.org/api/v1/content-pages/redistribution-hub?locale=en";
    private const string DocumentsUrl = "https://gitlab.com/meteoalarm-pm-group/documents/-/raw/master/";
    private readonly string _cacheDirectory = cacheDirectory ?? Path.Combine(
        Environment.GetEnvironmentVariable("OPENHIKEMAP_DATA_DIRECTORY") is { Length: > 0 } directory ? directory :
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenHikeMap"), "Eumetnet");
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Downloaded)> _feeds = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _feedLocks = new();
    private readonly SemaphoreSlim _downloads = new(4, 4), _metadataGate = new(1, 1);
    private EumetnetGeocodes? _geocodes;
    private DateTimeOffset _catalogExpires;
    private readonly object _startupLock = new();
    private Task<EumetnetAlertSnapshot>? _startupSnapshot;

    public Task<EumetnetAlertSnapshot> LoadAllOnceAsync(string language, CancellationToken applicationStopping = default)
    {
        lock (_startupLock)
            return _startupSnapshot ??= GetAreasAsync([new(-180, -90, 180, 90)], language, applicationStopping);
    }

    public async Task<EumetnetAlertSnapshot> GetAreasAsync(IReadOnlyList<EumetnetBounds> scopes, string language, CancellationToken token = default)
    {
        var countries = EumetnetCountries.All.Where(c => scopes.Any(scope => c.Bounds.Intersects(scope))).ToArray();
        if (countries.Length == 0) return new([], 0, 0, 0);
        var feeds = await Task.WhenAll(countries.Select(async country =>
        {
            try { return (Country: country, Json: await GetFeedAsync(country.Slug, token)); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException)
            {
                logger.LogWarning("EUMETNET feed {Country} unavailable: {Reason}", country.Code, ex.Message);
                return (Country: country, Json: (string?)null);
            }
        }));
        var parsed = new List<(string Json, string Country, EumetnetParsedWarnings Warnings)>(); var failures = 0;
        foreach (var feed in feeds)
        {
            token.ThrowIfCancellationRequested();
            if (feed.Json is null) { failures++; continue; }
            try { parsed.Add((feed.Json, feed.Country.Code, EumetnetWarningParser.Parse(feed.Json, feed.Country.Code, language, _geocodes, DateTimeOffset.UtcNow))); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
            { failures++; logger.LogWarning("Invalid EUMETNET feed {Country}: {Reason}", feed.Country.Code, ex.Message); }
        }
        if (parsed.Any(p => p.Warnings.UnmappedAreas > 0) || (_geocodes is not null && _catalogExpires <= DateTimeOffset.UtcNow))
        {
            await LoadGeocodesAsync(token);
            if (_geocodes is not null)
                parsed = parsed.Select(p => (p.Json, p.Country, EumetnetWarningParser.Parse(p.Json, p.Country, language, _geocodes, DateTimeOffset.UtcNow))).ToList();
        }
        var areas = parsed.SelectMany(p => p.Warnings.Areas)
            .Where(area => area.Expires is null || area.Expires > DateTimeOffset.UtcNow)
            .Where(area => area.Polygons.Any(polygon => polygon.Count > 0 &&
                scopes.Any(scope => scope.Intersects(EumetnetBounds.FromPoints(polygon[0])))))
            .ToList();
        return new(areas, failures, parsed.Sum(p => p.Warnings.UnmappedAreas), countries.Length,
            _geocodes is not null && _catalogExpires <= DateTimeOffset.UtcNow);
    }

    private async Task<string> GetFeedAsync(string slug, CancellationToken token)
    {
        var gate = _feedLocks.GetOrAdd(slug, _ => new(1, 1));
        await gate.WaitAsync(token);
        try
        {
            if (_feeds.TryGetValue(slug, out var cached) && cached.Downloaded.AddMinutes(3) > DateTimeOffset.UtcNow) return cached.Json;
            await _downloads.WaitAsync(token);
            try
            {
                // JSON CAP bundles are served by the same official endpoint linked from each country's Atom feed.
                var json = await http.GetStringAsync($"https://feeds.meteoalarm.org/api/v1/warnings/feeds-{slug}", token);
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Invalid EUMETNET feed.");
                _feeds[slug] = (json, DateTimeOffset.UtcNow);
                return json;
            }
            finally { _downloads.Release(); }
        }
        finally { gate.Release(); }
    }

    private async Task LoadGeocodesAsync(CancellationToken token)
    {
        await _metadataGate.WaitAsync(token);
        try
        {
            if (_geocodes is not null && _catalogExpires > DateTimeOffset.UtcNow) return;
            var geometryPath = Path.Combine(_cacheDirectory, "geocodes.json");
            var aliasesPath = Path.Combine(_cacheDirectory, "aliases.csv");
            if (_geocodes is null && File.Exists(geometryPath) && File.Exists(aliasesPath))
            {
                try
                {
                    _geocodes = new(await File.ReadAllTextAsync(geometryPath, token), await File.ReadAllTextAsync(aliasesPath, token));
                    _catalogExpires = new DateTimeOffset(File.GetLastWriteTimeUtc(geometryPath)).AddDays(7);
                    if (_catalogExpires > DateTimeOffset.UtcNow) return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
                { logger.LogWarning("Cannot load EUMETNET geometry cache: {Reason}", ex.Message); }
            }
            var geometryUrl = DocumentsUrl + "MeteoAlarm_Geocodes_2026_11_13.json?inline=false";
            var aliasesUrl = DocumentsUrl + "MeteoAlarm_Geocodes-Aliases_2026_11_13.csv?inline=false";
            try
            {
                using var hub = JsonDocument.Parse(await http.GetStringAsync(HubUrl, token));
                var content = hub.RootElement.GetProperty("page").GetProperty("content").GetString() ?? "";
                string? Find(string pattern) => Regex.Matches(content, pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
                    .Select(match => match.Value).OrderDescending(StringComparer.Ordinal).FirstOrDefault();
                geometryUrl = Find(@"https://gitlab\.com/meteoalarm-pm-group/documents/-/raw/master/MeteoAlarm_Geocodes_\d{4}_\d{2}_\d{2}\.json(?:\?inline=false)?") ?? geometryUrl;
                aliasesUrl = Find(@"https://gitlab\.com/meteoalarm-pm-group/documents/-/raw/master/MeteoAlarm_Geocodes-Aliases_\d{4}_\d{2}_\d{2}\.csv(?:\?inline=false)?") ?? aliasesUrl;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            { logger.LogWarning("EUMETNET catalog index unavailable, using the published fallback: {Reason}", ex.Message); }
            var geometry = await http.GetStringAsync(geometryUrl, token);
            var aliases = await http.GetStringAsync(aliasesUrl, token);
            var catalog = new EumetnetGeocodes(geometry, aliases);
            _geocodes = catalog; _catalogExpires = DateTimeOffset.UtcNow.AddDays(7);
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                await SaveCacheAsync(geometryPath, geometry, token);
                await SaveCacheAsync(aliasesPath, aliases, token);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { logger.LogWarning("Cannot persist EUMETNET geometry cache: {Reason}", ex.Message); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or IOException)
        { logger.LogWarning("EUMETNET geocodes unavailable; unlocatable warnings will be reported as incomplete coverage: {Reason}", ex.Message); }
        finally { _metadataGate.Release(); }
    }

    private static async Task SaveCacheAsync(string path, string value, CancellationToken token)
    {
        var temporary = path + ".tmp";
        try { await File.WriteAllTextAsync(temporary, value, token); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
