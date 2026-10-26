using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace OpenHikeMap.Services;

public sealed record OsmTile(byte[] Bytes, DateTimeOffset Expires, string? ETag, DateTimeOffset? LastModified, bool Cacheable);

public sealed class OsmTileService(IHttpClientFactory clients, IHostApplicationLifetime lifetime) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });
    private readonly ConcurrentDictionary<string, Lazy<Task<OsmTile>>> _pending = new();

    public async Task<OsmTile> GetAsync(int z, int x, int y, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var key = $"{z}/{x}/{y}";
        if (_cache.TryGetValue<OsmTile>(key, out var cached)) return cached!;
        var pending = _pending.GetOrAdd(key, _ => new Lazy<Task<OsmTile>>(() => FetchAsync(key)));
        return await pending.Value.WaitAsync(token);
    }

    private async Task<OsmTile> FetchAsync(string key)
    {
        try { return await DownloadAsync(key); }
        finally { _pending.TryRemove(key, out _); }
    }

    private async Task<OsmTile> DownloadAsync(string key)
    {
        var token = lifetime.ApplicationStopping;
        using var response = await clients.CreateClient("OsmTiles").GetAsync($"https://tile.openstreetmap.org/{key}.png", token);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("OSM tile request failed.", null, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        var now = DateTimeOffset.UtcNow;
        var control = response.Headers.CacheControl;
        var cacheable = control?.NoStore != true && control?.NoCache != true && control?.Private != true;
        var age = response.Headers.Age ?? TimeSpan.Zero;
        var cacheLifetime = control?.SharedMaxAge ?? control?.MaxAge;
        var expires = cacheLifetime is { } ttl ? now + ttl - age : response.Content.Headers.Expires ?? now.AddDays(7);
        var tile = new OsmTile(bytes, expires, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified, cacheable);
        if (cacheable && expires > now) _cache.Set(key, tile, new MemoryCacheEntryOptions { AbsoluteExpiration = expires, Size = bytes.Length });
        return tile;
    }

    public void Dispose() => _cache.Dispose();
}
