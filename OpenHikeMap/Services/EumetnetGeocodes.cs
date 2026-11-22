using System.Text.Json;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed class EumetnetGeocodes
{
    private readonly Dictionary<string, List<List<List<RoutePathPoint>>>> _shapes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public EumetnetGeocodes(string geoJson, string aliasesCsv)
    {
        using var document = JsonDocument.Parse(geoJson);
        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var properties = feature.GetProperty("properties");
            var code = properties.GetProperty("code").GetString();
            var type = properties.TryGetProperty("type", out var scheme) ? scheme.GetString() : "EMMA_ID";
            if (string.IsNullOrEmpty(code)) continue;
            _shapes[Key(type ?? "EMMA_ID", code)] = AlertGeometry.FromGeoJson(feature.GetProperty("geometry"));
        }
        foreach (var line in aliasesCsv.Split('\n').Skip(1))
        {
            var columns = line.Trim().Split(',').Select(v => v.Trim(' ', '\r', '"')).ToArray();
            if (columns.Length != 3) continue;
            var key = Key(columns[2], columns[1]);
            if (!_aliases.TryGetValue(key, out var codes)) _aliases[key] = codes = [];
            codes.Add(Key("EMMA_ID", columns[0]));
        }
        if (_shapes.Count == 0) throw new InvalidDataException("Empty EUMETNET geocode catalog.");
    }

    private static string Key(string type, string code) => type.ToUpperInvariant().Replace("WARNCELLID", "WARNCELL") + ":" + code;
    public List<List<List<RoutePathPoint>>>? Resolve(string type, string code)
    {
        var key = Key(type, code);
        if (_shapes.TryGetValue(key, out var direct)) return direct;
        if (!_aliases.TryGetValue(key, out var codes)) return null;
        var result = codes.Where(_shapes.ContainsKey).SelectMany(c => _shapes[c]).ToList();
        return result.Count > 0 ? result : null;
    }
}
