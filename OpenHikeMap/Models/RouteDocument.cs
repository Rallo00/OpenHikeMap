namespace OpenHikeMap.Models;

public sealed class RouteDocument
{
    public string Format { get; set; } = "OpenHikeMap.Route";
    public int Version { get; set; } = 1;
    public List<RoutePoint> RoutePoints { get; set; } = [];
    public List<RoutePathPoint> RouteGeometry { get; set; } = [];
    public string RouteKind { get; set; } = "Bozza diretta";
    public double? WeatherReferenceLatitude { get; set; }
    public double? WeatherReferenceLongitude { get; set; }
}
