using Optinull.Domain.Problems;

namespace Optinull.Optimization.Solvers;

public interface IOptimizationSolver
{
    OptimizationResult Solve(
        OptimizationProblem problem);
}
