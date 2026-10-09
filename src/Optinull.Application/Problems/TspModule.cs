using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.Tsp;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed class TspModule : IProblemModule
{
    public string Id => "tsp";

    public string Title => "traveling salesman";

    public IReadOnlyList<string> BuiltInNames { get; } = ["circle20", "grid8", "random50"];

    public string CustomInputHelp
    {
        get
        {
            var limits = TspInputLimits.Default;

            return
                "one city per line as 'x y' (an optional leading id is ignored; " +
                "decimals use a dot). Example:\n0 0\n10 0\n10 10\n0 10\n" +
                $"Limit: {limits.MaxCities} cities.";
        }
    }

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "circle20":
                instance = new TspInstance(
                    "circle20",
                    "circle20 (20 cities on a circle)",
                    TspBenchmarkInstances.Circle(20),
                    TspBenchmarkInstances.CircleOptimum(20));
                return true;

            case "grid8":
                instance = new TspInstance(
                    "grid8",
                    "grid8 (8 x 8 grid of cities)",
                    TspBenchmarkInstances.Grid(8),
                    TspBenchmarkInstances.GridOptimum(8));
                return true;

            case "random50":
                instance = new TspInstance(
                    "random50",
                    "random50 (50 random cities)",
                    TspBenchmarkInstances.RandomCities(50, seed: 7),
                    knownOptimum: null);
                return true;

            default:
                instance = null;
                return false;
        }
    }

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!TspTextParser.TryParse(text, TspInputLimits.Default, out var problem, out error))
            return false;

        instance = new TspInstance(
            "custom",
            $"custom tour, {problem!.CityCount} cities",
            problem,
            knownOptimum: null);

        return true;
    }
}

internal sealed class TspInstance : IProblemInstance
{
    private readonly TspProblem _problem;
    private readonly double? _knownOptimum;

    public TspInstance(
        string name,
        string description,
        TspProblem problem,
        double? knownOptimum)
    {
        Name = name;
        Description = description;
        _problem = problem;
        _knownOptimum = knownOptimum;
    }

    public string Name { get; }

    public string Description { get; }

    public SolveReport Solve(
        SolverKind solver,
        int seed,
        CancellationToken cancellationToken)
    {
        var result = new TspSolveService().Solve(_problem, solver, seed, cancellationToken);

        var route = new RouteChartRenderer().Render(
            _problem.Cities.Select(city => new RoutePoint(city.X, city.Y)).ToList(),
            result.Tour.Cities,
            $"{result.SolverName}: length {result.Length:0.0}");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return new SolveReport(
            result.SolverName,
            [
                new ReportImage($"{Name}-route.png", Caption(result), route),
                new ReportImage($"{Name}-convergence.png", "Convergence", convergence)
            ]);
    }

    private string Caption(TspSolveResult result)
    {
        var prefix = $"{Name} | {result.SolverName} | tour length {result.Length:0.0}";

        if (_knownOptimum is { } optimum)
        {
            var gap = (result.Length - optimum) / optimum;

            return $"{prefix} (optimal {optimum:0.0}, gap {gap:P1})";
        }

        var change = (result.GreedyLength - result.Length) / result.GreedyLength;

        return $"{prefix} (nearest neighbour {result.GreedyLength:0.0}, " +
               $"{Math.Abs(change):P1} {(change >= 0 ? "shorter" : "longer")})";
    }
}
