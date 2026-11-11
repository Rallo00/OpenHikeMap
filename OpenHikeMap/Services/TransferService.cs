using System.Text.Json;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public enum ImportKind { Route, Backup }
public sealed record ImportPlan(WebSession Data, ImportKind Kind);
public sealed class ImportFormatException(string key) : Exception(key);

public static class TransferService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static RouteDocument BuildRoute(WebSession data) => new()
    {
        RoutePoints = data.RoutePoints, RouteGeometry = data.RouteGeometry, RouteKind = data.RouteKind,
        WeatherReferenceLatitude = data.Settings.WeatherReferenceLatitude, WeatherReferenceLongitude = data.Settings.WeatherReferenceLongitude
    };

    public static BackupArchive BuildBackup(WebSession data)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(data.Settings, Json), Json)!;
        settings.WeatherReferenceLatitude = null; settings.WeatherReferenceLongitude = null;
        return new() { Settings = settings, Folders = data.Folders, Places = data.Places };
    }

    public static ImportPlan Parse(string filename, Stream input, WebSession current, GpxService gpx)
    {
        var candidate = JsonSerializer.Deserialize<WebSession>(JsonSerializer.Serialize(current, Json), Json)!;
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        var kind = ImportKind.Route;
        if (extension is ".gpx" or ".kml" or ".kmz")
        {
            var route = extension == ".gpx" ? gpx.ImportGpx(input) : RouteKmlService.Import(input, extension == ".kmz");
            if (route.RoutePoints.Count > 1) { route.RoutePoints[0].PointType = "Start"; route.RoutePoints[^1].PointType = "End"; }
            candidate.RoutePoints = route.RoutePoints; candidate.RouteGeometry = route.RouteGeometry;
            candidate.RouteKind = route.RouteGeometry.Count > 0 ? $"Traccia {extension[1..].ToUpperInvariant()} importata" : "Bozza diretta";
            candidate.Settings.WeatherReferenceLatitude = null; candidate.Settings.WeatherReferenceLongitude = null;
        }
        else if (extension is ".ohk" or ".json")
        {
            using var document = JsonDocument.Parse(input);
            var root = document.RootElement;
            if (extension == ".json")
            {
                if (!Has(root, "Settings") || !Has(root, "Folders") || !Has(root, "Places")) throw new ImportFormatException("ImportUnsupported");
                var backup = root.Deserialize<BackupArchive>(Json) ?? throw new ImportFormatException("ImportUnsupported");
                new WebSession { Settings = backup.Settings, Folders = backup.Folders, Places = backup.Places }.Validate();
                backup.Settings.WeatherReferenceLatitude = current.Settings.WeatherReferenceLatitude;
                backup.Settings.WeatherReferenceLongitude = current.Settings.WeatherReferenceLongitude;
                candidate.Settings = backup.Settings; candidate.Folders = backup.Folders; candidate.Places = backup.Places;
                kind = ImportKind.Backup;
            }
            else
            {
                if (!Has(root, "RoutePoints") || !Has(root, "RouteGeometry")) throw new ImportFormatException("ImportUnsupported");
                var route = root.Deserialize<RouteDocument>(Json) ?? throw new ImportFormatException("ImportUnsupported");
                if (route.Format != "OpenHikeMap.Route" || route.Version != 1) throw new ImportFormatException("ImportUnsupported");
                // Older OHK sessions included the entire library; only their route is restored here.
                if (TryProperty(root, "Settings", out var oldSettings) && oldSettings.ValueKind == JsonValueKind.Object)
                {
                    var settings = oldSettings.Deserialize<AppSettings>(Json)!;
                    route.WeatherReferenceLatitude = settings.WeatherReferenceLatitude;
                    route.WeatherReferenceLongitude = settings.WeatherReferenceLongitude;
                }
                candidate.RoutePoints = route.RoutePoints; candidate.RouteGeometry = route.RouteGeometry;
                candidate.RouteKind = string.IsNullOrWhiteSpace(route.RouteKind) ? "Bozza diretta" : route.RouteKind;
                candidate.Settings.WeatherReferenceLatitude = route.WeatherReferenceLatitude;
                candidate.Settings.WeatherReferenceLongitude = route.WeatherReferenceLongitude;
            }
        }
        else throw new ImportFormatException("ImportUnsupported");
        candidate.Validate();
        if (kind == ImportKind.Route && candidate.RoutePoints.Count == 0 && candidate.RouteGeometry.Count == 0) throw new ImportFormatException("ImportEmptyRoute");
        return new(candidate, kind);
    }

    private static bool Has(JsonElement element, string name) => TryProperty(element, name, out _);
    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default; return false;
    }
}
