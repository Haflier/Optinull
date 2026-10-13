using Optinull.Application.Optimization;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests;

public sealed class IteratedLocalSearchSolveTests
{
    [Fact]
    public void Tsp_ReturnsAValidTour()
    {
        var problem = TspBenchmarkInstances.RandomCities(30, seed: 3);
        var service = new TspSolveService();

        var result = service.Solve(problem, SolverKind.IteratedLocalSearch, seed: 3, iterations: 2_000);

        Assert.Equal("Iterated Local Search", result.SolverName);
        Assert.Equal(Enumerable.Range(0, 30), result.Tour.Cities.OrderBy(city => city));
        Assert.Equal(result.Length, result.Search.Evaluation.ObjectiveValue, 6);
    }

    [Fact]
    public void Tsp_StopsStartingNewRestartsOnceTheBudgetIsSpent()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        var result = new TspSolveService()
            .Solve(problem, SolverKind.IteratedLocalSearch, seed: 1, iterations: 1_000);

        // One climb in progress may overshoot, but the run must not go on for ever.
        Assert.InRange(result.Search.EvaluationCount, 1_000, 50_000);
    }

    [Fact]
    public void Knapsack_ReturnsAFeasibleSelection()
    {
        var problem = KnapsackInstances.Random(30, seed: 2);

        var result = new KnapsackSolveService()
            .Solve(problem, SolverKind.IteratedLocalSearch, seed: 2, iterations: 2_000);

        Assert.True(result.Weight <= problem.Capacity);
        Assert.True(result.Value > 0);
    }

    [Fact]
    public void JobShop_Ft06_ReturnsAConsistentSchedule()
    {
        var problem = JobShopBenchmarkInstances.Ft06();

        var result = new JobShopSolveService()
            .Solve(problem, SolverKind.IteratedLocalSearch, seed: 1, iterations: 2_000);

        Assert.Equal(36, result.Schedule.Operations.Count);
        Assert.True(result.Makespan >= 55);
    }

    [Fact]
    public void SameSeed_GivesTheSameTour()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 4);
        var service = new TspSolveService();

        Assert.Equal(
            service.Solve(problem, SolverKind.IteratedLocalSearch, seed: 9, iterations: 1_000).Tour.Cities,
            service.Solve(problem, SolverKind.IteratedLocalSearch, seed: 9, iterations: 1_000).Tour.Cities);
    }
}
