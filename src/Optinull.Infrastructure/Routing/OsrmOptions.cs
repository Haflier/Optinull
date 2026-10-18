namespace Optinull.Infrastructure.Routing;

public sealed class OsrmOptions
{
    /// <summary>
    /// The public demo server is for prototyping only (light use, no guarantees). Point this at
    /// a self-hosted OSRM or another production service later.
    /// </summary>
    public string BaseUrl { get; init; } = "https://router.project-osrm.org";

    public string Profile { get; init; } = "driving";

    /// <summary>Depot plus customers. The public demo server refuses more than 100.</summary>
    public int MaxLocations { get; init; } = 100;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How many distinct matrices to remember before the cache is cleared.</summary>
    public int CacheEntries { get; init; } = 32;
}
