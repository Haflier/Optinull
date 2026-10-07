using Optinull.Domain.Expressions;
using Optinull.Domain.Objectives;
using Optinull.Domain.Problems;

namespace Optinull.Domain.Tests;

public class ObjectiveTests
{
    [Fact]
    public void Maximize_CreatesMaximizeObjective()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        problem.Maximize(5 * x);

        Assert.NotNull(problem.Objective);
        Assert.Equal(
            ObjectiveType.Maximize,
            problem.Objective.Type);
        Assert.Equal(
            ExpressionType.Multiply,
            problem.Objective.Expression.Type);
    }

    [Fact]
    public void Minimize_CreatesMinimizeObjective()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            100);

        problem.Minimize(x);

        Assert.NotNull(problem.Objective);
        Assert.Equal(
            ObjectiveType.Minimize,
            problem.Objective.Type);
    }

    [Fact]
    public void Problem_CannotHaveTwoObjectives()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        problem.Maximize(x);

        Assert.Throws<InvalidOperationException>(
            () => problem.Minimize(x));
    }

    [Fact]
    public void Objective_CanContainComplexExpression()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        problem.Maximize(5 * x + 8 * y);

        var expression = problem.Objective!.Expression;

        var addition = Assert.IsType<BinaryExpression>(
            expression);

        Assert.Equal(
            ExpressionType.Add,
            addition.Type);
    }
}
