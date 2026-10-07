using Optinull.Domain.Evaluation;

namespace Optinull.Problems.JobShop;

public sealed class JobShopEvaluator
{
    private readonly JobShopScheduleDecoder _decoder;

    public JobShopEvaluator()
    {
        _decoder = new JobShopScheduleDecoder();
    }

    public EvaluationResult Evaluate(
        JobShopProblem problem,
        JobSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(sequence);

        var schedule = _decoder.Decode(
            problem,
            sequence);

        // Job-Shop is a minimization problem:
        // a smaller makespan means the schedule is better.
        return new EvaluationResult(
            objectiveValue: schedule.Makespan,
            isFeasible: true,
            penalty: 0.0);
    }
}
