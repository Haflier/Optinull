using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsScheduleMakespan()
    {
        var problem = CreateProblem();

        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var evaluator =
            new JobShopEvaluator();

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
    public void Evaluate_ReturnsFeasibleResult()
    {
        var problem = CreateProblem();

        var sequence =
            new JobSequence([1, 0, 1, 0]);

        var evaluator =
            new JobShopEvaluator();

        var result =
            evaluator.Evaluate(
                problem,
                sequence);

        Assert.True(result.IsFeasible);
    }

    [Fact]
    public void Evaluate_RejectsInvalidSequence()
    {
        var problem = CreateProblem();

        var invalidSequence =
            new JobSequence([0, 1, 0]);

        var evaluator =
            new JobShopEvaluator();

        Assert.Throws<ArgumentException>(
            () => evaluator.Evaluate(
                problem,
                invalidSequence));
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
