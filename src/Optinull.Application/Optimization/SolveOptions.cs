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
/// </summary>
public sealed record SolveOptions(int Seed = SolveOptions.DefaultSeed, int? Iterations = null)
{
    public const int DefaultSeed = 42;
}

/// <summary>Turns an evaluation budget into a simulated annealing cooling rate.</summary>
internal static class AnnealingBudget
{
    public const int IterationsPerTemperature = 90;

    private const double DefaultCoolingRate = 0.95;

    // Both solvers cool until the temperature is this fraction of the start.
    private const double TemperatureRatio = 1e-5;

    public static double CoolingRate(int? iterations)
    {
        if (iterations is not { } budget)
            return DefaultCoolingRate;

        var steps = Math.Max(1, budget / IterationsPerTemperature);

        return Math.Pow(TemperatureRatio, 1.0 / steps);
    }
}
