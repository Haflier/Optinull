using Optinull.Domain.Expressions;
using Optinull.Domain.Problems;

namespace Optinull.Domain.Tests;

public class ExpressionTests
{
    [Fact]
    public void Variable_CanBeUsedAsExpression()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        Expression expression = x;

        Assert.Equal(
            ExpressionType.Variable,
            expression.Type);
    }

    [Fact]
    public void Addition_CreatesBinaryExpression()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        Expression expression = x + y;

        var binary = Assert.IsType<BinaryExpression>(
            expression);

        Assert.Equal(
            ExpressionType.Add,
            binary.Type);

        Assert.Equal(
            ExpressionType.Variable,
            binary.Left.Type);

        Assert.Equal(
            ExpressionType.Variable,
            binary.Right.Type);
    }

    [Fact]
    public void ComplexExpression_BuildsCorrectTree()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        Expression expression = 2 * x + 5 * y;

        var addition = Assert.IsType<BinaryExpression>(
            expression);

        Assert.Equal(
            ExpressionType.Add,
            addition.Type);

        var left = Assert.IsType<BinaryExpression>(
            addition.Left);

        Assert.Equal(
            ExpressionType.Multiply,
            left.Type);

        var right = Assert.IsType<BinaryExpression>(
            addition.Right);

        Assert.Equal(
            ExpressionType.Multiply,
            right.Type);
    }

    [Fact]
    public void Negation_CreatesUnaryExpression()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        Expression expression = -x;

        var unary = Assert.IsType<UnaryExpression>(
            expression);

        Assert.Equal(
            ExpressionType.Negate,
            unary.Type);

        Assert.Equal(
            ExpressionType.Variable,
            unary.Operand.Type);
    }
}
