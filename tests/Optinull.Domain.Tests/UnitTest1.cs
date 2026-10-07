using Optinull.Domain.Problems;
using Optinull.Domain.Variables;

namespace Optinull.Domain.Tests;

public class VariableTests
{
    [Fact]
    public void AddBinaryVariable_CreatesBinaryVariable()
    {
        var problem = new OptimizationProblem("Test");

        var variable = problem.AddBinaryVariable("x");

        Assert.Equal("x", variable.Name);
        Assert.Equal(VariableType.Binary, variable.Type);
        Assert.Equal(0, variable.LowerBound);
        Assert.Equal(1, variable.UpperBound);
    }

    [Fact]
    public void AddIntegerVariable_CreatesIntegerVariable()
    {
        var problem = new OptimizationProblem("Test");

        var variable = problem.AddIntegerVariable("workers", 0, 10);

        Assert.Equal("workers", variable.Name);
        Assert.Equal(VariableType.Integer, variable.Type);
        Assert.Equal(0, variable.LowerBound);
        Assert.Equal(10, variable.UpperBound);
    }

    [Fact]
    public void AddContinuousVariable_CreatesContinuousVariable()
    {
        var problem = new OptimizationProblem("Test");

        var variable =
            problem.AddContinuousVariable("temperature", 0, 100);

        Assert.Equal(VariableType.Continuous, variable.Type);
        Assert.Equal(0, variable.LowerBound);
        Assert.Equal(100, variable.UpperBound);
    }

    [Fact]
    public void DuplicateVariableName_Throws()
    {
        var problem = new OptimizationProblem("Test");

        problem.AddBinaryVariable("x");

        Assert.Throws<ArgumentException>(
            () => problem.AddBinaryVariable("x"));
    }

    [Fact]
    public void InvalidBinaryBounds_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Variable(
                "x",
                VariableType.Binary,
                0,
                10));
    }
}
