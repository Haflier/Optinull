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

    public MapVrpInstance(
        VrpProblem problem,
        GeoPoint depot,
        IReadOnlyList<GeoPoint> customers,
        IRoutingClient routing)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(routing);

        _problem = problem;
        _depot = depot;
        _customers = customers;
        _routing = routing;
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

        var map = new RoadRouteChartRenderer().Render(
            Point(_depot),
            routes,
            $"{result.SolverName}: {result.Distance / 1000:0.0} km, {result.Plan.RouteCount} trucks");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return
        [
            new ReportImage("map-routes.png", Caption(result, straight), map),
            new ReportImage("map-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(VrpSolveResult result, int straight)
    {
        var text = $"map | {result.SolverName} | {result.Distance / 1000:0.0} km, " +
                   $"{result.Plan.RouteCount} of {_problem.Vehicles} trucks";

        var change = result.GreedyDistance > 0
            ? (result.GreedyDistance - result.Distance) / result.GreedyDistance
            : 0;

        text += $" ({Math.Abs(change):P1} {(change >= 0 ? "shorter" : "longer")} than nearest neighbour)";

        if (straight > 0)
            text += $"\n{straight} truck(s) drawn as straight lines: the road shape could not be loaded.";

        return text;
    }

    // Longitude is x and latitude is y.
    private static RoutePoint Point(GeoPoint point) => new(point.Longitude, point.Latitude);
}
