namespace Optinull.Problems.JobShop.Experiments;

public sealed class JobShopExperimentSummary
{
    public string SolverName { get; }

    public IReadOnlyList<JobShopExperimentResult> Runs { get; }

    public double BestMakespan { get; }

    public double WorstMakespan { get; }

    public double AverageMakespan { get; }

    public double SuccessRate { get; }

    public double AverageOptimalityGap { get; }

    public TimeSpan BestElapsed { get; }

    public TimeSpan WorstElapsed { get; }

    public TimeSpan AverageElapsed { get; }

    public JobShopExperimentSummary(
        string solverName,
        IEnumerable<JobShopExperimentResult> runs,
        double? knownOptimalMakespan = null)
    {
        if (string.IsNullOrWhiteSpace(solverName))
        {
            throw new ArgumentException(
                "Solver name cannot be empty.",
                nameof(solverName));
        }

        ArgumentNullException.ThrowIfNull(runs);

        var runList = runs.ToList();

        if (runList.Count == 0)
        {
            throw new ArgumentException(
                "At least one experiment run is required.",
                nameof(runs));
        }

        if (runList.Any(run =>
                !string.Equals(
                    run.SolverName,
                    solverName,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "All runs must belong to the specified solver.",
                nameof(runs));
        }

        if (knownOptimalMakespan is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(knownOptimalMakespan),
                "Known optimal makespan must be greater than zero.");
        }

        SolverName = solverName;
        Runs = runList;

        BestMakespan =
            runList.Min(run => run.Makespan);

        WorstMakespan =
            runList.Max(run => run.Makespan);

        AverageMakespan =
            runList.Average(run => run.Makespan);

        BestElapsed =
            runList.Min(run => run.Elapsed);

        WorstElapsed =
            runList.Max(run => run.Elapsed);

        AverageElapsed =
            TimeSpan.FromTicks(
                (long)runList.Average(
                    run => run.Elapsed.Ticks));

        if (knownOptimalMakespan.HasValue)
        {
            SuccessRate =
                runList.Count(run =>
                    run.Makespan ==
                    knownOptimalMakespan.Value)
                / (double)runList.Count;

            AverageOptimalityGap =
                runList.Average(
                    run =>
                        (run.Makespan -
                         knownOptimalMakespan.Value)
                        / knownOptimalMakespan.Value);
        }
        else
        {
            SuccessRate = double.NaN;
            AverageOptimalityGap = double.NaN;
        }
    }
}
