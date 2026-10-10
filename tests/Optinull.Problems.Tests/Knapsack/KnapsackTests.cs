using Optinull.Optimization.Randomness;
using Optinull.Problems.Knapsack;

namespace Optinull.Problems.Tests.Knapsack;

public sealed class KnapsackTests
{
    // The classic example: take the 100 and 120 items for 220.
    private static KnapsackProblem Classic() =>
        new([new(60, 10), new(100, 20), new(120, 30)], capacity: 50);

    [Fact]
    public void Evaluate_SumsValue_AndFlagsOverweight()
    {
        var problem = Classic();

        var fits = KnapsackEvaluator.Evaluate(problem, new KnapsackSelection([false, true, true]));
        Assert.Equal(220, fits.ObjectiveValue);
        Assert.True(fits.IsFeasible);
        Assert.Equal(0, fits.Penalty);

        var over = KnapsackEvaluator.Evaluate(problem, new KnapsackSelection([true, true, true]));
        Assert.Equal(280, over.ObjectiveValue);
        Assert.False(over.IsFeasible);
        Assert.Equal(10, over.Penalty);
    }

    [Fact]
    public void Evaluate_WrongLength_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            KnapsackEvaluator.Evaluate(Classic(), new KnapsackSelection([true, false])));

    [Fact]
    public void ExactSolver_FindsTheOptimum() =>
        Assert.Equal(220, KnapsackExactSolver.Optimum(Classic()));

    [Fact]
    public void Greedy_IsFeasible_AndNotBetterThanTheOptimum()
    {
        var problem = KnapsackInstances.Random(30, seed: 3);
        var search = new KnapsackSearchProblem(problem);

        var greedy = KnapsackEvaluator.Evaluate(problem, search.CreateGreedy(search.Evaluate));

        Assert.True(greedy.IsFeasible);
        Assert.True(greedy.ObjectiveValue <= KnapsackExactSolver.Optimum(problem));
    }

    [Fact]
    public void RandomSolutionsAndMoves_AlwaysFitTheCapacity()
    {
        var problem = KnapsackInstances.Random(40, seed: 5);
        var search = new KnapsackSearchProblem(problem);
        var random = new RandomSource(1);

        var current = search.CreateRandom(random);
        var other = search.CreateRandom(random);

        for (var i = 0; i < 500; i++)
        {
            Assert.True(search.Evaluate(current).IsFeasible);

            current = search.RandomNeighbor(current, random);
            other = search.Mutate(search.Crossover(current, other, random), random);

            Assert.True(search.Evaluate(other).IsFeasible);
        }
    }

    [Fact]
    public void Neighbors_AreAllFeasible_AndDifferFromTheSolution()
    {
        var problem = KnapsackInstances.Random(15, seed: 2);
        var search = new KnapsackSearchProblem(problem);
        var start = search.CreateRandom(new RandomSource(4));

        var neighbors = search.Neighbors(start).ToList();

        Assert.NotEmpty(neighbors);
        Assert.All(neighbors, neighbor =>
        {
            Assert.True(search.Evaluate(neighbor).IsFeasible);
            Assert.NotEqual(start.Flags, neighbor.Flags);
        });
    }

    [Fact]
    public void ItemHeavierThanTheCapacity_IsNeverPacked()
    {
        var problem = new KnapsackProblem([new(10, 5), new(1000, 99)], capacity: 10);
        var search = new KnapsackSearchProblem(problem);
        var random = new RandomSource(1);

        var current = search.CreateRandom(random);

        for (var i = 0; i < 100; i++)
        {
            current = search.RandomNeighbor(current, random);

            Assert.False(current[1]);
        }
    }

    [Fact]
    public void RandomInstances_AreDeterministic_AndEveryItemFitsAlone()
    {
        var first = KnapsackInstances.Random(25, seed: 8);
        var second = KnapsackInstances.Random(25, seed: 8);

        Assert.Equal(first.Items, second.Items);
        Assert.Equal(first.Capacity, second.Capacity);
        Assert.All(first.Items, item => Assert.True(item.Weight <= first.Capacity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Problem_RejectsNonPositiveCapacity(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KnapsackProblem([new(1, 1)], capacity));

    [Fact]
    public void Problem_RejectsEmptyOrNonPositiveItems()
    {
        Assert.Throws<ArgumentException>(() => new KnapsackProblem([], 10));
        Assert.Throws<ArgumentException>(() => new KnapsackProblem([new(0, 3)], 10));
        Assert.Throws<ArgumentException>(() => new KnapsackProblem([new(3, 0)], 10));
    }
}
