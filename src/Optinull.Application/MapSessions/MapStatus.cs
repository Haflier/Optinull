using Optinull.Application.Optimization;
using Optinull.Problems.Vrp;

namespace Optinull.Application.MapSessions;

public enum MapPhase
{
    /// <summary>Nothing has been submitted yet.</summary>
    Idle,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>What the page may show about its own session.</summary>
public sealed record MapSessionInfo(string Solver, int? Iterations, int Seed, DateTimeOffset ExpiresAt);

/// <summary>
/// One route of the solution. Customers are 0-based positions in the submitted customer list,
/// in driving order; the depot is not listed.
/// </summary>
public sealed record MapRouteResult(
    IReadOnlyList<int> Customers,
    int Load,
    int Capacity,
    double DistanceMeters);

public sealed record MapSolution(
    string Solver,
    double TotalDistanceMeters,
    double GreedyDistanceMeters,
    IReadOnlyList<MapRouteResult> Routes)
{
    internal static MapSolution From(VrpProblem problem, VrpSolveResult result) =>
        new(
            result.SolverName,
            result.Distance,
            result.GreedyDistance,
            result.Plan.Routes
                .Select(route => new MapRouteResult(
                    route.ToArray(),
                    VrpEvaluator.RouteLoad(problem, route),
                    problem.Capacity,
                    VrpEvaluator.RouteDistance(problem, route)))
                .ToList());
}

/// <summary>Progress is 0..1; only the budgeted solvers report it before they finish.</summary>
public sealed record MapSessionStatus(
    MapPhase Phase,
    double Progress,
    long? JobId,
    MapSolution? Solution,
    DateTimeOffset ExpiresAt);

/// <summary>A place on Earth in degrees.</summary>
public sealed record MapStop(double Latitude, double Longitude);

/// <summary>
/// The stops of every route of a finished run, in the order of <see cref="MapSolution.Routes"/>.
/// Each route starts and ends at the depot, so it can be sent to the router as it is.
/// </summary>
public sealed record MapRoutePlan(long JobId, IReadOnlyList<IReadOnlyList<MapStop>> Routes);

public enum MapSubmitOutcome
{
    Started,

    /// <summary>The user already has a job in progress, from this map or from the chat.</summary>
    Busy,

    QueueFull
}

public sealed record MapSubmitResult(MapSubmitOutcome Outcome, long? JobId = null);
