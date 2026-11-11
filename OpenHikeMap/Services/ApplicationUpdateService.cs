using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenHikeMap.Services;

public sealed record UpdateAsset(string Name, Uri Url, long Size, string? Digest);
public sealed record ApplicationUpdate(Version Version, string Tag, UpdateAsset? Asset);
public sealed record UpdateDownloadProgress(double? Percent, bool Verifying = false, bool Installing = false);
public sealed record DownloadedUpdate(string ExecutablePath, string Sha256);

public sealed class ApplicationUpdateService(HttpClient client)
{
    public const string ReleasesPage = "https://github.com/Rallo00/OpenHikeMap/releases";
    public const string ReleasesApi = "https://api.github.com/repos/Rallo00/OpenHikeMap/releases?per_page=30";
    private const long MaxDownloadBytes = 500L * 1024 * 1024;

    public async Task<ApplicationUpdate?> CheckAsync(Version installed, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApi);
        request.Headers.UserAgent.ParseAdd(ApplicationInfo.UserAgent); request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(input, cancellationToken: timeout.Token).ConfigureAwait(false);
        return ParseReleases(json.RootElement, installed);
    }

    public static bool TryVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var match = Regex.Match(value?.Trim() ?? "", @"^[vV]?(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?$");
        if (!match.Success || !Version.TryParse(string.Join('.', Enumerable.Range(1, 4).Select(i => match.Groups[i].Success ? match.Groups[i].Value : "0")), out var parsed)) return false;
        version = parsed; return true;
    }

    public static ApplicationUpdate? ParseReleases(JsonElement releases, Version installed)
    {
        if (releases.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid GitHub release response.");
        var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build), Math.Max(0, installed.Revision));
        ApplicationUpdate? newest = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (Flag(release, "draft") || Flag(release, "prerelease") || !release.TryGetProperty("tag_name", out var tag) || !TryVersion(tag.GetString(), out var version) || version <= current || newest is not null && version <= newest.Version) continue;
            UpdateAsset? selected = null; var bestScore = -1;
            if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameValue) || nameValue.GetString() is not { } name || !name.StartsWith("OpenHikeMap", StringComparison.OrdinalIgnoreCase)
                    || !asset.TryGetProperty("browser_download_url", out var urlValue) || !Uri.TryCreate(urlValue.GetString(), UriKind.Absolute, out var url) || !TrustedDownload(url)) continue;
                var lower = name.ToLowerInvariant();
                if (lower.Contains("setup") || lower.Contains("installer") || lower.Contains("arm64") || lower.Contains("x86") || lower.Contains("win32")) continue;
                var extension = Path.GetExtension(name).ToLowerInvariant(); if (extension is not (".exe" or ".zip")) continue;
                var score = (lower.Contains("x64") ? 10 : 0) + (extension == ".exe" ? 4 : 2) + (name.Equals("OpenHikeMap.exe", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
                if (score <= bestScore) continue;
                var size = asset.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var bytes) ? bytes : 0;
                if (size <= 0 || size > MaxDownloadBytes) continue;
                selected = new(name, url, size, asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String ? digest.GetString() : null); bestScore = score;
            }
            newest = new(version, tag.GetString()!, selected);
        }
        return newest;
    }

    public async Task<DownloadedUpdate> DownloadAsync(ApplicationUpdate update, string directory, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken token = default)
    {
        var asset = update.Asset ?? throw new InvalidDataException("Missing compatible release asset.");
        if (!TrustedDownload(asset.Url)) throw new InvalidDataException("Untrusted update URL.");
        Directory.CreateDirectory(directory);
        var package = Path.Combine(directory, "release" + Path.GetExtension(asset.Name).ToLowerInvariant());
        var executable = Path.Combine(directory, "OpenHikeMap.new.exe");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.Url); request.Headers.UserAgent.ParseAdd(ApplicationInfo.UserAgent);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri is { Scheme: not "https" }) throw new InvalidDataException("Update download must use HTTPS.");
        var length = response.Content.Headers.ContentLength;
        if (length is > MaxDownloadBytes || length is > 0 && length != asset.Size) throw new InvalidDataException("Unexpected update download size.");
        await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
        await using (var output = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            var buffer = new byte[81920]; long total = 0; var lastReport = Stopwatch.StartNew();
            int read;
            while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                total += read; if (total > asset.Size || total > MaxDownloadBytes) throw new InvalidDataException("Update download is too large.");
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
                if (lastReport.ElapsedMilliseconds >= 100) { progress?.Report(new(total * 100d / asset.Size)); lastReport.Restart(); }
            }
            if (total != asset.Size) throw new InvalidDataException("Incomplete update download.");
        }
        progress?.Report(new(100, true));
        var packageHash = await HashAsync(package, timeout.Token).ConfigureAwait(false);
        if (asset.Digest is { Length: > 0 } digest && (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || !packageHash.Equals(digest[7..], StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Update checksum mismatch.");
        if (Path.GetExtension(package) == ".zip")
        {
            using var archive = ZipFile.OpenRead(package);
            var entry = archive.Entries.FirstOrDefault(e => Path.GetFileName(e.FullName).Equals("OpenHikeMap.exe", StringComparison.OrdinalIgnoreCase))
                ?? archive.Entries.FirstOrDefault(e => Path.GetFileName(e.FullName).StartsWith("OpenHikeMap", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(e.FullName).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    && !e.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) && !e.Name.Contains("install", StringComparison.OrdinalIgnoreCase) && !e.Name.Contains("update", StringComparison.OrdinalIgnoreCase));
            if (entry is null || entry.Length <= 0 || entry.Length > MaxDownloadBytes) throw new InvalidDataException("The release does not contain OpenHikeMap.exe.");
            await using var input = entry.Open(); await using var output = new FileStream(executable, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920]; int read; long extracted = 0;
            while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                extracted += read; if (extracted > entry.Length || extracted > MaxDownloadBytes) throw new InvalidDataException("Executable exceeds the archive size limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
            }
            if (output.Length != entry.Length) throw new InvalidDataException("Incomplete executable in release archive.");
        }
        else File.Move(package, executable);
        ValidateExecutable(executable, update.Version);
        return new(executable, await HashAsync(executable, timeout.Token).ConfigureAwait(false));
    }

    public static void ValidateExecutable(string path, Version? expected = null)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        if (!string.Equals(info.ProductName, "OpenHikeMap", StringComparison.OrdinalIgnoreCase) || !TryVersion(info.FileVersion, out var version) || expected is not null && version != expected) throw new InvalidDataException("The executable metadata does not match the release.");
        using var input = File.OpenRead(path); using var pe = new PEReader(input);
        if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64 || RuntimeInformation.ProcessArchitecture != Architecture.X64) throw new InvalidDataException("The executable is not compatible with Windows x64.");
    }

    public static async Task<string> HashAsync(string path, CancellationToken token = default)
    {
        await using var input = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false));
    }
    private static bool Flag(JsonElement value, string key) => value.TryGetProperty(key, out var flag) && flag.ValueKind == JsonValueKind.True;
    private static bool TrustedDownload(Uri url) => url.Scheme == "https" && url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && url.AbsolutePath.StartsWith("/Rallo00/OpenHikeMap/releases/download/", StringComparison.OrdinalIgnoreCase);
}
