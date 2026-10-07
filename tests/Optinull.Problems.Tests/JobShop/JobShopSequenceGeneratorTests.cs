using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopSequenceGeneratorTests
{
    [Fact]
    public void Generate_CreatesSequenceWithCorrectJobCounts()
    {
        var problem = CreateProblem();

        var generator =
            new JobShopSequenceGenerator();

        var sequence =
            generator.Generate(
                problem,
                new Random(42));

        Assert.Equal(
            4,
            sequence.Count);

        Assert.Equal(
            2,
            sequence.JobIds.Count(id => id == 0));

        Assert.Equal(
            2,
            sequence.JobIds.Count(id => id == 1));
    }

    [Fact]
    public void Generate_CreatesValidSequence()
    {
        var problem = CreateProblem();

        var generator =
            new JobShopSequenceGenerator();

        var sequence =
            generator.Generate(
                problem,
                new Random(42));

        var evaluator =
            new JobShopEvaluator();

        var result =
            evaluator.Evaluate(
                problem,
                sequence);

        Assert.True(result.IsFeasible);
    }

    [Fact]
    public void Generate_WithSameSeed_IsReproducible()
    {
        var problem = CreateProblem();

        var generator =
            new JobShopSequenceGenerator();

        var first =
            generator.Generate(
                problem,
                new Random(42));

        var second =
            generator.Generate(
                problem,
                new Random(42));

        Assert.Equal(
            first.JobIds,
            second.JobIds);
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
