using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Genetics;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Tests;

public class BasicMutationTests
{
    [Fact]
    public void Mutate_RateZero_DoesNotChangeSolution()
    {
        var problem = new OptimizationProblem("Mutation");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();
        solution.SetValue(x, 0);

        var mutation = new BasicMutation(
            new RandomSource(42),
            mutationRate: 0);

        var mutated = mutation.Mutate(
            problem,
            solution);

        Assert.Equal(0, mutated.GetValue(x));
        Assert.Equal(0, solution.GetValue(x));
    }

    [Fact]
    public void Mutate_BinaryVariable_FlipsValue()
    {
        var problem = new OptimizationProblem("Mutation");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();
        solution.SetValue(x, 0);

        var mutation = new BasicMutation(
            new AlwaysMutateRandomSource(),
            mutationRate: 1);

        var mutated = mutation.Mutate(
            problem,
            solution);

        Assert.Equal(1, mutated.GetValue(x));

        // Original solution must remain unchanged.
        Assert.Equal(0, solution.GetValue(x));
    }

    [Fact]
    public void Mutate_IntegerVariable_StaysWithinBounds()
    {
        var problem = new OptimizationProblem("Mutation");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        var solution = new Solution();
        solution.SetValue(x, 0);

        var mutation = new BasicMutation(
            new AlwaysMutateRandomSource(),
            mutationRate: 1);

        var mutated = mutation.Mutate(
            problem,
            solution);

        Assert.InRange(
            mutated.GetValue(x),
            0,
            10);
    }

    [Fact]
    public void Constructor_RejectsInvalidMutationRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BasicMutation(
                new RandomSource(42),
                mutationRate: 1.1));
    }

    private sealed class AlwaysMutateRandomSource : IRandomSource
    {
        public double NextDouble()
        {
            return 0;
        }

        public int Next(int minValue, int maxValue)
        {
            return minValue;
        }
    }
}
