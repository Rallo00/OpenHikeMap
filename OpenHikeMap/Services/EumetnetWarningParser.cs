using System.Globalization;
using System.Text.Json;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed record EumetnetParsedWarnings(List<EumetnetAlertArea> Areas, int UnmappedAreas);

public static class EumetnetWarningParser
{
    public static EumetnetParsedWarnings Parse(string json, string country, string language,
        EumetnetGeocodes? geocodes, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid EUMETNET warning response.");
        var alerts = warnings.EnumerateArray().Select(w => w.GetProperty("alert")).ToList();
        var superseded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alert in alerts.Where(IsPublicActual))
        {
            if (Text(alert, "msgType") is not ("Update" or "Cancel")) continue;
            if (!Date(alert, "sent", out var sent) || sent > now) continue;
            foreach (var reference in Text(alert, "references").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = reference.Split(',');
                if (parts.Length == 3) superseded.Add(parts[0] + "|" + parts[1]);
            }
        }
        var result = new List<EumetnetAlertArea>(); var unmapped = 0;
        foreach (var alert in alerts)
        {
            if (!IsPublicActual(alert) || Text(alert, "msgType") is not ("Alert" or "Update") ||
                superseded.Contains(Text(alert, "sender") + "|" + Text(alert, "identifier"))) continue;
            if (!Date(alert, "sent", out var issued) || issued > now || !alert.TryGetProperty("info", out var infos) || infos.ValueKind != JsonValueKind.Array)
            { unmapped++; continue; }
            var candidates = infos.EnumerateArray().ToList();
            if (candidates.Count == 0) continue;
            var best = candidates.Min(info => LanguageRank(Text(info, "language"), language));
            var selectedLanguage = Text(candidates.First(info => LanguageRank(Text(info, "language"), language) == best), "language");
            foreach (var info in candidates.Where(info => Text(info, "language").Equals(selectedLanguage, StringComparison.OrdinalIgnoreCase)))
            {
                var severity = Awareness(info);
                if (severity == 1) continue;
                if (Array(info, "responseType").Any(v => v.GetString() == "AllClear")) continue;
                // CAP 1.2 permits an omitted expiry: such messages remain in force until updated/canceled.
                DateTimeOffset? expires = Date(info, "expires", out var end) ? end : null;
                if (expires is null && !string.IsNullOrEmpty(Text(info, "expires"))) { unmapped++; continue; }
                if (expires is { } expiration && expiration <= now) continue;
                if (severity == 0) { unmapped++; continue; }
                var onset = Date(info, "onset", out var start) ? start : Date(info, "effective", out start) ? start : issued;
                if (expires is { } until && onset >= until) { unmapped++; continue; }
                var details = string.Join("\n\n", new[] { Text(info, "headline"), Text(info, "description"), Text(info, "instruction") }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
                var areas = Array(info, "area").ToList();
                if (areas.Count == 0) { unmapped++; continue; }
                foreach (var area in areas)
                {
                    var polygons = new List<List<List<RoutePathPoint>>>();
                    try
                    {
                        foreach (var polygon in Array(area, "polygon")) polygons.Add([AlertGeometry.CapPolygon(polygon.GetString() ?? "")]);
                        foreach (var circle in Array(area, "circle")) polygons.Add([AlertGeometry.CapCircle(circle.GetString() ?? "")]);
                        // CAP geocodes supplement warnings without explicit geometry; do not expand polygon warnings to whole administrative regions.
                        if (polygons.Count == 0)
                            foreach (var code in Array(area, "geocode"))
                                if (geocodes?.Resolve(Text(code, "valueName"), Text(code, "value")) is { } shape) polygons.AddRange(shape);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException or InvalidOperationException)
                    { unmapped++; continue; }
                    if (polygons.Count == 0) { unmapped++; continue; }
                    result.Add(new(Text(alert, "identifier"), country, Text(area, "areaDesc"), severity, Text(info, "event"), details,
                        Text(info, "senderName") is { Length: > 0 } sender ? sender : Text(alert, "sender"), issued, onset, expires, polygons));
                }
            }
        }
        return new(result.DistinctBy(a => (a.Id, a.Name, a.Event, a.Onset)).ToList(), unmapped);
    }

    private static bool IsPublicActual(JsonElement alert) => Text(alert, "status") == "Actual" && Text(alert, "scope") == "Public";
    private static int LanguageRank(string candidate, string requested)
    {
        if (candidate.Equals(requested, StringComparison.OrdinalIgnoreCase)) return 0;
        if (candidate.Split('-')[0].Equals(requested.Split('-')[0], StringComparison.OrdinalIgnoreCase)) return 1;
        return candidate.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 2 : 3;
    }
    private static int Awareness(JsonElement info)
    {
        var level = Array(info, "parameter").FirstOrDefault(p => Text(p, "valueName") == "awareness_level");
        if (level.ValueKind == JsonValueKind.Object && int.TryParse(Text(level, "value").Split(';')[0].Trim(), out var number) && number is >= 1 and <= 4)
            return number;
        return Text(info, "severity") switch { "Extreme" => 4, "Severe" => 3, "Moderate" => 2, "Minor" => 1, _ => 0 };
    }
    private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static bool Date(JsonElement element, string name, out DateTimeOffset value) => DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    private static IEnumerable<JsonElement> Array(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var values)) return [];
        return values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().ToArray() : [values];
    }
}
