using System.IO;
using System.IO.Compression;
namespace OpenHikeMap;
internal static class WebAssets
{
    internal static string Prepare()
    {
        // The assembly build id changes with embedded assets; warm starts need only check the extracted files.
        var assembly = typeof(WebAssets).Assembly;
        var directory = Path.Combine(DesktopApplication.DataDirectory, "Application", assembly.ManifestModule.ModuleVersionId.ToString("N"));
        string[] required = ["_framework/blazor.web.js", "app.css", "css/map-overrides.css", "js/map.js", "js/webcams.js", "js/terrain-map.js", "lib/maplibre/maplibre-gl.mjs", "lib/maplibre/maplibre-gl-worker.mjs", "lib/maplibre/maplibre-gl.css", "js/reference-grid.js", "js/utm-grid.js", "lib/proj4/proj4.js", "js/elevation-profile.js", "lib/leaflet/leaflet.js", "lib/leaflet/leaflet.css"];
        if (required.All(file => File.Exists(Path.Combine(directory, "wwwroot", file)))) return directory;
        using var resource = assembly.GetManifestResourceStream("OpenHikeMap.assets.zip") ?? throw new InvalidDataException("Risorse dell'interfaccia non incluse nell'eseguibile.");
        using var framework = assembly.GetManifestResourceStream("OpenHikeMap.blazor.web.js") ?? throw new InvalidDataException("Runtime Blazor non incluso nell'eseguibile.");
        var staging = Directory.Exists(directory) ? directory : directory + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(staging, "wwwroot"));
        ZipFile.ExtractToDirectory(resource, Path.Combine(staging, "wwwroot"), overwriteFiles: true);
        Directory.CreateDirectory(Path.Combine(staging, "wwwroot", "_framework"));
        using (var file = File.Create(Path.Combine(staging, "wwwroot", "_framework", "blazor.web.js"))) framework.CopyTo(file);
        if (staging != directory) Directory.Move(staging, directory);
        return directory;
    }
}
