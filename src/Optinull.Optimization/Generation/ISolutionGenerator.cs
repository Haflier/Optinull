using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Generation;

public interface ISolutionGenerator
{
    Solution Generate(
        OptimizationProblem problem,
        Random random);
}
