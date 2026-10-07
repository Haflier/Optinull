using Optinull.Domain.Constraints;
using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Tests;

public class ConstraintEvaluatorTests
{
    [Fact]
    public void LessThanOrEqual_Satisfied_ReturnsTrue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .LessThanOrEqual(5)
            .ToHardConstraint();

        var solution = new Solution();
        solution.SetValue(x, 5);

        var evaluator = new ConstraintEvaluator();

        Assert.True(
            evaluator.IsSatisfied(
                constraint,
                solution));
    }

    [Fact]
    public void LessThanOrEqual_Violated_ReturnsFalse()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .LessThanOrEqual(5)
            .ToHardConstraint();

        var solution = new Solution();
        solution.SetValue(x, 6);

        var evaluator = new ConstraintEvaluator();

        Assert.False(
            evaluator.IsSatisfied(
                constraint,
                solution));
    }

    [Fact]
    public void GreaterThanOrEqual_Works()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .GreaterThanOrEqual(5)
            .ToHardConstraint();

        var solution = new Solution();
        solution.SetValue(x, 6);

        var evaluator = new ConstraintEvaluator();

        Assert.True(
            evaluator.IsSatisfied(
                constraint,
                solution));
    }

    [Fact]
    public void Equal_Works()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .EqualTo(5)
            .ToHardConstraint();

        var solution = new Solution();
        solution.SetValue(x, 5);

        var evaluator = new ConstraintEvaluator();

        Assert.True(
            evaluator.IsSatisfied(
                constraint,
                solution));
    }

    [Fact]
    public void SoftConstraint_Satisfied_HasZeroPenalty()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .LessThanOrEqual(5)
            .ToSoftConstraint(50);

        var solution = new Solution();
        solution.SetValue(x, 4);

        var evaluator = new ConstraintEvaluator();

        var penalty = evaluator.CalculatePenalty(
            constraint,
            solution);

        Assert.Equal(0, penalty);
    }

    [Fact]
    public void SoftLessThanOrEqual_CalculatesPenalty()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .LessThanOrEqual(5)
            .ToSoftConstraint(50);

        var solution = new Solution();
        solution.SetValue(x, 7);

        var evaluator = new ConstraintEvaluator();

        var penalty = evaluator.CalculatePenalty(
            constraint,
            solution);

        // Violation = 7 - 5 = 2
        // Penalty = 2 * 50 = 100
        Assert.Equal(100, penalty);
    }

    [Fact]
    public void SoftGreaterThanOrEqual_CalculatesPenalty()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .GreaterThanOrEqual(8)
            .ToSoftConstraint(20);

        var solution = new Solution();
        solution.SetValue(x, 5);

        var evaluator = new ConstraintEvaluator();

        var penalty = evaluator.CalculatePenalty(
            constraint,
            solution);

        // Violation = 8 - 5 = 3
        // Penalty = 3 * 20 = 60
        Assert.Equal(60, penalty);
    }

    [Fact]
    public void SoftEquality_CalculatesPenalty()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .EqualTo(8)
            .ToSoftConstraint(10);

        var solution = new Solution();
        solution.SetValue(x, 5);

        var evaluator = new ConstraintEvaluator();

        var penalty = evaluator.CalculatePenalty(
            constraint,
            solution);

        // Violation = |5 - 8| = 3
        // Penalty = 3 * 10 = 30
        Assert.Equal(30, penalty);
    }

    [Fact]
    public void HardConstraint_HasZeroPenalty()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var constraint = x
            .LessThanOrEqual(5)
            .ToHardConstraint();

        var solution = new Solution();
        solution.SetValue(x, 10);

        var evaluator = new ConstraintEvaluator();

        Assert.Equal(
            0,
            evaluator.CalculatePenalty(
                constraint,
                solution));
    }
}
