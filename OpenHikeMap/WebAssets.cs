using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenHikeMap;

internal static class WebAssets
{
    internal static string Prepare()
    {
        using var resource = typeof(WebAssets).Assembly.GetManifestResourceStream("OpenHikeMap.assets.zip") ?? throw new InvalidDataException("Risorse dell'interfaccia non incluse nell'eseguibile.");
        using var bytes = new MemoryStream(); resource.CopyTo(bytes);
        using var framework = typeof(WebAssets).Assembly.GetManifestResourceStream("OpenHikeMap.blazor.web.js") ?? throw new InvalidDataException("Runtime Blazor non incluso nell'eseguibile.");
        using var frameworkBytes = new MemoryStream(); framework.CopyTo(frameworkBytes);
        using var fingerprint = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        fingerprint.AppendData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length));
        fingerprint.AppendData(frameworkBytes.GetBuffer().AsSpan(0, (int)frameworkBytes.Length));
        var hash = Convert.ToHexString(fingerprint.GetHashAndReset())[..24];
        var directory = Path.Combine(DesktopApplication.DataDirectory, "Application", hash);
        string[] required = ["_framework/blazor.web.js", "app.css", "css/map-overrides.css", "js/map.js", "js/elevation-profile.js", "lib/leaflet/leaflet.js", "lib/leaflet/leaflet.css"];
        if (required.All(file => File.Exists(Path.Combine(directory, "wwwroot", file)))) return directory;
        var staging = Directory.Exists(directory) ? directory : directory + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(staging, "wwwroot")); bytes.Position = 0;
        ZipFile.ExtractToDirectory(bytes, Path.Combine(staging, "wwwroot"), overwriteFiles: true);
        Directory.CreateDirectory(Path.Combine(staging, "wwwroot", "_framework"));
        frameworkBytes.Position = 0;
        using (var file = File.Create(Path.Combine(staging, "wwwroot", "_framework", "blazor.web.js"))) frameworkBytes.CopyTo(file);
        if (staging != directory) Directory.Move(staging, directory);
        return directory;
    }
}
