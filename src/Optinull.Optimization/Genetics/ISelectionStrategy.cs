using Optinull.Domain.Evaluation;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Genetics;

public interface ISelectionStrategy
{
    Solution Select(
        Population population,
        IReadOnlyDictionary<Solution, EvaluationResult> evaluations);
}
