using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace OpenHikeMap.Services;

public sealed record OsmTile(byte[] Bytes, DateTimeOffset Expires, string? ETag, DateTimeOffset? LastModified, bool Cacheable);

public sealed class OsmTileService(IHttpClientFactory clients) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });
    private readonly ConcurrentDictionary<string, Lazy<Task<OsmTile>>> _pending = new();

    public async Task<OsmTile> GetAsync(int z, int x, int y)
    {
        var key = $"{z}/{x}/{y}";
        if (_cache.TryGetValue<OsmTile>(key, out var cached)) return cached!;
        var pending = _pending.GetOrAdd(key, _ => new Lazy<Task<OsmTile>>(() => FetchAsync(key)));
        try { return await pending.Value; }
        finally { _pending.TryRemove(new KeyValuePair<string, Lazy<Task<OsmTile>>>(key, pending)); }
    }

    private async Task<OsmTile> FetchAsync(string key)
    {
        using var response = await clients.CreateClient("OsmTiles").GetAsync($"https://tile.openstreetmap.org/{key}.png");
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("OSM tile request failed.", null, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var now = DateTimeOffset.UtcNow;
        var control = response.Headers.CacheControl;
        var cacheable = control?.NoStore != true && control?.NoCache != true && control?.Private != true;
        var age = response.Headers.Age ?? TimeSpan.Zero;
        var lifetime = control?.SharedMaxAge ?? control?.MaxAge;
        var expires = lifetime is { } ttl ? now + ttl - age : response.Content.Headers.Expires ?? now.AddDays(7);
        var tile = new OsmTile(bytes, expires, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified, cacheable);
        if (cacheable && expires > now) _cache.Set(key, tile, new MemoryCacheEntryOptions { AbsoluteExpiration = expires, Size = bytes.Length });
        return tile;
    }

    public void Dispose() => _cache.Dispose();
}
