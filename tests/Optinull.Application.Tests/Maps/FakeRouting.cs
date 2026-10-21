using Optinull.Application.Routing;
using Optinull.Problems.Vrp;

namespace Optinull.Application.Tests.Maps;

/// <summary>Straight-line distances in meters (a degree is about 111 km), and a "road" through the stops.</summary>
internal sealed class FakeRouting : IRoutingClient
{
    public int MaxLocations => 100;

    public Exception? MatrixFailure { get; set; }

    public Exception? RouteFailure { get; set; }

    public int MatrixCalls { get; private set; }

    public int RouteCalls { get; private set; }

    public Task<VrpCostMatrix> GetCostMatrixAsync(
        GeoPoint depot,
        IReadOnlyList<GeoPoint> customers,
        CancellationToken cancellationToken = default)
    {
        MatrixCalls++;

        if (MatrixFailure is not null)
            throw MatrixFailure;

        var points = customers.Prepend(depot).ToArray();
        var costs = new double[points.Length, points.Length];

        for (var a = 0; a < points.Length; a++)
        {
            for (var b = 0; b < points.Length; b++)
            {
                costs[a, b] = Math.Sqrt(
                    Math.Pow(points[a].Latitude - points[b].Latitude, 2) +
                    Math.Pow(points[a].Longitude - points[b].Longitude, 2)) * 111_000;
            }
        }

        return Task.FromResult(new VrpCostMatrix(costs));
    }

    public Task<RoadRoute> GetRouteAsync(
        IReadOnlyList<GeoPoint> stops,
        CancellationToken cancellationToken = default)
    {
        RouteCalls++;

        if (RouteFailure is not null)
            throw RouteFailure;

        return Task.FromResult(new RoadRoute(stops, 1000, 100));
    }
}
