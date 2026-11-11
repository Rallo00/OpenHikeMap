using System.Globalization;
using System.Text.Json;

namespace OpenHikeMap.Services;

public sealed record MapWebcam(string Id, string Name, double Latitude, double Longitude, string Url);
public sealed record WebcamArea(IReadOnlyList<MapWebcam> Cameras, int Total, bool Truncated);

public sealed class OpenCamService(HttpClient client) : IDisposable
{
    private const int MaxCameras = 2000, MaxResponseBytes = 8 * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset Expires, WebcamArea Area)> _cache = [];

    public static bool ValidBounds(double west, double south, double east, double north) =>
        double.IsFinite(west) && double.IsFinite(south) && double.IsFinite(east) && double.IsFinite(north)
        && west >= -180 && east <= 180 && south >= -90 && north <= 90 && west < east && south < north;

    public async Task<WebcamArea> GetAsync(double west, double south, double east, double north, CancellationToken token = default)
    {
        if (!ValidBounds(west, south, east, north)) throw new ArgumentOutOfRangeException(nameof(west));
        var bounds = string.Join(',', new[] { west, south, east, north }.Select(n => n.ToString("F5", CultureInfo.InvariantCulture)));
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(bounds, out var saved) && saved.Expires > DateTimeOffset.UtcNow) return saved.Area;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(15)); token = timeout.Token;
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://opencam.live/api/cctv/within?bbox=" + bounds);
            request.Headers.UserAgent.ParseAdd(ApplicationInfo.UserAgent); request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxResponseBytes) throw new InvalidDataException("OpenCam catalogue is too large.");
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var data = new MemoryStream(); var buffer = new byte[16384]; int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            { if (data.Length + count > MaxResponseBytes) throw new InvalidDataException("OpenCam catalogue is too large."); data.Write(buffer, 0, count); }
            using var json = JsonDocument.Parse(data.GetBuffer().AsMemory(0, (int)data.Length));
            var area = Parse(json.RootElement, west, south, east, north);
            foreach (var key in _cache.Where(p => p.Value.Expires <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) _cache.Remove(key);
            if (_cache.Count >= 16) _cache.Remove(_cache.MinBy(p => p.Value.Expires).Key);
            _cache[bounds] = (DateTimeOffset.UtcNow.AddMinutes(10), area);
            return area;
        }
        finally { _gate.Release(); }
    }

    public static WebcamArea Parse(JsonElement root, double west, double south, double east, double north)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("cameras", out var entries) || entries.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid OpenCam catalogue.");
        var cameras = new List<MapWebcam>(); var seen = new HashSet<string>(StringComparer.Ordinal); var available = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var identifier) || identifier.ValueKind != JsonValueKind.String || identifier.GetString() is not { } id || !ValidId(id)
                || !entry.TryGetProperty("lat", out var lat) || lat.ValueKind != JsonValueKind.Number || !lat.TryGetDouble(out var latitude) || !double.IsFinite(latitude)
                || !entry.TryGetProperty("lng", out var lng) || lng.ValueKind != JsonValueKind.Number || !lng.TryGetDouble(out var longitude) || !double.IsFinite(longitude)
                || latitude < south || latitude > north || longitude < west || longitude > east || !seen.Add(id)) continue;
            available++;
            if (cameras.Count == MaxCameras) continue;
            var share = entry.TryGetProperty("share", out var link) && link.ValueKind == JsonValueKind.String && link.GetString() is { } value && ValidId(value) ? value : id;
            var name = entry.TryGetProperty("name", out var title) && title.ValueKind == JsonValueKind.String ? title.GetString() : null;
            var url = FormattableString.Invariant($"https://opencam.live/globe?cam={share}&lat={latitude:F6}&lng={longitude:F6}&zoom=14");
            cameras.Add(new(id, string.IsNullOrWhiteSpace(name) ? "OpenCam" : name[..Math.Min(name.Length, 300)], latitude, longitude, url));
        }
        var total = root.TryGetProperty("total", out var totalValue) && totalValue.ValueKind == JsonValueKind.Number && totalValue.TryGetInt32(out var number) ? Math.Max(available, number) : available;
        var truncated = available > MaxCameras || total > available || root.TryGetProperty("truncated", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new(cameras, total, truncated);
    }
    private static bool ValidId(string id) => id.Length is > 0 and <= 200 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public void Dispose() => _gate.Dispose();
}
