using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopRandomInstancesTests
{
    [Fact]
    public void Generate_EveryJobVisitsEveryMachineOnce()
    {
        var problem = JobShopRandomInstances.Generate(jobCount: 5, machineCount: 4, seed: 1);

        Assert.Equal(5, problem.Jobs.Count);
        Assert.Equal(4, problem.MachineCount);

        Assert.All(problem.Jobs, job =>
            Assert.Equal(
                Enumerable.Range(0, 4),
                job.Operations.Select(operation => operation.MachineId).OrderBy(id => id)));
    }

    [Fact]
    public void Generate_SameSeed_GivesTheSameProblem()
    {
        static IEnumerable<(int, double)> Flatten(JobShopProblem problem) =>
            problem.Jobs.SelectMany(job =>
                job.Operations.Select(operation => (operation.MachineId, operation.ProcessingTime)));

        Assert.Equal(
            Flatten(JobShopRandomInstances.Generate(4, 3, seed: 9)),
            Flatten(JobShopRandomInstances.Generate(4, 3, seed: 9)));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public void Generate_RejectsEmptySizes(int jobs, int machines) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JobShopRandomInstances.Generate(jobs, machines, seed: 1));
}
