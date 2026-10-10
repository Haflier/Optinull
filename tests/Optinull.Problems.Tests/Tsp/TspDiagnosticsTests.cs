using Optinull.Optimization.Randomness;
using Optinull.Problems.Tsp;

namespace Optinull.Problems.Tests.Tsp;

/// <summary>Checks the TSP pieces against independent calculations.</summary>
public sealed class TspDiagnosticsTests
{
    private static double LengthOf(TspProblem problem, IReadOnlyList<int> order)
    {
        var length = 0.0;

        for (var i = 0; i < order.Count; i++)
        {
            var a = problem.Cities[order[i]];
            var b = problem.Cities[order[(i + 1) % order.Count]];

            length += Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        return length;
    }

    private static bool IsPermutation(IReadOnlyList<int> order, int count) =>
        order.Count == count && order.OrderBy(city => city).SequenceEqual(Enumerable.Range(0, count));

    [Fact]
    public void Evaluate_MatchesAnIndependentLength_AndIgnoresRotationAndDirection()
    {
        var problem = TspBenchmarkInstances.RandomCities(15, seed: 4);
        var random = new RandomSource(1);
        var search = new TspSearchProblem(problem);

        var tour = search.CreateRandom(random);
        var length = TspEvaluator.Evaluate(problem, tour).ObjectiveValue;

        Assert.Equal(LengthOf(problem, tour.Cities), length, 6);

        var rotated = new TspTour(tour.Cities.Skip(4).Concat(tour.Cities.Take(4)));
        var reversed = new TspTour(tour.Cities.Reverse());

        Assert.Equal(length, TspEvaluator.Evaluate(problem, rotated).ObjectiveValue, 6);
        Assert.Equal(length, TspEvaluator.Evaluate(problem, reversed).ObjectiveValue, 6);
    }

    [Fact]
    public void Neighbors_AreAllPermutations_OfTheExpectedCount_AndEvaluateCorrectly()
    {
        const int cities = 12;

        var problem = TspBenchmarkInstances.RandomCities(cities, seed: 2);
        var search = new TspSearchProblem(problem);
        var tour = search.CreateRandom(new RandomSource(7));

        var neighbors = search.Neighbors(tour).ToList();

        // Every 2-opt reversal except the one that only mirrors the whole tour.
        Assert.Equal(cities * (cities - 1) / 2 - 1, neighbors.Count);

        foreach (var neighbor in neighbors)
        {
            Assert.True(IsPermutation(neighbor.Cities, cities));
            Assert.Equal(
                LengthOf(problem, neighbor.Cities),
                search.Evaluate(neighbor).ObjectiveValue,
                6);
        }
    }

    [Fact]
    public void RandomNeighborCrossoverAndMutation_AlwaysKeepAValidTour()
    {
        const int cities = 25;

        var problem = TspBenchmarkInstances.RandomCities(cities, seed: 8);
        var search = new TspSearchProblem(problem);
        var random = new RandomSource(11);

        var a = search.CreateRandom(random);
        var b = search.CreateRandom(random);

        for (var i = 0; i < 500; i++)
        {
            var moved = search.RandomNeighbor(a, random);

            Assert.True(IsPermutation(moved.Cities, cities));
            Assert.False(moved.Cities.SequenceEqual(a.Cities));

            var child = search.Mutate(search.Crossover(a, b, random), random);

            Assert.True(IsPermutation(child.Cities, cities));

            a = moved;
            b = child;
        }
    }

    [Fact]
    public void Greedy_StartsAtCityZero_AndAlwaysGoesToTheNearestUnvisitedCity()
    {
        var problem = TspBenchmarkInstances.RandomCities(30, seed: 5);
        var search = new TspSearchProblem(problem);

        var tour = search.CreateGreedy(search.Evaluate).Cities;

        Assert.Equal(0, tour[0]);
        Assert.True(IsPermutation(tour, 30));

        for (var step = 1; step < tour.Count; step++)
        {
            var visited = tour.Take(step).ToHashSet();

            var nearest = Enumerable
                .Range(0, 30)
                .Where(city => !visited.Contains(city))
                .Min(city => problem.Distance(tour[step - 1], city));

            Assert.Equal(nearest, problem.Distance(tour[step - 1], tour[step]), 9);
        }
    }

    [Fact]
    public void CircleAndGrid_KnownOptimaMatchTheirEvaluatedTours()
    {
        // Cities are generated in order around the circle, so the identity tour is optimal.
        var circle = TspBenchmarkInstances.Circle(20);
        var identity = new TspTour(Enumerable.Range(0, 20));

        Assert.Equal(
            TspBenchmarkInstances.CircleOptimum(20),
            TspEvaluator.Evaluate(circle, identity).ObjectiveValue,
            6);

        // An even grid has a Hamiltonian cycle of unit edges: down the first column,
        // then snake through the remaining columns of each row pair.
        var side = 6;
        var grid = TspBenchmarkInstances.Grid(side);
        var cycle = new List<int>();

        for (var x = 0; x < side; x++)
            cycle.Add(x);

        for (var y = 1; y < side; y++)
        {
            if (y % 2 == 1)
                for (var x = side - 1; x >= 1; x--)
                    cycle.Add(y * side + x);
            else
                for (var x = 1; x < side; x++)
                    cycle.Add(y * side + x);
        }

        for (var y = side - 1; y >= 1; y--)
            cycle.Add(y * side);

        Assert.True(IsPermutation(cycle, side * side));
        Assert.Equal(
            TspBenchmarkInstances.GridOptimum(side),
            TspEvaluator.Evaluate(grid, new TspTour(cycle)).ObjectiveValue,
            6);
    }
}
