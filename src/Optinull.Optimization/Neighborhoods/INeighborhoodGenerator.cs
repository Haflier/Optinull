using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Neighborhoods;

public interface INeighborhoodGenerator
{
    IEnumerable<Solution> Generate(
        OptimizationProblem problem,
        Solution solution);
}
