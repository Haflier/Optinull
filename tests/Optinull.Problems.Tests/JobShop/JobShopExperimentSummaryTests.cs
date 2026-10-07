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
                    "Simulated Annealing",
                    makespan: 11,
                    elapsed: TimeSpan.FromMilliseconds(10)),

                new JobShopExperimentResult(
                    "Simulated Annealing",
                    makespan: 13,
                    elapsed: TimeSpan.FromMilliseconds(20)),

                new JobShopExperimentResult(
                    "Simulated Annealing",
                    makespan: 12,
                    elapsed: TimeSpan.FromMilliseconds(30))
            };

        var summary =
            new JobShopExperimentSummary(
                "Simulated Annealing",
                runs,
                knownOptimalMakespan: 11);

        Assert.Equal(
            "Simulated Annealing",
            summary.SolverName);

        Assert.Equal(
            3,
            summary.Runs.Count);

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
            1.0 / 3.0,
            summary.SuccessRate,
            precision: 10);

        Assert.Equal(
            ((0.0 + (2.0 / 11.0) + (1.0 / 11.0)) / 3.0),
            summary.AverageOptimalityGap,
            precision: 10);

        Assert.Equal(
            TimeSpan.FromMilliseconds(10),
            summary.BestElapsed);

        Assert.Equal(
            TimeSpan.FromMilliseconds(30),
            summary.WorstElapsed);

        Assert.Equal(
            TimeSpan.FromMilliseconds(20),
            summary.AverageElapsed);
    }

    [Fact]
    public void Constructor_ReturnsFullSuccess_WhenEveryRunReachesOptimum()
    {
        var runs =
            new[]
            {
                CreateRun(11),
                CreateRun(11),
                CreateRun(11)
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
    public void Constructor_AllowsMissingKnownOptimum()
    {
        var summary =
            new JobShopExperimentSummary(
                "Hill Climbing",
                [CreateRun(11)]);

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
    public void Constructor_RejectsRunsFromDifferentSolver()
    {
        var runs =
            new[]
            {
                CreateRun(11),
                new JobShopExperimentResult(
                    "Genetic Algorithm",
                    11,
                    TimeSpan.Zero)
            };

        Assert.Throws<ArgumentException>(
            () =>
                new JobShopExperimentSummary(
                    "Hill Climbing",
                    runs));
    }

    private static JobShopExperimentResult CreateRun(
        double makespan)
    {
        return new JobShopExperimentResult(
            "Hill Climbing",
            makespan,
            TimeSpan.Zero);
    }
}
