namespace Optinull.Application.Routing;

/// <summary>Map tiles (the street picture) for the background of a route map.</summary>
public interface IMapTileClient
{
    /// <summary>The credit that must appear on the map, for example the OpenStreetMap notice.</summary>
    string Attribution { get; }

    /// <summary>One PNG tile, or null when it is not available. A missing tile is never an error.</summary>
    Task<byte[]?> GetTileAsync(int zoom, int x, int y, CancellationToken cancellationToken = default);
}
