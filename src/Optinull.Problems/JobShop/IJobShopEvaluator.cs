using Optinull.Domain.Evaluation;

namespace Optinull.Problems.JobShop;

public interface IJobShopEvaluator
{
    EvaluationResult Evaluate(
        JobShopProblem problem,
        JobSequence sequence);
}
