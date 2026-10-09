using Optinull.Problems.Tsp;

namespace Optinull.Problems.Tests.Tsp;

public sealed class TspGreedyTests
{
    [Fact]
    public void NearestNeighbour_OnACircle_FollowsThePolygon()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(12));
        var tour = problem.CreateGreedy(problem.Evaluate);

        Assert.Equal(Enumerable.Range(0, 12), tour.Cities.OrderBy(c => c));
        Assert.Equal(
            TspBenchmarkInstances.CircleOptimum(12),
            problem.Evaluate(tour).ObjectiveValue,
            6);
    }

    [Fact]
    public void NearestNeighbour_OnRandomCities_IsAValidTour()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.RandomCities(40, seed: 1));
        var tour = problem.CreateGreedy(problem.Evaluate);

        Assert.Equal(Enumerable.Range(0, 40), tour.Cities.OrderBy(c => c));
        Assert.Equal(0, tour.Cities[0]);
    }

    [Fact]
    public void RandomCities_AreReproducible()
    {
        var first = TspBenchmarkInstances.RandomCities(10, seed: 7);
        var second = TspBenchmarkInstances.RandomCities(10, seed: 7);

        Assert.Equal(first.Cities, second.Cities);
    }
}
