using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public static class EumetnetAlertSelection
{
    public static List<EumetnetAlertArea> ForRoute(IEnumerable<EumetnetAlertArea> areas,
        IReadOnlyList<RoutePathPoint> path, RoutePathPoint? weatherPoint, DateTimeOffset now)
    {
        if (path.Count == 0 && weatherPoint is null) return [];
        var routeBounds = path.Count > 0 ? EumetnetBounds.FromPoints(path) : null;
        // A saved/manual weather point must not add unrelated regions to an existing trail.
        var referencePoint = path.Count == 0 ? weatherPoint : null;
        return areas.Where(area => (area.Expires is null || area.Expires > now) &&
            area.Polygons.Any(polygon => polygon.Count > 0 && MatchesBounds(EumetnetBounds.FromPoints(polygon[0]))) &&
            (AlertGeometry.Intersects(area, path) || referencePoint is not null && AlertGeometry.Intersects(area, [referencePoint]))).ToList();

        bool MatchesBounds(EumetnetBounds bounds) => routeBounds is not null && bounds.Intersects(routeBounds) ||
            referencePoint is not null && referencePoint.Longitude >= bounds.West && referencePoint.Longitude <= bounds.East &&
            referencePoint.Latitude >= bounds.South && referencePoint.Latitude <= bounds.North;
    }
}
