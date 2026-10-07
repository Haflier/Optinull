using Optinull.Domain.Evaluation;

namespace Optinull.Optimization.Comparison;

public interface ISolutionComparer
{
    bool IsBetter(
        EvaluationResult candidate,
        EvaluationResult current);
}
