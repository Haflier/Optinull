using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Tests;

public class SolutionTests
{
    [Fact]
    public void SetValue_StoresVariableValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();

        solution.SetValue(x, 1);

        Assert.Equal(1, solution.GetValue(x));
    }

    [Fact]
    public void SetValue_RejectsValueOutsideBounds()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var solution = new Solution();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => solution.SetValue(x, 11));
    }

    [Fact]
    public void SetValue_RejectsInvalidBinaryValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();

        Assert.Throws<ArgumentException>(
            () => solution.SetValue(x, 0.5));
    }

    [Fact]
    public void SetValue_RejectsNonIntegerValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        var solution = new Solution();

        Assert.Throws<ArgumentException>(
            () => solution.SetValue(x, 2.5));
    }

    [Fact]
    public void GetValue_ThrowsWhenVariableHasNoValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();

        Assert.Throws<InvalidOperationException>(
            () => solution.GetValue(x));
    }
}
