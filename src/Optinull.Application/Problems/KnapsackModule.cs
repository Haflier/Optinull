using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.Knapsack;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed class KnapsackModule : IProblemModule
{
    public string Id => "knapsack";

    public string Title => "0/1 knapsack";

    public IReadOnlyList<string> BuiltInNames { get; } = ["knap20", "knap100"];

    public string CustomInputHelp
    {
        get
        {
            var limits = KnapsackInputLimits.Default;

            return
                "first line: the capacity; then one item per line as 'value weight' " +
                "(whole numbers). Example:\n50\n60 10\n100 20\n120 30\n" +
                $"Limits: {limits.MaxItems} items, capacity up to {limits.MaxCapacity}.";
        }
    }

    public IReadOnlyList<ProblemParameter> Parameters { get; } =
    [
        new("items", "number of random items", 3, KnapsackInputLimits.Default.MaxItems, 30)
    ];

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "knap20":
                instance = new KnapsackInstance(
                    "knap20",
                    "knap20 (20 items)",
                    KnapsackInstances.Random(20, seed: 11));
                return true;

            case "knap100":
                instance = new KnapsackInstance(
                    "knap100",
                    "knap100 (100 items)",
                    KnapsackInstances.Random(100, seed: 11));
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

        if (!KnapsackTextParser.TryParse(text, KnapsackInputLimits.Default, out var problem, out error))
            return false;

        instance = new KnapsackInstance(
            "custom",
            $"custom knapsack, {problem!.ItemCount} items, capacity {problem.Capacity}",
            problem);

        return true;
    }

    public IReadOnlyList<DatasetPreset> Presets { get; } =
    [
        new("small", "Small: knap20 (20 items)", "knap20"),
        new("medium", "Medium: 50 random items", "knapsack",
            new Dictionary<string, string> { ["items"] = "50" }),
        new("large", "Large: knap100 (100 items)", "knap100")
    ];

    public IProblemDialog CreateDialog() => KnapsackDialog.Create(this);

    public bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!ProblemParameterReader.TryRead(Parameters, values, out var read, out error))
            return false;

        var items = read["items"];
        var problem = KnapsackInstances.Random(items, seed);

        instance = new KnapsackInstance(
            $"knapsack{items}",
            $"random knapsack, {items} items, capacity {problem.Capacity} (seed {seed})",
            problem);

        return true;
    }
}

internal sealed class KnapsackInstance : IProblemInstance
{
    private readonly KnapsackProblem _problem;

    public KnapsackInstance(string name, string description, KnapsackProblem problem)
    {
        Name = name;
        Description = description;
        _problem = problem;
    }

    public string Name { get; }

    public string Description { get; }

    public string ScoreLabel => "Value";

    public bool HigherIsBetter => true;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new KnapsackSolveService().Solve(
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
            result.Value,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Baseline: result.GreedyValue);
    }

    private IReadOnlyList<ReportImage> RenderImages(KnapsackSolveResult result)
    {
        var points = _problem.Items
            .Select((item, index) => new KnapsackPoint(item.Weight, item.Value, result.Selection[index]))
            .ToList();

        var items = new KnapsackChartRenderer().Render(
            points,
            $"{result.SolverName}: value {result.Value}, weight {result.Weight} of {_problem.Capacity}");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return
        [
            new ReportImage($"{Name}-items.png", Caption(result), items),
            new ReportImage($"{Name}-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(KnapsackSolveResult result)
    {
        // The optimum is cheap to compute exactly at these sizes.
        var optimum = KnapsackExactSolver.Optimum(_problem);
        var gap = (optimum - result.Value) / (double)optimum;

        return $"{Name} | {result.SolverName} | value {result.Value}, weight {result.Weight}/{_problem.Capacity} " +
               $"(optimal {optimum}, gap {gap:P1}; greedy {result.GreedyValue:0})";
    }
}
