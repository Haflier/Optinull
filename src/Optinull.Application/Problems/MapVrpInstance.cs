using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.Vrp;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

/// <summary>
/// A routing problem drawn on a map. Its costs are road distances in meters, already fetched
/// from the routing service, so solving it is plain computation like any other instance. The
/// solution is kept for the map session to read; the chat gets the convergence chart and the
/// summary, and the routes themselves are shown on the map.
/// </summary>
internal sealed class MapVrpInstance : IProblemInstance
{
    private VrpSolveResult? _result;

    public MapVrpInstance(VrpProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        Problem = problem;
    }

    public VrpProblem Problem { get; }

    /// <summary>The solution of the latest run; null until one has finished.</summary>
    public VrpSolveResult? Result => Volatile.Read(ref _result);

    public string Name => "map";

    public string Description =>
        $"map routing, {Problem.CustomerCount} customers, {Problem.Vehicles} trucks, capacity {Problem.Capacity}";

    public string ScoreLabel => "Total road distance (m)";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new VrpSolveService().Solve(
            Problem,
            solver,
            options.Seed,
            cancellationToken,
            options.Iterations,
            options.Progress,
            options.Tuning);

        Volatile.Write(ref _result, result);

        return new SolverOutcome(
            solver,
            result.SolverName,
            result.Distance,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Baseline: result.GreedyDistance,
            Settings: result.Settings);
    }

    private static IReadOnlyList<ReportImage> RenderImages(VrpSolveResult result)
    {
        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return [new ReportImage("map-convergence.png", "Convergence", convergence)];
    }
}
