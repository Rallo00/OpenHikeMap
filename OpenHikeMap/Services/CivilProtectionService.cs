using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed record CivilAlertArea(string Name, int Severity, string Details, List<List<List<RoutePathPoint>>> Polygons);

public sealed class CivilProtectionService(HttpClient http)
{
    public const string BulletinUrl = "https://mappe.protezionecivile.gov.it/it/mappe-rischi/bollettino-di-criticita/index.html";
    private const string RegionsUrl = "https://raw.githubusercontent.com/openpolis/geojson-italy/master/geojson/limits_IT_regions.geojson";
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private List<CivilAlertArea>? _cache;
    private DateTimeOffset _expires;
    private string? _regions;

    public async Task<List<CivilAlertArea>> GetAreasAsync(CancellationToken token = default)
    {
        await _refresh.WaitAsync(token);
        try
        {
            if (_cache is not null && _expires > DateTimeOffset.UtcNow) return _cache;
            var bulletin = await http.GetStringAsync(BulletinUrl, token);
            _regions ??= await http.GetStringAsync(RegionsUrl, token);
            _cache = Parse(bulletin, _regions);
            _expires = DateTimeOffset.UtcNow.AddMinutes(10);
            return _cache;
        }
        finally { _refresh.Release(); }
    }

    public static List<CivilAlertArea> Parse(string html, string regionsJson)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", "\n"));
        var alerts = new Dictionary<string, (int Severity, List<string> Details)>(StringComparer.OrdinalIgnoreCase);
        string? header = null;
        string day = "";
        var active = false;
        var foundForecast = false;
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
        {
            if (line.StartsWith("Per la giornata di oggi", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Per la giornata di domani", StringComparison.OrdinalIgnoreCase))
            {
                active = foundForecast = true; header = null; day = line; continue;
            }
            if (line.StartsWith("Scarica i dati del bollettino", StringComparison.OrdinalIgnoreCase)) active = false;
            if (!active) continue;
            if (line.Contains("ALLERTA", StringComparison.OrdinalIgnoreCase)) { header = line; continue; }
            if (header is null || !line.Contains(':')) continue;
            var severity = header.Contains("ROSSA", StringComparison.OrdinalIgnoreCase) ? 3 : header.Contains("ARANCIONE", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            if (severity < 2) continue;
            var separator = line.IndexOf(':');
            var region = Normalize(line[..separator]);
            if (string.IsNullOrWhiteSpace(line[(separator + 1)..])) continue;
            if (!alerts.TryGetValue(region, out var alert)) alert = (0, []);
            alert.Details.Add($"{day} — {header}: {line[(separator + 1)..].Trim()}");
            alerts[region] = (Math.Max(severity, alert.Severity), alert.Details);
        }
        if (!foundForecast) throw new InvalidDataException("Il bollettino SNPC-IT non contiene le sezioni delle allerte attese.");
        using var regions = JsonDocument.Parse(regionsJson);
        var result = new List<CivilAlertArea>();
        foreach (var feature in regions.RootElement.GetProperty("features").EnumerateArray())
        {
            var name = feature.GetProperty("properties").GetProperty("reg_name").GetString() ?? "";
            if (!alerts.TryGetValue(Normalize(name), out var alert)) continue;
            var geometry = feature.GetProperty("geometry");
            var coords = geometry.GetProperty("coordinates");
            var polygons = new List<List<List<RoutePathPoint>>>();
            if (geometry.GetProperty("type").GetString() == "Polygon") polygons.Add(ParsePolygon(coords));
            else foreach (var polygon in coords.EnumerateArray()) polygons.Add(ParsePolygon(polygon));
            result.Add(new CivilAlertArea(name, alert.Severity, string.Join(" | ", alert.Details.Distinct()), polygons));
        }
        return result;
    }

    private static List<List<RoutePathPoint>> ParsePolygon(JsonElement polygon) => polygon.EnumerateArray()
        .Select(ring => ring.EnumerateArray().Select(p => new RoutePathPoint { Longitude = p[0].GetDouble(), Latitude = p[1].GetDouble() }).ToList()).ToList();

    private static string Normalize(string value) => value.Trim().Replace('’', '\'')
        .Replace("Provincia Autonoma di Bolzano", "Trentino-Alto Adige", StringComparison.OrdinalIgnoreCase)
        .Replace("Provincia Autonoma di Trento", "Trentino-Alto Adige", StringComparison.OrdinalIgnoreCase)
        .Split('/')[0].Replace("-", "").Replace(" ", "").ToUpperInvariant();

    public static bool Intersects(CivilAlertArea area, IReadOnlyList<RoutePathPoint> path)
    {
        foreach (var rings in area.Polygons)
        {
            if (rings.Count == 0 || rings[0].Count < 3) continue;
            if (path.Any(p => Contains(rings[0], p) && !rings.Skip(1).Any(hole => Contains(hole, p)))) return true;
            for (var i = 1; i < path.Count; i++)
                for (var j = 1; j < rings[0].Count; j++)
                    if (Crosses(path[i - 1], path[i], rings[0][j - 1], rings[0][j])) return true;
        }
        return false;
    }

    private static bool Contains(List<RoutePathPoint> ring, RoutePathPoint point)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            if ((ring[i].Latitude > point.Latitude) != (ring[j].Latitude > point.Latitude) &&
                point.Longitude < (ring[j].Longitude - ring[i].Longitude) * (point.Latitude - ring[i].Latitude) / (ring[j].Latitude - ring[i].Latitude) + ring[i].Longitude) inside = !inside;
        return inside;
    }

    private static bool Crosses(RoutePathPoint a, RoutePathPoint b, RoutePathPoint c, RoutePathPoint d)
    {
        if (Math.Max(a.Longitude, b.Longitude) < Math.Min(c.Longitude, d.Longitude) || Math.Min(a.Longitude, b.Longitude) > Math.Max(c.Longitude, d.Longitude) ||
            Math.Max(a.Latitude, b.Latitude) < Math.Min(c.Latitude, d.Latitude) || Math.Min(a.Latitude, b.Latitude) > Math.Max(c.Latitude, d.Latitude)) return false;
        static double Side(RoutePathPoint p, RoutePathPoint q, RoutePathPoint r) => (q.Longitude - p.Longitude) * (r.Latitude - p.Latitude) - (q.Latitude - p.Latitude) * (r.Longitude - p.Longitude);
        return Side(a, b, c) * Side(a, b, d) <= 0 && Side(c, d, a) * Side(c, d, b) <= 0;
    }
}
