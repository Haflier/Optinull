using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Generation;

public interface ISolutionGenerator
{
    Solution Generate(
        OptimizationProblem problem,
        IRandomSource random);
}
