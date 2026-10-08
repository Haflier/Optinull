using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopIteratedLocalSearchSolverTests
{
    [Fact]
    public void Solve_IsNeverWorseThanHillClimbing()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0, 0, 0, 0,
                1, 1, 1, 1, 1, 1,
                2, 2, 2, 2, 2, 2,
                3, 3, 3, 3, 3, 3,
                4, 4, 4, 4, 4, 4,
                5, 5, 5, 5, 5, 5
            ]);

        var hillClimbing =
            new JobShopHillClimbingSolver();

        var ils =
            new JobShopIteratedLocalSearchSolver(
                perturbationSwapCount: 3,
                iterationCount: 100,
                random: new Random(42));

        var hillClimbingResult =
            hillClimbing.Solve(
                problem,
                initialSequence);

        var ilsResult =
            ils.Solve(
                problem,
                initialSequence);

        Assert.True(
            ilsResult.Makespan <= hillClimbingResult.Makespan);
    }

    [Fact]
    public void Solve_CanEscapeHillClimbingLocalOptimum()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var initialSequence =
            new JobSequence(
            [
                0, 0, 0, 0, 0, 0,
                1, 1, 1, 1, 1, 1,
                2, 2, 2, 2, 2, 2,
                3, 3, 3, 3, 3, 3,
                4, 4, 4, 4, 4, 4,
                5, 5, 5, 5, 5, 5
            ]);

        var hillClimbing =
            new JobShopHillClimbingSolver();

        var ils =
            new JobShopIteratedLocalSearchSolver(
                perturbationSwapCount: 3,
                iterationCount: 100,
                random: new Random(42));

        var hillClimbingResult =
            hillClimbing.Solve(
                problem,
                initialSequence);

        var ilsResult =
            ils.Solve(
                problem,
                initialSequence);

        Assert.True(
            ilsResult.Makespan < hillClimbingResult.Makespan,
            $"Hill Climbing: {hillClimbingResult.Makespan}, " +
            $"ILS: {ilsResult.Makespan}");
    }
}
