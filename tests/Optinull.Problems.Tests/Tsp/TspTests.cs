using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Tsp;

namespace Optinull.Problems.Tests.Tsp;

public sealed class TspTests
{
    private static readonly TspProblem Square = new(
    [
        new City(0, 0), new City(10, 0), new City(10, 10), new City(0, 10)
    ]);

    private static void AssertPermutation(TspTour tour, int count)
    {
        Assert.Equal(count, tour.Count);
        Assert.Equal(Enumerable.Range(0, count), tour.Cities.OrderBy(c => c));
    }

    [Fact]
    public void Problem_NeedsAtLeastThreeCities() =>
        Assert.Throws<ArgumentException>(() =>
            new TspProblem([new City(0, 0), new City(1, 1)]));

    [Fact]
    public void Problem_RejectsNonFiniteCoordinates() =>
        Assert.Throws<ArgumentException>(() =>
            new TspProblem([new City(0, 0), new City(1, 1), new City(double.NaN, 2)]));

    [Fact]
    public void Distance_IsSymmetric_AndZeroToSelf()
    {
        Assert.Equal(10, Square.Distance(0, 1));
        Assert.Equal(Square.Distance(0, 2), Square.Distance(2, 0));
        Assert.Equal(0, Square.Distance(3, 3));
    }

    [Fact]
    public void Evaluate_SquareTours()
    {
        var perimeter = TspEvaluator.Evaluate(Square, new TspTour([0, 1, 2, 3]));
        var crossing = TspEvaluator.Evaluate(Square, new TspTour([0, 2, 1, 3]));

        Assert.Equal(40, perimeter.ObjectiveValue, 9);
        Assert.True(perimeter.IsFeasible);
        Assert.Equal(0, perimeter.Penalty);
        Assert.True(crossing.ObjectiveValue > perimeter.ObjectiveValue);
    }

    [Theory]
    [InlineData(new[] { 0, 1, 2 })]
    [InlineData(new[] { 0, 1, 2, 2 })]
    [InlineData(new[] { 0, 1, 2, 7 })]
    public void Evaluate_InvalidTour_Throws(int[] cities) =>
        Assert.Throws<ArgumentException>(() =>
            TspEvaluator.Evaluate(Square, new TspTour(cities)));

    [Fact]
    public void CreateRandom_IsAPermutation_AndReproducible()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(15));

        var first = problem.CreateRandom(new RandomSource(5));
        var second = problem.CreateRandom(new RandomSource(5));

        AssertPermutation(first, 15);
        Assert.Equal(first.Cities, second.Cities);
    }

    [Fact]
    public void RandomNeighbor_IsADifferentPermutation()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(10));
        var random = new RandomSource(1);
        var tour = problem.CreateRandom(random);

        for (var i = 0; i < 200; i++)
        {
            var neighbor = problem.RandomNeighbor(tour, random);

            AssertPermutation(neighbor, 10);
            Assert.NotEqual(tour.Cities, neighbor.Cities);
        }
    }

    [Fact]
    public void Neighbors_AreAllTwoOptMoves_ExceptTheFullReversal()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(8));
        var tour = new TspTour(Enumerable.Range(0, 8));

        var neighbors = problem.Neighbors(tour).ToList();

        // 8 * 7 / 2 pairs, minus the one that reverses the whole tour.
        Assert.Equal(27, neighbors.Count);
        Assert.All(neighbors, neighbor => AssertPermutation(neighbor, 8));
    }

    [Fact]
    public void Crossover_AlwaysProducesAPermutation()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(12));
        var random = new RandomSource(3);

        for (var i = 0; i < 300; i++)
        {
            var child = problem.Crossover(
                problem.CreateRandom(random),
                problem.CreateRandom(random),
                random);

            AssertPermutation(child, 12);
        }
    }

    [Fact]
    public void Crossover_DifferentLengths_Throws()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(5));

        Assert.Throws<ArgumentException>(() =>
            problem.Crossover(
                new TspTour([0, 1, 2, 3, 4]),
                new TspTour([0, 1, 2, 3]),
                new RandomSource(1)));
    }

    [Fact]
    public void Circle_OptimumIsThePolygonPerimeter()
    {
        var instance = TspBenchmarkInstances.Circle(12);
        var polygon = TspEvaluator.Evaluate(instance, new TspTour(Enumerable.Range(0, 12)));

        Assert.Equal(TspBenchmarkInstances.CircleOptimum(12), polygon.ObjectiveValue, 6);
    }

    [Fact]
    public void Grid_HasSideSquaredCities_AndRejectsOddSides()
    {
        Assert.Equal(36, TspBenchmarkInstances.Grid(6).CityCount);
        Assert.Equal(360, TspBenchmarkInstances.GridOptimum(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TspBenchmarkInstances.Grid(5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void HillClimbing_OnACircle_FindsTheOptimum(int seed)
    {
        // Points in convex position have no crossing-free tour except the hull,
        // and 2-opt removes every crossing, so any local optimum is optimal.
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(12));
        var start = problem.CreateRandom(new RandomSource(seed));

        var result = new HillClimbing<TspTour>().Solve(problem, start);

        Assert.Equal(
            TspBenchmarkInstances.CircleOptimum(12),
            result.Evaluation.ObjectiveValue,
            6);
    }

    [Fact]
    public void GeneticAlgorithm_OnACircle_GetsCloseToTheOptimum()
    {
        var problem = new TspSearchProblem(TspBenchmarkInstances.Circle(15));
        var optimum = TspBenchmarkInstances.CircleOptimum(15);

        var result = new GeneticAlgorithm<TspTour>(
                populationSize: 30,
                generations: 200,
                eliteCount: 2,
                mutationRate: 0.2,
                localSearchTries: 20,
                random: new RandomSource(42))
            .Solve(problem);

        Assert.True(
            result.Evaluation.ObjectiveValue <= optimum * 1.10,
            $"Expected within 10% of {optimum:F1}, got {result.Evaluation.ObjectiveValue:F1}.");
    }
}
