using Optinull.Optimization.Randomness;
using Optinull.Problems.BinPacking;

namespace Optinull.Problems.Tests.BinPacking;

public sealed class BinPackingTests
{
    // Three bins can be filled exactly: 60+40, 50+50, 30+70.
    private static BinPackingProblem Small() => new([60, 40, 50, 50, 30, 70], capacity: 100);

    [Fact]
    public void Problem_RejectsBadInput()
    {
        Assert.ThrowsAny<ArgumentException>(() => new BinPackingProblem([1], capacity: 0));
        Assert.ThrowsAny<ArgumentException>(() => new BinPackingProblem([], capacity: 10));
        Assert.ThrowsAny<ArgumentException>(() => new BinPackingProblem([0, 5], capacity: 10));
        Assert.ThrowsAny<ArgumentException>(() => new BinPackingProblem([11], capacity: 10));
    }

    [Fact]
    public void LowerBound_IsTotalSizeOverCapacity_RoundedUp()
    {
        Assert.Equal(3, Small().LowerBound);
        Assert.Equal(2, new BinPackingProblem([1, 1, 1], capacity: 2).LowerBound);
    }

    [Fact]
    public void Plan_NumbersBinsByFirstAppearance_AndDropsGaps()
    {
        var plan = new BinPackingPlan([5, 5, 2, 9]);

        Assert.Equal(new[] { 0, 0, 1, 2 }, plan.Assignment);
        Assert.Equal(3, plan.BinCount);
        Assert.Equal(new[] { 0, 1 }, plan.Bins()[0]);
    }

    [Fact]
    public void Plan_FromBins_RoundTrips_AndRejectsMissingOrRepeatedItems()
    {
        var plan = BinPackingPlan.FromBins([[2, 0], [1]], itemCount: 3);

        Assert.Equal(new[] { 0, 1, 0 }, plan.Assignment);

        Assert.Throws<ArgumentException>(() => BinPackingPlan.FromBins([[0], [0, 1]], itemCount: 2));
        Assert.Throws<ArgumentException>(() => BinPackingPlan.FromBins([[0]], itemCount: 2));
    }

    [Fact]
    public void Evaluate_CountsBins_AndBreaksTiesTowardUnevenFill()
    {
        var problem = Small();

        var perfect = BinPackingEvaluator.Evaluate(problem, new BinPackingPlan([0, 0, 1, 1, 2, 2]));
        Assert.Equal(3.0, perfect.ObjectiveValue, 9);
        Assert.True(perfect.IsFeasible);

        // Loads 100, 50, 80, 70: fill squares 1 + .25 + .64 + .49, so 4 + 1 - 2.38 / 4.
        var four = BinPackingEvaluator.Evaluate(problem, new BinPackingPlan([0, 0, 1, 2, 2, 3]));
        Assert.Equal(4.405, four.ObjectiveValue, 9);
        Assert.True(four.IsFeasible);
    }

    [Fact]
    public void Evaluate_OverfullBin_IsInfeasible_WithTheOverflowAsPenalty()
    {
        var result = BinPackingEvaluator.Evaluate(Small(), new BinPackingPlan([0, 0, 0, 0, 0, 0]));

        Assert.False(result.IsFeasible);
        Assert.Equal(200, result.Penalty);
    }

    [Fact]
    public void Evaluate_WrongLength_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            BinPackingEvaluator.Evaluate(Small(), new BinPackingPlan([0, 1])));

    [Fact]
    public void FewerBins_AlwaysScoreBetter_WhateverTheFill()
    {
        var problem = BinPackingInstances.Random(30, 100, 10, 60, seed: 4);
        var search = new BinPackingSearchProblem(problem);
        var random = new RandomSource(2);

        var plans = Enumerable
            .Range(0, 200)
            .Select(_ => search.CreateRandom(random))
            .Select(plan => (plan.BinCount, Score: search.Evaluate(plan).ObjectiveValue))
            .OrderBy(entry => entry.Score)
            .ToList();

        Assert.Equal(plans.Select(entry => entry.BinCount).Order(), plans.Select(entry => entry.BinCount));
    }

    [Fact]
    public void Greedy_IsFeasible_AndNotBelowTheLowerBound()
    {
        var problem = BinPackingInstances.Random(60, 100, 10, 50, seed: 3);
        var search = new BinPackingSearchProblem(problem);

        var greedy = search.CreateGreedy(search.Evaluate);

        Assert.True(search.Evaluate(greedy).IsFeasible);
        Assert.True(greedy.BinCount >= problem.LowerBound);
    }

    [Fact]
    public void RandomSolutions_AndMoves_AlwaysFit()
    {
        var problem = BinPackingInstances.Random(40, 100, 5, 70, seed: 5);
        var search = new BinPackingSearchProblem(problem);
        var random = new RandomSource(1);

        var current = search.CreateRandom(random);
        var other = search.CreateRandom(random);

        for (var i = 0; i < 500; i++)
        {
            Assert.True(search.Evaluate(current).IsFeasible);

            current = search.RandomNeighbor(current, random);
            other = search.Mutate(search.Crossover(current, other, random), random);

            Assert.True(search.Evaluate(other).IsFeasible);
            Assert.Equal(problem.ItemCount, other.Count);
        }
    }

    [Fact]
    public void Crossover_OfAPlanWithItself_GivesThatPlanBack()
    {
        var problem = BinPackingInstances.Random(40, 100, 10, 60, seed: 6);
        var search = new BinPackingSearchProblem(problem);
        var random = new RandomSource(3);

        for (var i = 0; i < 50; i++)
        {
            var plan = search.CreateRandom(random);

            Assert.Equal(plan.Assignment, search.Crossover(plan, plan, random).Assignment);
        }
    }

    [Fact]
    public void Crossover_NeverUsesMoreBinsThanItemsAndNeverOverfills()
    {
        var problem = BinPackingInstances.Random(60, 100, 10, 60, seed: 9);
        var search = new BinPackingSearchProblem(problem);
        var random = new RandomSource(8);

        for (var i = 0; i < 200; i++)
        {
            var child = search.Crossover(search.CreateRandom(random), search.CreateRandom(random), random);

            Assert.True(search.Evaluate(child).IsFeasible);
            Assert.InRange(child.BinCount, problem.LowerBound, problem.ItemCount);
        }
    }

    [Fact]
    public void Neighbors_AreAllFeasible_AndDifferFromThePlan()
    {
        var problem = BinPackingInstances.Random(15, 100, 10, 60, seed: 2);
        var search = new BinPackingSearchProblem(problem);
        var start = search.CreateRandom(new RandomSource(4));

        var neighbors = search.Neighbors(start).ToList();

        Assert.NotEmpty(neighbors);
        Assert.All(neighbors, neighbor =>
        {
            Assert.True(search.Evaluate(neighbor).IsFeasible);
            Assert.NotEqual(start.Assignment, neighbor.Assignment);
        });
    }

    [Fact]
    public void Neighbors_CanEmptyABin()
    {
        var problem = new BinPackingProblem([50, 50], capacity: 100);
        var search = new BinPackingSearchProblem(problem);

        var neighbors = search.Neighbors(new BinPackingPlan([0, 1])).ToList();

        Assert.Contains(neighbors, plan => plan.BinCount == 1);
    }

    [Fact]
    public void RandomInstances_AreDeterministic_AndSizesStayInRange()
    {
        var first = BinPackingInstances.Random(25, 100, 20, 40, seed: 8);
        var second = BinPackingInstances.Random(25, 100, 20, 40, seed: 8);

        Assert.Equal(first.Sizes, second.Sizes);
        Assert.All(first.Sizes, size => Assert.InRange(size, 20, 40));
        Assert.Throws<ArgumentException>(() => BinPackingInstances.Random(5, 100, 50, 40, seed: 1));
        Assert.Throws<ArgumentException>(() => BinPackingInstances.Random(5, 100, 10, 101, seed: 1));
    }
}
