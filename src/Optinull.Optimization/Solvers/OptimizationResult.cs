using Optinull.Domain.Evaluation;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Solvers;

public sealed class OptimizationResult
{
    public Solution Solution { get; }

    public EvaluationResult Evaluation { get; }

    public OptimizationResult(
        Solution solution,
        EvaluationResult evaluation)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(evaluation);

        Solution = solution;
        Evaluation = evaluation;
    }
}
