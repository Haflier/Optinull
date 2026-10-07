using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Genetics;

public interface IMutationStrategy
{
    Solution Mutate(
        OptimizationProblem problem,
        Solution solution);
}
