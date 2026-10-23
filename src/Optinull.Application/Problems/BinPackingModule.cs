using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.BinPacking;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed class BinPackingModule : IProblemModule
{
    public string Id => "binpacking";

    public string Title => "Bin packing";

    public IReadOnlyList<string> BuiltInNames { get; } = ["bin30", "bin120"];

    public string CustomInputHelp
    {
        get
        {
            var limits = BinPackingInputLimits.Default;

            return
                "first line: the capacity of one bin; then one line per kind of item as " +
                "'size' or 'size quantity' (whole numbers). Example, cutting 6000 mm bars:\n" +
                "6000\n2400 3\n1800 5\n1200 8\n" +
                $"Limits: {limits.MaxItems} items in total, capacity up to {limits.MaxCapacity}.";
        }
    }

    public IReadOnlyList<ProblemParameter> Parameters { get; } =
    [
        new("items", "number of random items", 3, BinPackingInputLimits.Default.MaxItems, 40),
        new("capacity", "capacity of one bin", 1, BinPackingInputLimits.Default.MaxCapacity, 100),
        new("minsize", "smallest item size (default: a tenth of the capacity)", 1, BinPackingInputLimits.Default.MaxCapacity, 10),
        new("maxsize", "largest item size (default: half the capacity)", 1, BinPackingInputLimits.Default.MaxCapacity, 50)
    ];

    public IReadOnlyList<DatasetPreset> Presets { get; } =
    [
        new("small", "Small: bin30 (30 items)", "bin30"),
        new("medium", "Medium: 60 random items", "binpacking",
            new Dictionary<string, string> { ["items"] = "60" }),
        new("large", "Large: bin120 (120 items)", "bin120")
    ];

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "bin30":
                instance = new BinPackingInstance(
                    "bin30",
                    "bin30 (30 items, bin capacity 100)",
                    BinPackingInstances.Random(30, 100, 10, 50, seed: 11));
                return true;

            case "bin120":
                instance = new BinPackingInstance(
                    "bin120",
                    "bin120 (120 items, bin capacity 100)",
                    BinPackingInstances.Random(120, 100, 10, 50, seed: 11));
                return true;

            default:
                instance = null;
                return false;
        }
    }

    public IProblemDialog CreateDialog() => BinPackingDialog.Create(this);

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!BinPackingTextParser.TryParse(text, BinPackingInputLimits.Default, out var problem, out error))
            return false;

        instance = new BinPackingInstance(
            "custom",
            $"custom bin packing, {problem!.ItemCount} items, bin capacity {problem.Capacity}",
            problem);

        return true;
    }

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
        var capacity = read["capacity"];

        // Sizes the user left out follow the capacity, so capacity=20 alone still makes sense.
        var maxSize = Given(values, "maxsize") ? read["maxsize"] : Math.Max(1, capacity / 2);
        var minSize = Given(values, "minsize") ? read["minsize"] : Math.Min(maxSize, Math.Max(1, capacity / 10));

        if (maxSize > capacity)
        {
            error = $"maxsize cannot be more than the capacity ({capacity}), or those items would never fit.";
            return false;
        }

        if (minSize > maxSize)
        {
            error = $"minsize ({minSize}) cannot be more than maxsize ({maxSize}).";
            return false;
        }

        var problem = BinPackingInstances.Random(items, capacity, minSize, maxSize, seed);

        instance = new BinPackingInstance(
            $"binpacking{items}",
            $"random bin packing, {items} items of size {minSize}-{maxSize}, " +
            $"bin capacity {capacity} (seed {seed})",
            problem);

        return true;
    }

    private static bool Given(IReadOnlyDictionary<string, string> values, string key) =>
        values.Keys.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
}

internal sealed class BinPackingInstance : IProblemInstance
{
    private readonly BinPackingProblem _problem;

    public BinPackingInstance(string name, string description, BinPackingProblem problem)
    {
        Name = name;
        Description = description;
        _problem = problem;
    }

    public string Name { get; }

    public string Description { get; }

    public string ScoreLabel => "Bins";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new BinPackingSolveService().Solve(
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
            result.Bins,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Baseline: result.GreedyBins,
            Settings: result.Settings);
    }

    private IReadOnlyList<ReportImage> RenderImages(BinPackingSolveResult result)
    {
        var sizes = _problem.Sizes;

        var bins = result.Plan
            .Bins()
            .Select(bin => (IReadOnlyList<int>)bin.Select(item => sizes[item]).OrderByDescending(size => size).ToList())
            .ToList();

        var packing = new BinPackingChartRenderer().Render(
            bins,
            _problem.Capacity,
            $"{result.SolverName}: {result.Bins} bins of capacity {_problem.Capacity}");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName} (bins plus a fraction for uneven fill)");

        return
        [
            new ReportImage($"{Name}-bins.png", Caption(result), packing),
            new ReportImage($"{Name}-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(BinPackingSolveResult result)
    {
        // No bin count can beat total size over capacity, so reaching it proves the packing optimal.
        var lowerBound = _problem.LowerBound;
        var bound = result.Bins == lowerBound ? $"lower bound {lowerBound}, optimal" : $"lower bound {lowerBound}";
        var fill = _problem.TotalSize / (double)((long)result.Bins * _problem.Capacity);

        return $"{Name} | {result.SolverName} | {result.Bins} bins of {_problem.Capacity} " +
               $"({bound}; first-fit decreasing {result.GreedyBins}) | average fill {fill:P0}";
    }
}
