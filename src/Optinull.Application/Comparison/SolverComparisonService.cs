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

    public static SolverOutcome PickBest(IReadOnlyList<SolverOutcome> outcomes, bool higherIsBetter) =>
        PickWinners(outcomes, higherIsBetter)[0];

    /// <summary>
    /// The solvers with the best score, narrowed to the fastest of them. Scores that differ
    /// only by rounding (the same tour walked from another city) are equal, and so are times
    /// that show the same number of milliseconds. More than one winner means a real tie.
    /// </summary>
    public static IReadOnlyList<SolverOutcome> PickWinners(
        IReadOnlyList<SolverOutcome> outcomes,
        bool higherIsBetter)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var extreme = higherIsBetter
            ? outcomes.Max(outcome => outcome.Score)
            : outcomes.Min(outcome => outcome.Score);

        var best = outcomes.Where(outcome => AreEqual(outcome.Score, extreme)).ToList();
        var fastest = best.Min(Milliseconds);

        return best.Where(outcome => Milliseconds(outcome) == fastest).ToList();
    }

    private static long Milliseconds(SolverOutcome outcome) =>
        (long)Math.Round(outcome.Elapsed.TotalMilliseconds);

    private static bool AreEqual(double first, double second) =>
        Math.Abs(first - second) <= Math.Max(Math.Abs(second) * 1e-9, 1e-12);

    private static SolveReport Build(IProblemInstance instance, IReadOnlyList<SolverOutcome> outcomes)
    {
        var winners = PickWinners(outcomes, instance.HigherIsBetter);

        var bars = outcomes
            .Select(outcome => new ComparisonBar(
                outcome.SolverName,
                outcome.Score,
                winners.Any(winner => ReferenceEquals(winner, outcome))))
            .ToList();

        var scoreChart = new ComparisonChartRenderer().Render(
            bars,
            $"{instance.Name}: {instance.ScoreLabel} by solver",
            instance.ScoreLabel);

        var convergence = new ConvergenceChartRenderer().Render(
            ConvergenceOf(outcomes),
            $"Convergence, {instance.Name}");

        var images = new List<ReportImage>
        {
            new($"{instance.Name}-compare.png", Table(instance, outcomes, winners), scoreChart),
            new($"{instance.Name}-compare-convergence.png", "Convergence of every solver (Greedy builds a single solution, so it is a flat line)", convergence)
        };

        images.AddRange(winners[0].Render().Take(1));

        return new SolveReport("Compare", images);
    }

    /// <summary>
    /// One series per solver. Greedy builds a single solution, so it is stretched to the
    /// longest run and drawn as a flat reference line to beat.
    /// </summary>
    public static IReadOnlyList<ConvergenceSeries> ConvergenceOf(IReadOnlyList<SolverOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var longest = outcomes.Max(outcome => outcome.Convergence.FinalEvaluations);

        return outcomes
            .Select(outcome => outcome.Solver == SolverKind.Greedy
                ? outcome.Convergence with { FinalEvaluations = longest }
                : outcome.Convergence)
            .ToList();
    }

    public static string Table(
        IProblemInstance instance,
        IReadOnlyList<SolverOutcome> outcomes,
        IReadOnlyList<SolverOutcome> winners)
    {
        var lines = new List<string> { $"{instance.Description} - {instance.ScoreLabel}:" };

        lines.AddRange(outcomes.Select(outcome =>
            $"{outcome.SolverName}: {outcome.Score:0.##} ({outcome.Elapsed.TotalMilliseconds:0} ms)"));

        var best = winners[0];
        var names = string.Join(", ", winners.Select(winner => winner.SolverName));
        var sameScore = outcomes.All(outcome => AreEqual(outcome.Score, best.Score));

        if (winners.Count == 1)
        {
            lines.Add($"Best: {best.SolverName}");

            if (sameScore)
            {
                lines.Add(
                    "Every solver found the same result, so this problem is too small or too easy " +
                    "to tell them apart.");
            }
        }
        else
        {
            // Same score and the same time: there is no honest way to pick one.
            lines.Add($"Best: {names} (tie)");
            lines.Add(
                winners.Count == outcomes.Count
                    ? "Every solver found the same result in the same time, so this problem is too " +
                      "small or too easy to tell them apart."
                    : $"{names} found the same best result in the same time, so there is no clear winner.");
        }

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
