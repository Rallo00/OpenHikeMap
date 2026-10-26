using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public static class RouteMidpoint
{
    public static RoutePathPoint? Find(IReadOnlyList<RoutePathPoint> path)
    {
        if (path.Count == 0) return null;
        var distances = new double[path.Count];
        for (var i = 1; i < path.Count; i++) distances[i] = distances[i - 1] + Distance(path[i - 1], path[i]);
        var target = distances[^1] / 2;
        for (var i = 1; i < path.Count; i++)
        {
            if (distances[i] < target) continue;
            var fraction = distances[i] > distances[i - 1] ? (target - distances[i - 1]) / (distances[i] - distances[i - 1]) : 0;
            return new RoutePathPoint { Latitude = path[i - 1].Latitude + fraction * (path[i].Latitude - path[i - 1].Latitude), Longitude = path[i - 1].Longitude + fraction * (path[i].Longitude - path[i - 1].Longitude) };
        }
        return new RoutePathPoint { Latitude = path[0].Latitude, Longitude = path[0].Longitude };
    }

    private static double Distance(RoutePathPoint a, RoutePathPoint b)
    {
        var rad = Math.PI / 180;
        var h = Math.Pow(Math.Sin((b.Latitude - a.Latitude) * rad / 2), 2) + Math.Cos(a.Latitude * rad) * Math.Cos(b.Latitude * rad) * Math.Pow(Math.Sin((b.Longitude - a.Longitude) * rad / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1)));
    }
}
