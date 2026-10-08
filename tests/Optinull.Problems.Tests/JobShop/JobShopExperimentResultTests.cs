using Optinull.Problems.JobShop.Experiments;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopExperimentResultTests
{
    [Fact]
    public void Constructor_StoresValues()
    {
        var elapsed =
            TimeSpan.FromMilliseconds(25);

        var result =
            new JobShopExperimentResult(
                solverName: "Hill Climbing",
                makespan: 11,
                elapsed: elapsed,
                evaluationCount: 42,
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
            42,
            result.EvaluationCount);

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
                elapsed: TimeSpan.Zero,
                evaluationCount: 100);

        Assert.Null(
            result.InitialMakespan);

        Assert.Null(
            result.OptimalityGap);

        Assert.Equal(
            100,
            result.EvaluationCount);
    }

    [Fact]
    public void Constructor_RejectsEmptySolverName()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "",
                    makespan: 11,
                    elapsed: TimeSpan.Zero,
                    evaluationCount: 1));
    }

    [Fact]
    public void Constructor_RejectsNegativeMakespan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "Test",
                    makespan: -1,
                    elapsed: TimeSpan.Zero,
                    evaluationCount: 1));
    }

    [Fact]
    public void Constructor_RejectsNegativeElapsedTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "Test",
                    makespan: 11,
                    elapsed: TimeSpan.FromMilliseconds(-1),
                    evaluationCount: 1));
    }

    [Fact]
    public void Constructor_RejectsNegativeEvaluationCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopExperimentResult(
                    solverName: "Test",
                    makespan: 11,
                    elapsed: TimeSpan.Zero,
                    evaluationCount: -1));
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
                    evaluationCount: 1,
                    optimalityGap: -0.1));
    }

    [Fact]
    public void Constructor_StoresEvaluationCount()
    {
        var result =
            new JobShopExperimentResult(
                "Hill Climbing",
                11,
                TimeSpan.FromMilliseconds(10),
                42);

        Assert.Equal(
            42,
            result.EvaluationCount);
    }
}
