using System.Globalization;
using System.Text.Json;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public static class AlertGeometry
{
    public static List<List<List<RoutePathPoint>>> FromGeoJson(JsonElement geometry)
    {
        var coordinates = geometry.GetProperty("coordinates");
        return geometry.GetProperty("type").GetString() switch
        {
            "Polygon" => [Polygon(coordinates)],
            "MultiPolygon" => coordinates.EnumerateArray().Select(Polygon).ToList(),
            _ => throw new InvalidDataException("Unsupported warning geometry.")
        };
    }

    private static List<List<RoutePathPoint>> Polygon(JsonElement polygon) => polygon.EnumerateArray().Select(ring =>
        ValidateRing(ring.EnumerateArray().Select(point => new RoutePathPoint
        { Longitude = point[0].GetDouble(), Latitude = point[1].GetDouble() }).ToList())).ToList();

    public static List<RoutePathPoint> CapPolygon(string value) => ValidateRing(value.Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries).Select(pair =>
        {
            var coordinates = pair.Split(',');
            if (coordinates.Length != 2) throw new InvalidDataException("Invalid CAP coordinate.");
            return new RoutePathPoint { Latitude = double.Parse(coordinates[0], CultureInfo.InvariantCulture),
                Longitude = double.Parse(coordinates[1], CultureInfo.InvariantCulture) };
        }).ToList());

    public static List<RoutePathPoint> CapCircle(string value)
    {
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) throw new InvalidDataException("Invalid CAP circle.");
        var center = parts[0].Split(',');
        if (center.Length != 2) throw new InvalidDataException("Invalid CAP circle center.");
        var lat = double.Parse(center[0], CultureInfo.InvariantCulture);
        var lon = double.Parse(center[1], CultureInfo.InvariantCulture);
        var radius = double.Parse(parts[1], CultureInfo.InvariantCulture);
        if (!WebSession.CoordinatesValid(lat, lon) || !double.IsFinite(radius) || radius <= 0 || radius > 20000)
            throw new InvalidDataException("Invalid CAP circle radius.");
        var result = new List<RoutePathPoint>();
        var phi = lat * Math.PI / 180; var lambda = lon * Math.PI / 180; var distance = radius / 6371.0088;
        for (var i = 0; i <= 72; i++)
        {
            var bearing = i * 2 * Math.PI / 72;
            var latitude = Math.Asin(Math.Sin(phi) * Math.Cos(distance) + Math.Cos(phi) * Math.Sin(distance) * Math.Cos(bearing));
            var longitude = lambda + Math.Atan2(Math.Sin(bearing) * Math.Sin(distance) * Math.Cos(phi),
                Math.Cos(distance) - Math.Sin(phi) * Math.Sin(latitude));
            result.Add(new() { Latitude = latitude * 180 / Math.PI, Longitude = (longitude * 180 / Math.PI + 540) % 360 - 180 });
        }
        return result;
    }

    private static List<RoutePathPoint> ValidateRing(List<RoutePathPoint> ring)
    {
        if (ring.Count < 3 || ring.Any(p => !WebSession.CoordinatesValid(p.Latitude, p.Longitude)))
            throw new InvalidDataException("Invalid warning polygon.");
        if (ring[0].Latitude != ring[^1].Latitude || ring[0].Longitude != ring[^1].Longitude) ring.Add(ring[0]);
        return ring;
    }

    public static bool Intersects(EumetnetAlertArea area, IReadOnlyList<RoutePathPoint> path)
    {
        if (path.Count == 0) return false;
        var routeBounds = EumetnetBounds.FromPoints(path);
        foreach (var rings in area.Polygons)
        {
            if (rings.Count == 0) continue;
            var bounds = EumetnetBounds.FromPoints(rings[0]);
            if (!bounds.Intersects(routeBounds)) continue;
            if (path.Any(p => p.Longitude >= bounds.West && p.Longitude <= bounds.East && p.Latitude >= bounds.South && p.Latitude <= bounds.North &&
                (rings.Any(ring => OnBoundary(ring, p)) || Contains(rings[0], p) && !rings.Skip(1).Any(hole => Contains(hole, p))))) return true;
            // Check every boundary, including holes and implicitly closed rings.
            for (var i = 1; i < path.Count; i++)
            {
                if (Math.Max(path[i - 1].Longitude, path[i].Longitude) < bounds.West || Math.Min(path[i - 1].Longitude, path[i].Longitude) > bounds.East ||
                    Math.Max(path[i - 1].Latitude, path[i].Latitude) < bounds.South || Math.Min(path[i - 1].Latitude, path[i].Latitude) > bounds.North) continue;
                foreach (var ring in rings)
                    for (var j = 0; j < ring.Count; j++)
                        if (Crosses(path[i - 1], path[i], ring[j], ring[(j + 1) % ring.Count])) return true;
            }
        }
        return false;
    }

    private static bool OnBoundary(List<RoutePathPoint> ring, RoutePathPoint point)
    {
        for (var i = 0; i < ring.Count; i++)
            if (Crosses(point, point, ring[i], ring[(i + 1) % ring.Count])) return true;
        return false;
    }

    private static bool Contains(List<RoutePathPoint> ring, RoutePathPoint point)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            if (Crosses(point, point, ring[j], ring[i])) return true;
            if ((ring[i].Latitude > point.Latitude) != (ring[j].Latitude > point.Latitude) &&
                point.Longitude < (ring[j].Longitude - ring[i].Longitude) * (point.Latitude - ring[i].Latitude) /
                (ring[j].Latitude - ring[i].Latitude) + ring[i].Longitude) inside = !inside;
        }
        return inside;
    }

    private static bool Crosses(RoutePathPoint a, RoutePathPoint b, RoutePathPoint c, RoutePathPoint d)
    {
        if (Math.Max(a.Longitude, b.Longitude) < Math.Min(c.Longitude, d.Longitude) || Math.Min(a.Longitude, b.Longitude) > Math.Max(c.Longitude, d.Longitude) ||
            Math.Max(a.Latitude, b.Latitude) < Math.Min(c.Latitude, d.Latitude) || Math.Min(a.Latitude, b.Latitude) > Math.Max(c.Latitude, d.Latitude)) return false;
        static double Side(RoutePathPoint p, RoutePathPoint q, RoutePathPoint r) =>
            (q.Longitude - p.Longitude) * (r.Latitude - p.Latitude) - (q.Latitude - p.Latitude) * (r.Longitude - p.Longitude);
        return Side(a, b, c) * Side(a, b, d) <= 0 && Side(c, d, a) * Side(c, d, b) <= 0;
    }
}
