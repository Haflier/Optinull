using Optinull.Problems.Vrp;

namespace Optinull.Application.Routing;

public interface IRoutingClient
{
    /// <summary>The most places one request may cover, the depot included.</summary>
    int MaxLocations { get; }

    /// <summary>
    /// Road distances in meters between the depot and the customers, directed (the way there
    /// can differ from the way back). Throws <see cref="UnreachableRouteException"/> when a
    /// pair has no connecting road.
    /// </summary>
    Task<VrpCostMatrix> GetCostMatrixAsync(
        GeoPoint depot,
        IReadOnlyList<GeoPoint> customers,
        CancellationToken cancellationToken = default);

    /// <summary>The road geometry through the stops, in the order given.</summary>
    Task<RoadRoute> GetRouteAsync(
        IReadOnlyList<GeoPoint> stops,
        CancellationToken cancellationToken = default);
}
