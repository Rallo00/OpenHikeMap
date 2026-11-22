namespace OpenHikeMap.Models;

public sealed record EumetnetAlertArea(string Id, string Country, string Name, int Severity, string Event,
    string Details, string Sender, DateTimeOffset Issued, DateTimeOffset Onset, DateTimeOffset? Expires,
    List<List<List<RoutePathPoint>>> Polygons);

public sealed record EumetnetAlertSnapshot(List<EumetnetAlertArea> Areas, int FailedCountries, int UnmappedAreas,
    int RequestedCountries, bool GeometryCatalogStale = false)
{
    public bool IsComplete => FailedCountries == 0 && UnmappedAreas == 0 && !GeometryCatalogStale;
}

public sealed record EumetnetBounds(double West, double South, double East, double North)
{
    public bool Intersects(EumetnetBounds other) => South <= other.North && North >= other.South &&
        LongitudeRanges().Any(a => other.LongitudeRanges().Any(b => a.West <= b.East && a.East >= b.West));

    private IEnumerable<(double West, double East)> LongitudeRanges() => West <= East
        ? [(West, East)] : [(-180, East), (West, 180)];

    public static EumetnetBounds FromPoints(IEnumerable<RoutePathPoint> points)
    {
        var west = double.PositiveInfinity; var south = double.PositiveInfinity;
        var east = double.NegativeInfinity; var north = double.NegativeInfinity;
        foreach (var point in points)
        {
            west = Math.Min(west, point.Longitude); east = Math.Max(east, point.Longitude);
            south = Math.Min(south, point.Latitude); north = Math.Max(north, point.Latitude);
        }
        if (!double.IsFinite(west)) throw new ArgumentException("Cannot bound an empty route.", nameof(points));
        return new(west, south, east, north);
    }
}
