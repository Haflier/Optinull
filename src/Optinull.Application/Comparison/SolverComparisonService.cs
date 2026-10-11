using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Comparison;

/// <summary>Runs every solver on the same problem and the same seed, then reports them side by side.</summary>
public sealed class SolverComparisonService
{
    public static readonly IReadOnlyList<SolverKind> Solvers =
    [
        SolverKind.Greedy,
        SolverKind.HillClimbing,
        SolverKind.SimulatedAnnealing,
        SolverKind.GeneticAlgorithm
    ];

    public SolveReport Compare(
        IProblemInstance instance,
        SolveOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(options);

        var outcomes = new List<SolverOutcome>();

        for (var index = 0; index < Solvers.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var segment = new SegmentProgress(options.Progress, index, Solvers.Count);

            outcomes.Add(instance.Run(
                Solvers[index],
                options with { Progress = segment },
                cancellationToken));

            segment.Complete();
        }

        return Build(instance, outcomes);
    }

    public static SolverOutcome PickBest(IReadOnlyList<SolverOutcome> outcomes, bool higherIsBetter)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        // Ties go to the earlier, cheaper solver.
        return (higherIsBetter
            ? outcomes.MaxBy(outcome => outcome.Score)
            : outcomes.MinBy(outcome => outcome.Score))!;
    }

    private static SolveReport Build(IProblemInstance instance, IReadOnlyList<SolverOutcome> outcomes)
    {
        var best = PickBest(outcomes, instance.HigherIsBetter);

        var bars = outcomes
            .Select(outcome => new ComparisonBar(
                outcome.SolverName,
                outcome.Score,
                ReferenceEquals(outcome, best)))
            .ToList();

        var scoreChart = new ComparisonChartRenderer().Render(
            bars,
            $"{instance.Name}: {instance.ScoreLabel} by solver",
            instance.ScoreLabel);

        // Greedy builds one solution, so it has nothing to show over time.
        var convergence = new ConvergenceChartRenderer().Render(
            outcomes.Skip(1).Select(outcome => outcome.Convergence).ToList(),
            $"Convergence, {instance.Name}");

        var images = new List<ReportImage>
        {
            new($"{instance.Name}-compare.png", Table(instance, outcomes, best), scoreChart),
            new($"{instance.Name}-compare-convergence.png", "Convergence of every solver", convergence)
        };

        images.AddRange(best.Render().Take(1));

        return new SolveReport("Compare", images);
    }

    public static string Table(
        IProblemInstance instance,
        IReadOnlyList<SolverOutcome> outcomes,
        SolverOutcome best)
    {
        var lines = new List<string> { $"{instance.Description} - {instance.ScoreLabel}:" };

        lines.AddRange(outcomes.Select(outcome =>
            $"{outcome.SolverName}: {outcome.Score:0.##} ({outcome.Elapsed.TotalMilliseconds:0} ms)"));

        lines.Add($"Best: {best.SolverName}");

        var greedy = outcomes[0].Score;

        if (greedy != 0)
        {
            var improvement = instance.HigherIsBetter
                ? (best.Score - greedy) / greedy
                : (greedy - best.Score) / greedy;

            lines.Add($"Improvement over greedy: {improvement:P1}");
        }

        return string.Join("\n", lines);
    }

    // Maps one solver's 0..1 onto its share of the whole comparison.
    private sealed class SegmentProgress : IProgress<double>
    {
        private readonly IProgress<double>? _outer;
        private readonly int _index;
        private readonly int _count;

        public SegmentProgress(IProgress<double>? outer, int index, int count)
        {
            _outer = outer;
            _index = index;
            _count = count;
        }

        public void Report(double value) =>
            _outer?.Report((_index + Math.Clamp(value, 0, 1)) / _count);

        public void Complete() => Report(1.0);
    }
}
