using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Genetics;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Tests;

public class OnePointCrossoverTests
{
    [Fact]
    public void Crossover_CombinesGenesFromBothParents()
    {
        var problem = new OptimizationProblem("Crossover");

        var a = problem.AddIntegerVariable("a", 0, 100);
        var b = problem.AddIntegerVariable("b", 0, 100);
        var c = problem.AddIntegerVariable("c", 0, 100);
        var d = problem.AddIntegerVariable("d", 0, 100);

        var firstParent = new Solution();

        firstParent.SetValue(a, 1);
        firstParent.SetValue(b, 2);
        firstParent.SetValue(c, 3);
        firstParent.SetValue(d, 4);

        var secondParent = new Solution();

        secondParent.SetValue(a, 10);
        secondParent.SetValue(b, 20);
        secondParent.SetValue(c, 30);
        secondParent.SetValue(d, 40);

        // Force the crossover point to be 2:
        //
        // Parent 1: [1  2 | 3  4]
        // Parent 2: [10 20 | 30 40]
        //
        // Child:    [1  2 | 30 40]
        var crossover = new OnePointCrossover(
            new FixedRandomSource(2));

        var child = crossover.Crossover(
            problem,
            firstParent,
            secondParent);

        Assert.Equal(1, child.GetValue(a));
        Assert.Equal(2, child.GetValue(b));
        Assert.Equal(30, child.GetValue(c));
        Assert.Equal(40, child.GetValue(d));
    }

    [Fact]
    public void Crossover_RejectsProblemWithLessThanTwoVariables()
    {
        var problem = new OptimizationProblem("Crossover");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            100);

        var firstParent = new Solution();
        firstParent.SetValue(x, 10);

        var secondParent = new Solution();
        secondParent.SetValue(x, 20);

        var crossover = new OnePointCrossover(
            new RandomSource(42));

        Assert.Throws<InvalidOperationException>(
            () => crossover.Crossover(
                problem,
                firstParent,
                secondParent));
    }

    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly int _value;

        public FixedRandomSource(int value)
        {
            _value = value;
        }

        public double NextDouble()
        {
            return 0;
        }

        public int Next(int minValue, int maxValue)
        {
            if (_value < minValue || _value >= maxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            return _value;
        }
    }
}
