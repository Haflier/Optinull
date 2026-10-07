using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopBenchmarkTests
{
    [Fact]
    public void Ft06_HasExpectedStructure()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        Assert.Equal(6, problem.MachineCount);
        Assert.Equal(6, problem.Jobs.Count);

        Assert.All(
            problem.Jobs,
            job => Assert.Equal(6, job.Operations.Count));
    }

    [Fact]
    public void Ft06_HasExpectedOperationCount()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var operationCount =
            problem.Jobs.Sum(job => job.Operations.Count);

        Assert.Equal(36, operationCount);
    }
}
