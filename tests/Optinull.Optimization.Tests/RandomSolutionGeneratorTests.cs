using Optinull.Domain.Problems;
using Optinull.Optimization.Generation;

namespace Optinull.Optimization.Tests;

public class RandomSolutionGeneratorTests
{
    [Fact]
    public void Generate_CreatesValueForEveryVariable()
    {
        var problem = new OptimizationProblem("Test");

        var binary = problem.AddBinaryVariable("binary");

        var integer = problem.AddIntegerVariable(
            "integer",
            1,
            10);

        var continuous = problem.AddContinuousVariable(
            "continuous",
            0,
            100);

        var generator = new RandomSolutionGenerator();

        var solution = generator.Generate(
            problem,
            new Random(123));

        Assert.Equal(3, solution.Values.Count);

        Assert.InRange(
            solution.GetValue(binary),
            0,
            1);

        Assert.InRange(
            solution.GetValue(integer),
            1,
            10);

        Assert.InRange(
            solution.GetValue(continuous),
            0,
            100);
    }

    [Fact]
    public void Generate_WithSameSeed_ProducesSameSolution()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            100);

        var y = problem.AddIntegerVariable(
            "y",
            0,
            100);

        var generator = new RandomSolutionGenerator();

        var solution1 = generator.Generate(
            problem,
            new Random(42));

        var solution2 = generator.Generate(
            problem,
            new Random(42));

        Assert.Equal(
            solution1.GetValue(x),
            solution2.GetValue(x));

        Assert.Equal(
            solution1.GetValue(y),
            solution2.GetValue(y));
    }
}
