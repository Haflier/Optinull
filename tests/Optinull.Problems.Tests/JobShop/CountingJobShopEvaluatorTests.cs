using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Experiments;

namespace Optinull.Problems.Tests.JobShop;

public sealed class CountingJobShopEvaluatorTests
{
    [Fact]
    public void Evaluate_IncrementsEvaluationCount()
    {
        var problem = CreateProblem();

        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var evaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        Assert.Equal(
            0,
            evaluator.EvaluationCount);

        evaluator.Evaluate(
            problem,
            sequence);

        Assert.Equal(
            1,
            evaluator.EvaluationCount);

        evaluator.Evaluate(
            problem,
            sequence);

        Assert.Equal(
            2,
            evaluator.EvaluationCount);
    }

    [Fact]
    public void Evaluate_ReturnsInnerEvaluatorResult()
    {
        var problem = CreateProblem();

        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var evaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        var result =
            evaluator.Evaluate(
                problem,
                sequence);

        Assert.True(result.IsFeasible);

        Assert.Equal(
            6.0,
            result.ObjectiveValue);

        Assert.Equal(
            0.0,
            result.Penalty);
    }

    [Fact]
    public void Reset_SetsEvaluationCountToZero()
    {
        var problem = CreateProblem();

        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var evaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        evaluator.Evaluate(
            problem,
            sequence);

        evaluator.Evaluate(
            problem,
            sequence);

        Assert.Equal(
            2,
            evaluator.EvaluationCount);

        evaluator.Reset();

        Assert.Equal(
            0,
            evaluator.EvaluationCount);
    }

    private static JobShopProblem CreateProblem()
    {
        var job0 = new Job(
            0,
            [
                new JobOperation(
                    machineId: 0,
                    processingTime: 3),

                new JobOperation(
                    machineId: 1,
                    processingTime: 2)
            ]);

        var job1 = new Job(
            1,
            [
                new JobOperation(
                    machineId: 1,
                    processingTime: 2),

                new JobOperation(
                    machineId: 0,
                    processingTime: 3)
            ]);

        return new JobShopProblem(
            machineCount: 2,
            jobs: [job0, job1]);
    }
}
