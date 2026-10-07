using Optinull.Domain.Problems;

namespace Optinull.Optimization.Solvers;

public sealed class SolverComparison
{
    public IReadOnlyList<SolverResult> Compare(
        OptimizationProblem problem,
        IEnumerable<(string Name, IOptimizationSolver Solver)> solvers)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(solvers);

        var results = new List<SolverResult>();

        foreach (var (name, solver) in solvers)
        {
            ArgumentNullException.ThrowIfNull(solver);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var result = solver.Solve(problem);

            stopwatch.Stop();

            results.Add(
                new SolverResult(
                    name,
                    result,
                    stopwatch.Elapsed));
        }

        return results;
    }
}
