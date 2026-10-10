using Optinull.Optimization.Randomness;
using Optinull.Problems.Knapsack;

namespace Optinull.Problems.Tests.Knapsack;

/// <summary>Checks the knapsack pieces against independent, brute-force calculations.</summary>
public sealed class KnapsackDiagnosticsTests
{
    private static long BruteForceOptimum(KnapsackProblem problem)
    {
        long best = 0;

        for (var mask = 0; mask < 1 << problem.ItemCount; mask++)
        {
            long value = 0;
            long weight = 0;

            for (var i = 0; i < problem.ItemCount; i++)
            {
                if ((mask & (1 << i)) == 0)
                    continue;

                value += problem.Items[i].Value;
                weight += problem.Items[i].Weight;
            }

            if (weight <= problem.Capacity && value > best)
                best = value;
        }

        return best;
    }

    [Fact]
    public void ExactSolver_MatchesBruteForce_OnManyRandomInstances()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var itemCount = 3 + seed % 10;
            var problem = KnapsackInstances.Random(itemCount, seed);

            Assert.True(
                BruteForceOptimum(problem) == KnapsackExactSolver.Optimum(problem),
                $"Mismatch for {itemCount} items, seed {seed}.");
        }
    }

    [Fact]
    public void Greedy_ResultIsMaximal_NoLeftOutItemStillFits()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var problem = KnapsackInstances.Random(30, seed);
            var search = new KnapsackSearchProblem(problem);

            var greedy = search.CreateGreedy(search.Evaluate);
            var weight = KnapsackEvaluator.Totals(problem, greedy).Weight;

            for (var i = 0; i < problem.ItemCount; i++)
            {
                if (!greedy[i])
                    Assert.True(weight + problem.Items[i].Weight > problem.Capacity, $"Item {i}, seed {seed}.");
            }
        }
    }

    [Fact]
    public void Neighbors_CountMatchesAnIndependentCount()
    {
        var problem = KnapsackInstances.Random(18, seed: 6);
        var search = new KnapsackSearchProblem(problem);
        var random = new RandomSource(3);

        for (var round = 0; round < 25; round++)
        {
            var solution = search.CreateRandom(random);
            var weight = KnapsackEvaluator.Totals(problem, solution).Weight;
            var items = problem.Items;

            var expected = 0;

            for (var i = 0; i < problem.ItemCount; i++)
            {
                if (solution[i])
                    expected++;
                else if (weight + items[i].Weight <= problem.Capacity)
                    expected++;
            }

            for (var a = 0; a < problem.ItemCount; a++)
            {
                for (var b = 0; b < problem.ItemCount; b++)
                {
                    if (solution[a] && !solution[b] &&
                        weight - items[a].Weight + items[b].Weight <= problem.Capacity)
                    {
                        expected++;
                    }
                }
            }

            Assert.Equal(expected, search.Neighbors(solution).Count());
        }
    }

    [Fact]
    public void Crossover_OfTwoIdenticalParents_ReturnsThatParent()
    {
        var problem = KnapsackInstances.Random(25, seed: 1);
        var search = new KnapsackSearchProblem(problem);
        var random = new RandomSource(2);

        var parent = search.CreateRandom(random);

        for (var i = 0; i < 50; i++)
            Assert.Equal(parent.Flags, search.Crossover(parent, parent, random).Flags);
    }

    [Fact]
    public void HillClimbingOverNeighbors_EndsAtALocalOptimum_ThatIsFeasibleAndNotAboveTheOptimum()
    {
        var problem = KnapsackInstances.Random(25, seed: 9);
        var search = new KnapsackSearchProblem(problem);
        var optimum = KnapsackExactSolver.Optimum(problem);

        var current = search.CreateRandom(new RandomSource(5));
        var value = search.Evaluate(current).ObjectiveValue;

        while (true)
        {
            var better = search.Neighbors(current)
                .Select(neighbor => (Solution: neighbor, Value: search.Evaluate(neighbor).ObjectiveValue))
                .Where(candidate => candidate.Value > value)
                .OrderByDescending(candidate => candidate.Value)
                .FirstOrDefault();

            if (better.Solution is null)
                break;

            current = better.Solution;
            value = better.Value;
        }

        Assert.True(search.Evaluate(current).IsFeasible);
        Assert.True(value <= optimum);
    }
}
