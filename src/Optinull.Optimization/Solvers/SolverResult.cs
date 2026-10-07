using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Solvers;

public sealed class SolverResult
{
    public string SolverName { get; }
    public OptimizationResult Result { get; }
    public TimeSpan Elapsed { get; }

    public Solution Solution => Result.Solution;

    public SolverResult(
        string solverName,
        OptimizationResult result,
        TimeSpan elapsed)
    {
        if (string.IsNullOrWhiteSpace(solverName))
        {
            throw new ArgumentException(
                "Solver name cannot be empty.",
                nameof(solverName));
        }

        ArgumentNullException.ThrowIfNull(result);

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsed),
                "Elapsed time cannot be negative.");
        }

        SolverName = solverName;
        Result = result;
        Elapsed = elapsed;
    }
}
