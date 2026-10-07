namespace Optinull.Problems.JobShop.Experiments;

public sealed class JobShopExperimentResult
{
    public string SolverName { get; }

    public double Makespan { get; }

    public TimeSpan Elapsed { get; }

    public double? InitialMakespan { get; }

    public double? OptimalityGap { get; }

    public JobShopExperimentResult(
        string solverName,
        double makespan,
        TimeSpan elapsed,
        double? initialMakespan = null,
        double? optimalityGap = null)
    {
        if (string.IsNullOrWhiteSpace(solverName))
        {
            throw new ArgumentException(
                "Solver name cannot be empty.",
                nameof(solverName));
        }

        if (makespan < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(makespan),
                "Makespan cannot be negative.");
        }

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed),
                "Elapsed time cannot be negative.");
        }

        if (initialMakespan is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialMakespan),
                "Initial makespan cannot be negative.");
        }

        if (optimalityGap is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(optimalityGap),
                "Optimality gap cannot be negative.");
        }

        SolverName = solverName;
        Makespan = makespan;
        Elapsed = elapsed;
        InitialMakespan = initialMakespan;
        OptimalityGap = optimalityGap;
    }
}
