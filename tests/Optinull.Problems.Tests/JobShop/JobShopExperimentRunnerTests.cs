using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Experiments;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopExperimentRunnerTests
{
    [Fact]
    public void Run_ReturnsResultForEverySolver()
    {
        var problem =
            CreateThreeByThreeProblem();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var runner =
            new JobShopExperimentRunner();

        var results =
            runner.Run(
                problem,
                initialSequence,
                knownOptimalMakespan: 11);

        Assert.Equal(
            3,
            results.Count);

        Assert.Contains(
            results,
            result => result.SolverName == "Hill Climbing");

        Assert.Contains(
            results,
            result => result.SolverName == "Simulated Annealing");

        Assert.Contains(
            results,
            result => result.SolverName == "Genetic Algorithm");
    }

    [Fact]
    public void Run_UsesSameInitialMakespanForHillClimbingAndSimulatedAnnealing()
    {
        var problem =
            CreateThreeByThreeProblem();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var runner =
            new JobShopExperimentRunner();

        var results =
            runner.Run(
                problem,
                initialSequence,
                knownOptimalMakespan: 11);

        var hillClimbing =
            results.Single(
                result =>
                    result.SolverName == "Hill Climbing");

        var simulatedAnnealing =
            results.Single(
                result =>
                    result.SolverName == "Simulated Annealing");

        Assert.Equal(
            hillClimbing.InitialMakespan,
            simulatedAnnealing.InitialMakespan);

        Assert.Equal(
            11,
            hillClimbing.Makespan);

        Assert.Equal(
            11,
            simulatedAnnealing.Makespan);
    }

    [Fact]
    public void Run_CalculatesZeroOptimalityGap_WhenOptimalMakespanIsReached()
    {
        var problem =
            CreateThreeByThreeProblem();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var runner =
            new JobShopExperimentRunner();

        var results =
            runner.Run(
                problem,
                initialSequence,
                knownOptimalMakespan: 11);

        Assert.All(
            results,
            result =>
                Assert.Equal(
                    0.0,
                    result.OptimalityGap));
    }

    [Fact]
    public void RunMultiple_ReturnsSummaryForEverySolver()
    {
        var problem =
            CreateThreeByThreeProblem();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0,
              1, 1, 1,
              2, 2, 2
            ]);

        var runner =
            new JobShopExperimentRunner();

        var summaries =
            runner.RunMultiple(
                problem,
                initialSequence,
                runCount: 5,
                knownOptimalMakespan: 11,
                startingRandomSeed: 42);

        Assert.Equal(
            3,
            summaries.Count);

        Assert.All(
            summaries,
            summary =>
            {
                Assert.Equal(
                    5,
                    summary.Runs.Count);

                Assert.True(
                    summary.BestMakespan >= 11);

                Assert.True(
                    summary.WorstMakespan >=
                    summary.BestMakespan);

                Assert.True(
                    summary.AverageMakespan >=
                    summary.BestMakespan);
            });
    }

    [Fact]
    public void Run_IsReproducibleForStochasticSolvers()
    {
        var problem =
            CreateThreeByThreeProblem();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var runner =
            new JobShopExperimentRunner();

        var first =
            runner.Run(
                problem,
                initialSequence,
                knownOptimalMakespan: 11,
                randomSeed: 42);

        var second =
            runner.Run(
                problem,
                initialSequence,
                knownOptimalMakespan: 11,
                randomSeed: 42);

        Assert.Equal(
            first.Select(result => result.Makespan),
            second.Select(result => result.Makespan));
    }

    private static JobShopProblem CreateThreeByThreeProblem()
    {
        var job0 =
            new Job(
                0,
                [
                    new JobOperation(0, 3),
                    new JobOperation(1, 2),
                    new JobOperation(2, 2)
                ]);

        var job1 =
            new Job(
                1,
                [
                    new JobOperation(0, 2),
                    new JobOperation(2, 1),
                    new JobOperation(1, 4)
                ]);

        var job2 =
            new Job(
                2,
                [
                    new JobOperation(1, 4),
                    new JobOperation(2, 3),
                    new JobOperation(0, 2)
                ]);

        return new JobShopProblem(
            machineCount: 3,
            jobs:
            [
                job0,
                job1,
                job2
            ]);
    }
}
