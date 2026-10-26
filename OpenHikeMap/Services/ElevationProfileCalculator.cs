using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public sealed record ElevationSample(double DistanceMeters, double ElevationMeters, double SlopePercent);
public sealed record ElevationProfileData(double DistanceMeters, double GainMeters, double LossMeters, double AverageSlope, List<ElevationSample> Samples)
{
    public bool HasElevation => Samples.Count > 1;
}

public static class ElevationProfileCalculator
{
    public static ElevationProfileData Calculate(IReadOnlyList<RoutePathPoint> path)
    {
        var distances = new double[path.Count];
        for (var i = 1; i < path.Count; i++) distances[i] = distances[i - 1] + Distance(path[i - 1], path[i]);
        var total = distances.Length > 0 ? distances[^1] : 0;
        if (total < .01 || path.Any(p => p.ElevationMeters is not { } e || !double.IsFinite(e))) return new(total, 0, 0, 0, []);

        // Sample by travelled distance, preserving switchbacks and the exact last point.
        var interval = Math.Max(10, total / 4000);
        var positions = new List<double> { 0 };
        for (var d = interval; d < total; d += interval) positions.Add(d);
        positions.Add(total);
        var heights = new double[positions.Count];
        var segment = 1;
        for (var i = 0; i < positions.Count; i++)
        {
            while (segment < path.Count - 1 && distances[segment] < positions[i]) segment++;
            var span = distances[segment] - distances[segment - 1];
            var ratio = span > 0 ? (positions[i] - distances[segment - 1]) / span : 0;
            heights[i] = path[segment - 1].ElevationMeters!.Value + ratio * (path[segment].ElevationMeters!.Value - path[segment - 1].ElevationMeters!.Value);
        }
        heights[0] = path[0].ElevationMeters!.Value;
        heights[^1] = path[^1].ElevationMeters!.Value;
        var smooth = heights.ToArray();
        for (var i = 1; i < heights.Length - 1; i++)
        {
            double sum = 0, weight = 0;
            for (var j = Math.Max(0, i - 2); j <= Math.Min(heights.Length - 1, i + 2); j++)
            {
                var w = Math.Abs(j - i) switch { 0 => 4, 1 => 2, _ => 1 };
                sum += heights[j] * w; weight += w;
            }
            smooth[i] = sum / weight;
        }
        double gain = 0, loss = 0, average = 0;
        var samples = new List<ElevationSample>(positions.Count);
        for (var i = 0; i < positions.Count; i++)
        {
            if (i > 0)
            {
                var delta = smooth[i] - smooth[i - 1];
                gain += Math.Max(0, delta); loss += Math.Max(0, -delta);
                average += Math.Abs(Slope(positions, smooth, i, 100)) * (positions[i] - positions[i - 1]);
            }
            samples.Add(new(positions[i], smooth[i], Slope(positions, smooth, i, 20)));
        }
        return new(total, gain, loss, average / total, samples);
    }

    private static double Slope(List<double> distances, double[] heights, int center, double window)
    {
        var left = center; var right = center;
        while (left > 0 && distances[left] > distances[center] - window / 2) left--;
        while (right < distances.Count - 1 && distances[right] < distances[center] + window / 2) right++;
        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        for (var i = left; i <= right; i++)
        {
            var x = distances[i] - distances[left];
            sumX += x; sumY += heights[i]; sumXY += x * heights[i]; sumXX += x * x;
        }
        var count = right - left + 1;
        var denominator = count * sumXX - sumX * sumX;
        return denominator > 1e-8 ? (count * sumXY - sumX * sumY) / denominator * 100 : 0;
    }

    private static double Distance(RoutePathPoint a, RoutePathPoint b)
    {
        const double rad = Math.PI / 180;
        var h = Math.Pow(Math.Sin((b.Latitude - a.Latitude) * rad / 2), 2) + Math.Cos(a.Latitude * rad) * Math.Cos(b.Latitude * rad) * Math.Pow(Math.Sin((b.Longitude - a.Longitude) * rad / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1)));
    }
}
