using System.Collections.Concurrent;
using Optinull.Application.Routing;

namespace Optinull.Infrastructure.Routing;

/// <summary>Downloads map tiles over HTTP and remembers them, as the OpenStreetMap tile policy asks.</summary>
public sealed class HttpMapTileClient : IMapTileClient
{
    private readonly HttpClient _http;
    private readonly MapTileOptions _options;
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public HttpMapTileClient(HttpClient http, MapTileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        _options = options ?? new MapTileOptions();
    }

    public string Attribution => _options.Attribution;

    public async Task<byte[]?> GetTileAsync(
        int zoom,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.UrlTemplate))
            return null;

        var count = 1L << Math.Clamp(zoom, 0, 22);

        if (zoom < 0 || zoom > 22 || x < 0 || y < 0 || x >= count || y >= count)
            return null;

        var url = _options.UrlTemplate
            .Replace("{z}", zoom.ToString())
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString());

        if (_cache.TryGetValue(url, out var cached))
            return cached;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            timeout.CancelAfter(_options.Timeout);

            using var response = await _http.GetAsync(url, timeout.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);

            if (_cache.Count >= _options.CacheEntries)
                _cache.Clear();

            _cache[url] = bytes;
            return bytes;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
