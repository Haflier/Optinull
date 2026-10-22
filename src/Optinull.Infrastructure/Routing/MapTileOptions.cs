namespace Optinull.Infrastructure.Routing;

public sealed class MapTileOptions
{
    /// <summary>
    /// The tile server, with {z}, {x} and {y}. The default is the public OpenStreetMap server, whose
    /// usage policy allows light use only (a real User-Agent, caching, no bulk downloads). For a busy
    /// bot use a tile provider or your own server here, and put an API key in the URL if it needs
    /// one. An empty value turns the map background off.
    /// </summary>
    public string UrlTemplate { get; init; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>Printed on every map that uses tiles. Change it together with the tile source.</summary>
    public string Attribution { get; init; } = "\u00a9 OpenStreetMap contributors";

    public string UserAgent { get; init; } = "Optinull/1.0 (Telegram routing bot)";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How many tiles to remember, so a repeated area costs no downloads.</summary>
    public int CacheEntries { get; init; } = 512;
}
