using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OpenHikeMap.Models;

namespace OpenHikeMap.Services;

public static class RouteKmlService
{
    private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";
    private static readonly XNamespace Gx = "http://www.google.com/kml/ext/2.2";
    private const int MaxKmlBytes = 16 * 1024 * 1024;
    private const string PointTypeKey = "openhikemap:pointType";

    public static string BuildKml(IReadOnlyList<RoutePoint> stops, IReadOnlyList<RoutePathPoint> geometry)
    {
        var name = stops.Count > 1 ? $"{stops[0].Name} → {stops[^1].Name}" : "OpenHikeMap";
        var path = geometry.Count > 0 ? geometry : stops.Select(p => new RoutePathPoint { Latitude = p.Latitude, Longitude = p.Longitude }).ToList();
        var route = new XElement(Kml + "Document", new XElement(Kml + "name", name),
            new XElement(Kml + "Style", new XAttribute("id", "route"),
                new XElement(Kml + "LineStyle", new XElement(Kml + "color", "ff5b7d2e"), new XElement(Kml + "width", 4))));
        if (path.Count > 1)
            route.Add(new XElement(Kml + "Placemark", new XElement(Kml + "name", name), new XElement(Kml + "styleUrl", "#route"),
                new XElement(Kml + "LineString", new XElement(Kml + "tessellate", 1),
                    new XElement(Kml + "altitudeMode", path.Any(p => p.ElevationMeters.HasValue) ? "absolute" : "clampToGround"),
                    new XElement(Kml + "coordinates", string.Join("\n", path.Select(Coordinate))))));
        route.Add(new XElement(Kml + "Folder", new XAttribute("id", "openhikemap-route-stops"), new XElement(Kml + "name", "OpenHikeMap"),
            stops.Select(p => new XElement(Kml + "Placemark", new XElement(Kml + "name", p.Name),
                new XElement(Kml + "ExtendedData", new XElement(Kml + "Data", new XAttribute("name", PointTypeKey), new XElement(Kml + "value", p.PointType))),
                new XElement(Kml + "Point", new XElement(Kml + "coordinates", Coordinate(new RoutePathPoint { Latitude = p.Latitude, Longitude = p.Longitude })))))));
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + new XElement(Kml + "kml", route);
    }

    public static byte[] BuildKmz(IReadOnlyList<RoutePoint> stops, IReadOnlyList<RoutePathPoint> geometry)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("doc.kml", CompressionLevel.Optimal).Open(), new UTF8Encoding(false)))
            writer.Write(BuildKml(stops, geometry));
        return output.ToArray();
    }

    public static GpxImportResult Import(Stream input, bool compressed)
    {
        if (!compressed) return ReadKml(input);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("doc.kml", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(e => Path.GetExtension(e.FullName).Equals(".kml", StringComparison.OrdinalIgnoreCase));
        if (entry is null || entry.Length > MaxKmlBytes) throw new ImportFormatException("ImportUnsupported");
        using var stream = entry.Open();
        return ReadKml(stream);
    }

    private static GpxImportResult ReadKml(Stream input)
    {
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxKmlBytes });
        var document = XDocument.Load(reader);
        var root = document.Root;
        if (root?.Name.LocalName != "kml") throw new ImportFormatException("ImportUnsupported");
        var ns = root.Name.Namespace;
        if (ns != Kml && ns.NamespaceName is not ("" or "http://earth.google.com/kml/2.1" or "http://earth.google.com/kml/2.0")) throw new ImportFormatException("ImportUnsupported");
        var geometry = new List<RoutePathPoint>();
        foreach (var line in root.Descendants().Where(e => e.Name == ns + "LineString" || e.Name == Gx + "Track"))
        {
            var part = line.Name == Gx + "Track"
                ? line.Elements(Gx + "coord").Select(e => ReadCoordinate(e.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))).ToList()
                : ReadCoordinates(line.Element(ns + "coordinates")?.Value ?? "");
            if (part.Count == 0) continue;
            // KML ground-relative heights are not elevations above sea level.
            if ((line.Element(ns + "altitudeMode") ?? line.Element(Gx + "altitudeMode"))?.Value != "absolute")
                foreach (var point in part) point.ElevationMeters = null;
            if (geometry.Count > 0)
            {
                if (!SamePosition(geometry[^1], part[0]))
                {
                    if (!SamePosition(geometry[^1], part[^1])) throw new ImportFormatException("ImportDisconnectedKml");
                    part.Reverse();
                }
                part.RemoveAt(0);
            }
            geometry.AddRange(part);
        }
        var stops = new List<RoutePoint>();
        foreach (var placemark in root.Descendants(ns + "Placemark"))
        {
            var point = placemark.Element(ns + "Point");
            var type = placemark.Element(ns + "ExtendedData")?.Elements(ns + "Data").FirstOrDefault(e => (string?)e.Attribute("name") == PointTypeKey)?.Element(ns + "value")?.Value;
            if (point is null || type is null) continue;
            var position = ReadCoordinates(point.Element(ns + "coordinates")?.Value ?? "");
            if (position.Count != 1 || type is not ("Start" or "End" or "Waypoint" or "DirectWaypoint")) throw new ImportFormatException("ImportUnsupported");
            stops.Add(new RoutePoint { Name = placemark.Element(ns + "name")?.Value ?? "", Latitude = position[0].Latitude, Longitude = position[0].Longitude, PointType = type });
        }
        if (geometry.Count == 0 && stops.Count == 0) throw new ImportFormatException("ImportEmptyRoute");
        if (stops.Count == 0)
        {
            string EndpointName(RoutePathPoint p, string fallback) => root.Descendants(ns + "Placemark").FirstOrDefault(e =>
                e.Element(ns + "Point")?.Element(ns + "coordinates") is { } value && ReadCoordinates(value.Value) is [var point] && SamePosition(point, p))?.Element(ns + "name")?.Value ?? fallback;
            stops.Add(new RoutePoint { Name = EndpointName(geometry[0], "A"), Latitude = geometry[0].Latitude, Longitude = geometry[0].Longitude, PointType = "Start" });
            if (geometry.Count > 1) stops.Add(new RoutePoint { Name = EndpointName(geometry[^1], "B"), Latitude = geometry[^1].Latitude, Longitude = geometry[^1].Longitude, PointType = "End" });
        }
        return new GpxImportResult { RoutePoints = stops, RouteGeometry = geometry };
    }

    private static List<RoutePathPoint> ReadCoordinates(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(tuple => ReadCoordinate(tuple.Split(','))).ToList();
    private static RoutePathPoint ReadCoordinate(string[] values)
    {
        if (values.Length is < 2 or > 3 || !double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            || !double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) || !WebSession.CoordinatesValid(lat, lon)) throw new ImportFormatException("ImportUnsupported");
        double? elevation = null;
        if (values.Length == 3 && !string.IsNullOrWhiteSpace(values[2]))
        {
            if (!double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude) || !double.IsFinite(altitude)) throw new ImportFormatException("ImportUnsupported");
            elevation = altitude;
        }
        return new RoutePathPoint { Longitude = lon, Latitude = lat, ElevationMeters = elevation };
    }
    private static bool SamePosition(RoutePathPoint a, RoutePathPoint b) => Math.Abs(a.Latitude - b.Latitude) < 0.000001 && Math.Abs(a.Longitude - b.Longitude) < 0.000001;
    private static string Coordinate(RoutePathPoint point) => point.Longitude.ToString("R", CultureInfo.InvariantCulture) + "," + point.Latitude.ToString("R", CultureInfo.InvariantCulture)
        + (point.ElevationMeters is { } elevation ? "," + elevation.ToString("R", CultureInfo.InvariantCulture) : "");
}
