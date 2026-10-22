using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Application.Reporting;
using Optinull.Application.Routing;
using Optinull.Problems.Vrp;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Maps;

/// <summary>
/// A routing problem placed on a map. Its costs are road distances in meters, already fetched
/// from the routing service, so solving it is plain computation like any other instance. The
/// road geometry is only fetched when the result is drawn, and a truck whose road cannot be
/// loaded is drawn as straight lines instead of failing the job.
/// </summary>
internal sealed class MapVrpInstance : IProblemInstance
{
    private readonly VrpProblem _problem;
    private readonly GeoPoint _depot;
    private readonly IReadOnlyList<GeoPoint> _customers;
    private readonly IRoutingClient _routing;
    private readonly IMapTileClient? _tiles;

    public MapVrpInstance(
        VrpProblem problem,
        GeoPoint depot,
        IReadOnlyList<GeoPoint> customers,
        IRoutingClient routing,
        IMapTileClient? tiles = null)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(routing);

        _problem = problem;
        _depot = depot;
        _customers = customers;
        _routing = routing;
        _tiles = tiles;
    }

    public string Name => "map";

    public string Description =>
        $"map routing, {_problem.CustomerCount} customers, {_problem.Vehicles} trucks, capacity {_problem.Capacity}";

    public string ScoreLabel => "Total road distance (m)";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new VrpSolveService().Solve(
            _problem,
            solver,
            options.Seed,
            cancellationToken,
            options.Iterations,
            options.Progress,
            options.Tuning);

        return new SolverOutcome(
            solver,
            result.SolverName,
            result.Distance,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result, cancellationToken),
            Baseline: result.GreedyDistance,
            Settings: result.Settings);
    }

    // Runs on the job's worker thread, one road request per truck, one after another.
    private IReadOnlyList<ReportImage> RenderImages(VrpSolveResult result, CancellationToken cancellationToken)
    {
        var routes = new List<RoadChartRoute>();
        var straight = 0;

        for (var index = 0; index < result.Plan.Routes.Count; index++)
        {
            var route = result.Plan.Routes[index];

            var stops = route
                .Select(customer => _customers[customer])
                .Prepend(_depot)
                .Append(_depot)
                .ToList();

            IReadOnlyList<GeoPoint> line = stops;

            try
            {
                line = _routing.GetRouteAsync(stops, cancellationToken).GetAwaiter().GetResult().Points;
            }
            catch (Exception failure) when (failure is RoutingException or ArgumentException)
            {
                straight++;
            }

            var km = VrpEvaluator.RouteDistance(_problem, route) / 1000;
            var load = VrpEvaluator.RouteLoad(_problem, route);

            routes.Add(new RoadChartRoute(
                line.Select(Point).ToList(),
                route.Select(customer => Point(_customers[customer])).ToList(),
                $"Truck {index + 1}: {route.Count} stops, load {load}/{_problem.Capacity}, {km:0.#} km"));
        }

        var viewport = MapViewport.Fit(routes.SelectMany(route => route.Line).Append(Point(_depot)));
        var tiles = FetchTiles(viewport, out var wanted, cancellationToken);

        var map = new RoadRouteChartRenderer().Render(
            Point(_depot),
            routes,
            $"{result.SolverName}: {result.Distance / 1000:0.0} km, {result.Plan.RouteCount} trucks",
            viewport,
            tiles,
            _tiles?.Attribution);

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return
        [
            new ReportImage("map-routes.png", Caption(result, straight, wanted - tiles.Count), map),
            new ReportImage("map-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(VrpSolveResult result, int straight, int missingTiles)
    {
        var text = $"map | {result.SolverName} | {result.Distance / 1000:0.0} km, " +
                   $"{result.Plan.RouteCount} of {_problem.Vehicles} trucks";

        var change = result.GreedyDistance > 0
            ? (result.GreedyDistance - result.Distance) / result.GreedyDistance
            : 0;

        text += $" ({Math.Abs(change):P1} {(change >= 0 ? "shorter" : "longer")} than nearest neighbour)";

        if (straight > 0)
            text += $"\n{straight} truck(s) drawn as straight lines: the road shape could not be loaded.";

        if (_tiles is not null && missingTiles > 0)
            text += $"\nMap background incomplete: {missingTiles} tile(s) could not be loaded.";

        return text;
    }

    // One tile after another, as the tile policy asks. After a few failures in a row it stops
    // trying: the picture is still drawn, just with a plainer background.
    private Dictionary<MapTile, byte[]> FetchTiles(
        MapViewport viewport,
        out int wanted,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<MapTile, byte[]>();
        var failures = 0;

        wanted = 0;

        if (_tiles is null)
            return found;

        foreach (var tile in viewport.Tiles())
        {
            wanted++;

            if (failures >= 3)
                continue;

            var bytes = _tiles.GetTileAsync(tile.Zoom, tile.X, tile.Y, cancellationToken)
                .GetAwaiter()
                .GetResult();

            if (bytes is null)
                failures++;
            else
                found[tile] = bytes;
        }

        return found;
    }

    // Longitude is x and latitude is y.
    private static RoutePoint Point(GeoPoint point) => new(point.Longitude, point.Latitude);
}
