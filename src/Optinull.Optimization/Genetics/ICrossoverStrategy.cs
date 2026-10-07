using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Genetics;

public interface ICrossoverStrategy
{
    Solution Crossover(
        OptimizationProblem problem,
        Solution firstParent,
        Solution secondParent);
}
