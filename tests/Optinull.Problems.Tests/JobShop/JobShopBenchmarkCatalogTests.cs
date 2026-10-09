using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopBenchmarkCatalogTests
{
    [Fact]
    public void Names_ContainEveryBenchmarkThatCanBeLookedUp() =>
        Assert.All(JobShopBenchmarkCatalog.Names, name =>
            Assert.True(JobShopBenchmarkCatalog.TryGet(name, out _)));

    [Theory]
    [InlineData("ft06", 6, 55)]
    [InlineData("FT10", 10, 930)]
    [InlineData("  ft06 ", 6, 55)]
    public void TryGet_ReturnsProblemAndKnownOptimum(
        string name,
        int machines,
        double optimum)
    {
        Assert.True(JobShopBenchmarkCatalog.TryGet(name, out var benchmark));
        Assert.Equal(machines, benchmark.Problem.MachineCount);
        Assert.Equal(optimum, benchmark.KnownOptimum);
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalse() =>
        Assert.False(JobShopBenchmarkCatalog.TryGet("la01", out _));
}
