using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public class JobShopProblemTests
{
    [Fact]
    public void Constructor_StoresJobsAndMachineCount()
    {
        var job = new Job(
            0,
            [
                new JobOperation(0, 3),
                new JobOperation(1, 2)
            ]);

        var problem = new JobShopProblem(
            2,
            [job]);

        Assert.Equal(2, problem.MachineCount);
        Assert.Single(problem.Jobs);
        Assert.Equal(0, problem.Jobs[0].Id);
        Assert.Equal(2, problem.Jobs[0].Operations.Count);
    }

    [Fact]
    public void JobOperation_RejectsNegativeMachineId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobOperation(-1, 3));
    }

    [Fact]
    public void JobOperation_RejectsNonPositiveProcessingTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new JobOperation(0, 0));
    }

    [Fact]
    public void Job_RejectsEmptyOperations()
    {
        Assert.Throws<ArgumentException>(
            () => new Job(
                0,
                []));
    }

    [Fact]
    public void Problem_RejectsDuplicateJobIds()
    {
        var jobs = new[]
        {
            new Job(
                0,
                [new JobOperation(0, 3)]),

            new Job(
                0,
                [new JobOperation(1, 2)])
        };

        Assert.Throws<ArgumentException>(
            () => new JobShopProblem(
                2,
                jobs));
    }

    [Fact]
    public void Problem_RejectsUnknownMachine()
    {
        var job = new Job(
            0,
            [
                new JobOperation(2, 3)
            ]);

        Assert.Throws<ArgumentException>(
            () => new JobShopProblem(
                2,
                [job]));
    }

    [Fact]
    public void Problem_RejectsEmptyJobList()
    {
        Assert.Throws<ArgumentException>(
            () => new JobShopProblem(
                2,
                []));
    }
}
