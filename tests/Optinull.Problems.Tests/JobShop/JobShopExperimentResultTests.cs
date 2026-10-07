using Optinull.Problems.JobShop.Experiments;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopExperimentResultTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var elapsed = TimeSpan.FromMilliseconds(25);

        var result =
            new JobShopExperimentResult(
                solverName: "Hill Climbing",
                makespan: 11,
                elapsed: elapsed,
                initialMakespan: 15,
                optimalityGap: 0);

        Assert.Equal(
            "Hill Climbing",
            result.SolverName);

        Assert.Equal(
            11,
            result.Makespan);

        Assert.Equal(
            elapsed,
            result.Elapsed);

        Assert.Equal(
            15,
            result.InitialMakespan);

        Assert.Equal(
            0,
            result.OptimalityGap);
    }

    [Fact]
    public void Constructor_AllowsMissingOptionalValues()
    {
        var result =
            new JobShopExperimentResult(
                solverName: "Genetic Algorithm",
                makespan: 11,
                elapsed: TimeSpan.Zero);

        Assert.Null(result.InitialMakespan);
        Assert.Null(result.OptimalityGap);
    }

    [Fact]
    public void Constructor_RejectsEmptySolverName()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "",
                    makespan: 11,
                    elapsed: TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_RejectsNegativeMakespan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "Test",
                    makespan: -1,
                    elapsed: TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_RejectsNegativeElapsedTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "Test",
                    makespan: 11,
                    elapsed: TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Constructor_RejectsNegativeOptimalityGap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "Test",
                    makespan: 11,
                    elapsed: TimeSpan.Zero,
                    optimalityGap: -0.1));
    }
}
