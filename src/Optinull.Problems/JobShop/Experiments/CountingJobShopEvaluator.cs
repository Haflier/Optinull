using Optinull.Domain.Evaluation;

namespace Optinull.Problems.JobShop.Experiments;

public sealed class CountingJobShopEvaluator : IJobShopEvaluator
{
    private readonly IJobShopEvaluator _inner;

    public int EvaluationCount { get; private set; }

    public CountingJobShopEvaluator(
        IJobShopEvaluator inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    public EvaluationResult Evaluate(
        JobShopProblem problem,
        JobSequence sequence)
    {
        EvaluationCount++;

        return _inner.Evaluate(
            problem,
            sequence);
    }

    public void Reset()
    {
        EvaluationCount = 0;
    }
}
