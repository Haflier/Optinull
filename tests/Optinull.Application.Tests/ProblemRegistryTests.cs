using Optinull.Application.Problems;

namespace Optinull.Application.Tests;

public sealed class ProblemRegistryTests
{
    [Fact]
    public void Default_HasJobShopAndTsp()
    {
        var registry = ProblemRegistry.CreateDefault();

        Assert.NotNull(registry.FindModule("jobshop"));
        Assert.NotNull(registry.FindModule("TSP"));
        Assert.Null(registry.FindModule("knapsack"));
    }

    [Theory]
    [InlineData("ft06")]
    [InlineData("FT10")]
    [InlineData("circle20")]
    [InlineData("grid8")]
    [InlineData("random50")]
    public void BuiltInNames_AreFoundAcrossModules(string name) =>
        Assert.True(ProblemRegistry.CreateDefault().TryGetBuiltIn(name, out _));

    [Fact]
    public void EveryListedBuiltInName_CanBeLookedUp()
    {
        var registry = ProblemRegistry.CreateDefault();

        Assert.All(registry.BuiltInNames, name =>
            Assert.True(registry.TryGetBuiltIn(name, out _)));
    }

    [Fact]
    public void UnknownName_IsNotFound() =>
        Assert.False(ProblemRegistry.CreateDefault().TryGetBuiltIn("la01", out _));

    [Fact]
    public void DuplicateNames_AreRejected() =>
        Assert.Throws<InvalidOperationException>(() =>
            new ProblemRegistry([new JobShopModule(), new JobShopModule()]));
}
