namespace Optinull.Application.Optimization;

/// <summary>Limits for what a user may ask for in /solve.</summary>
public static class SolveLimits
{
    public const int MinIterations = 100;

    public const int MaxIterations = 100_000;
}

/// <summary>
/// Per-run settings from the chat. Iterations is the number of candidate
/// solutions the solver may evaluate; null keeps each solver's own default.
/// Progress, when given, receives 0..1 while a budgeted solver runs.
/// </summary>
public sealed record SolveOptions(
    int Seed = SolveOptions.DefaultSeed,
    int? Iterations = null,
    IProgress<double>? Progress = null,
    SolverTuning? Tuning = null)
{
    public const int DefaultSeed = 42;

    /// <summary>The evaluation budget the annealing and genetic solvers use by default.</summary>
    public const int DefaultBudget = 20_000;
}

/// <summary>Turns an evaluation budget into a simulated annealing cooling rate.</summary>
internal static class AnnealingBudget
{
    public const int IterationsPerTemperature = 90;

    private const double DefaultCoolingRate = 0.95;

    /// <summary>The solvers cool until the temperature is this fraction of the start.</summary>
    public const double DefaultFinalRatio = 1e-5;

    private const double TemperatureRatio = DefaultFinalRatio;

    public static double CoolingRate(int? iterations)
    {
        if (iterations is not { } budget)
            return DefaultCoolingRate;

        var steps = Math.Max(1, budget / IterationsPerTemperature);

        return Math.Pow(TemperatureRatio, 1.0 / steps);
    }

    /// <summary>
    /// The cooling rate for a user-chosen schedule. The budget still decides how long the
    /// run is: it holds budget / moves temperature levels, ending at the final fraction.
    /// </summary>
    public static double CoolingRate(int? iterations, int moves, double finalRatio)
    {
        // The default schedule keeps its exact, long-standing rate.
        if (iterations is null && moves == IterationsPerTemperature && finalRatio == DefaultFinalRatio)
            return DefaultCoolingRate;

        var budget = iterations ?? SolveOptions.DefaultBudget;
        var steps = Math.Max(1, budget / moves);

        return Math.Pow(finalRatio, 1.0 / steps);
    }
}
