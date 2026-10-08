using Optinull.Problems.JobShop.Experiments;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopExperimentSummaryTests
{
    [Fact]
    public void Constructor_CalculatesStatistics()
    {
        var runs =
            new[]
            {
                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.FromMilliseconds(10),
                    100),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    13,
                    TimeSpan.FromMilliseconds(20),
                    150),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    12,
                    TimeSpan.FromMilliseconds(15),
                    125)
            };

        var summary =
            new JobShopExperimentSummary(
                "Hill Climbing",
                runs);

        Assert.Equal(
            "Hill Climbing",
            summary.SolverName);

        Assert.Equal(
            runs,
            summary.Runs);

        Assert.Equal(
            11,
            summary.BestMakespan);

        Assert.Equal(
            13,
            summary.WorstMakespan);

        Assert.Equal(
            12,
            summary.AverageMakespan);

        Assert.Equal(
            TimeSpan.FromMilliseconds(10),
            summary.BestElapsed);

        Assert.Equal(
            TimeSpan.FromMilliseconds(20),
            summary.WorstElapsed);

        Assert.Equal(
            TimeSpan.FromMilliseconds(15),
            summary.AverageElapsed);

        Assert.True(
            double.IsNaN(summary.SuccessRate));

        Assert.True(
            double.IsNaN(summary.AverageOptimalityGap));
    }

    [Fact]
    public void Constructor_CalculatesSuccessRateAndAverageOptimalityGap()
    {
        var runs =
            new[]
            {
                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    100),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    12,
                    TimeSpan.Zero,
                    110),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    120)
            };

        var summary =
            new JobShopExperimentSummary(
                "Hill Climbing",
                runs,
                knownOptimalMakespan: 11);

        Assert.Equal(
            2.0 / 3.0,
            summary.SuccessRate);

        Assert.Equal(
            (0.0 + (1.0 / 11.0) + 0.0) / 3.0,
            summary.AverageOptimalityGap);
    }

    [Fact]
    public void Constructor_AllRunsOptimal_HasFullSuccessRate()
    {
        var runs =
            new[]
            {
                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    100),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    100)
            };

        var summary =
            new JobShopExperimentSummary(
                "Hill Climbing",
                runs,
                knownOptimalMakespan: 11);

        Assert.Equal(
            1.0,
            summary.SuccessRate);

        Assert.Equal(
            0.0,
            summary.AverageOptimalityGap);
    }

    [Fact]
    public void Constructor_AllowsMissingKnownOptimalMakespan()
    {
        var runs =
            new[]
            {
                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    100)
            };

        var summary =
            new JobShopExperimentSummary(
                "Hill Climbing",
                runs);

        Assert.True(
            double.IsNaN(summary.SuccessRate));

        Assert.True(
            double.IsNaN(summary.AverageOptimalityGap));
    }

    [Fact]
    public void Constructor_RejectsEmptyRuns()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new JobShopExperimentSummary(
                    "Hill Climbing",
                    []));
    }

    [Fact]
    public void Constructor_RejectsMixedSolverNames()
    {
        var runs =
            new[]
            {
                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    100),

                new JobShopExperimentResult(
                    "Simulated Annealing",
                    12,
                    TimeSpan.Zero,
                    100)
            };

        Assert.Throws<ArgumentException>(
            () =>
                new JobShopExperimentSummary(
                    "Hill Climbing",
                    runs));
    }

    [Fact]
    public void Constructor_CalculatesAverageEvaluationCount()
    {
        var runs =
            new[]
            {
                new JobShopExperimentResult(
                    "Hill Climbing",
                    11,
                    TimeSpan.Zero,
                    100),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    12,
                    TimeSpan.Zero,
                    200),

                new JobShopExperimentResult(
                    "Hill Climbing",
                    13,
                    TimeSpan.Zero,
                    300)
            };

        var summary =
            new JobShopExperimentSummary(
                "Hill Climbing",
                runs);

        Assert.Equal(
            200,
            summary.AverageEvaluationCount);
    }
}
