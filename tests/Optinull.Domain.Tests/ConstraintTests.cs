using Optinull.Domain.Constraints;
using Optinull.Domain.Expressions;
using Optinull.Domain.Problems;

namespace Optinull.Domain.Tests;

public class ConstraintTests
{
    [Fact]
    public void LessThanOrEqual_CreatesConstraintExpression()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        var expression = (x + y).LessThanOrEqual(1);

        Assert.Equal(
            ConstraintType.LessThanOrEqual,
            expression.Type);

        Assert.Equal(
            ExpressionType.Add,
            expression.Left.Type);

        Assert.Equal(
            ExpressionType.Constant,
            expression.Right.Type);
    }

    [Fact]
    public void ConstraintExpression_CanBecomeHardConstraint()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        var constraintExpression =
            (x + y).LessThanOrEqual(1);

        var constraint =
            constraintExpression.ToHardConstraint();

        Assert.False(constraint.IsSoft);
        Assert.Equal(0, constraint.Penalty);
        Assert.Equal(
            ConstraintType.LessThanOrEqual,
            constraint.Type);
    }

    [Fact]
    public void ConstraintExpression_CanBecomeSoftConstraint()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var constraint =
            x.LessThanOrEqual(1)
             .ToSoftConstraint(50);

        Assert.True(constraint.IsSoft);
        Assert.Equal(50, constraint.Penalty);
    }

    [Fact]
    public void Problem_StoresConstraints()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        problem.AddConstraint(
            x.LessThanOrEqual(1)
             .ToHardConstraint());

        Assert.Single(problem.Constraints);
    }
}
