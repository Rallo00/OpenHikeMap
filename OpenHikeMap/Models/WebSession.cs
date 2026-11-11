namespace OpenHikeMap.Models;

public sealed class WebSession
{
    public AppSettings Settings { get; set; } = new();
    public List<SavedFolder> Folders { get; set; } = [];
    public List<SavedPlace> Places { get; set; } = [];
    public List<RoutePoint> RoutePoints { get; set; } = [];
    public List<RoutePathPoint> RouteGeometry { get; set; } = [];
    public string RouteKind { get; set; } = "Bozza diretta";

    public static bool CoordinatesValid(double lat, double lon) => double.IsFinite(lat) && double.IsFinite(lon) && lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

    public void Validate()
    {
        if (Settings is null || Folders is null || Places is null || RoutePoints is null || RouteGeometry is null || RouteKind is null)
            throw new InvalidDataException("Il file contiene raccolte o impostazioni nulle.");
        if (Places.Count > 10_000 || Folders.Count > 1_000 || RoutePoints.Count > 5_000 || RouteGeometry.Count > 100_000)
            throw new InvalidDataException("Troppi elementi: massimo 10.000 pin, 1.000 cartelle, 5.000 tappe e 100.000 punti traccia.");
        if (!CoordinatesValid(Settings.DefaultLatitude, Settings.DefaultLongitude) || Settings.DefaultZoom is < 2 or > 19 ||
            !double.IsFinite(Settings.TravelSpeed) || Settings.TravelSpeed is < 0.1 or > 30)
            throw new InvalidDataException("Controlla centro, zoom e velocità nelle impostazioni.");
        if (Settings.WeatherReferenceLatitude.HasValue != Settings.WeatherReferenceLongitude.HasValue ||
            (Settings.WeatherReferenceLatitude is { } lat && !CoordinatesValid(lat, Settings.WeatherReferenceLongitude!.Value)))
            throw new InvalidDataException("Coordinate del riferimento meteo non valide.");
        if (Places.Any(p => p is null || p.Name is null || !CoordinatesValid(p.Latitude, p.Longitude)) ||
            RoutePoints.Any(p => p is null || p.Name is null || !CoordinatesValid(p.Latitude, p.Longitude)) ||
            RouteGeometry.Any(p => p is null || !CoordinatesValid(p.Latitude, p.Longitude) || (p.ElevationMeters is { } e && !double.IsFinite(e))) ||
            Folders.Any(f => f is null || f.Name is null))
            throw new InvalidDataException("Il file contiene punti o coordinate non validi.");
        if (Folders.Select(f => f.Id).Distinct().Count() != Folders.Count || Places.Select(p => p.Id).Distinct().Count() != Places.Count)
            throw new InvalidDataException("Identificativi duplicati nel file.");
        var folders = Folders.ToDictionary(f => f.Id);
        foreach (var folder in Folders)
        {
            var seen = new HashSet<Guid> { folder.Id };
            var parent = folder.ParentId;
            while (parent is { } id && folders.TryGetValue(id, out var ancestor))
            {
                if (!seen.Add(id)) throw new InvalidDataException("La struttura delle cartelle contiene un ciclo.");
                parent = ancestor.ParentId;
            }
        }
    }
}
