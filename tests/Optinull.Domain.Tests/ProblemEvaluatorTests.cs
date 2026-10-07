using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Tests;

public class ProblemEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsObjectiveValue()
    {
        var problem = new OptimizationProblem(
            "Simple Selection");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        problem.Maximize(5 * x + 8 * y);

        var solution = new Solution();

        solution.SetValue(x, 1);
        solution.SetValue(y, 0);

        var evaluator = new ProblemEvaluator();

        var result = evaluator.Evaluate(
            problem,
            solution);

        Assert.Equal(5, result.ObjectiveValue);
    }

    [Fact]
    public void Evaluate_SatisfiedHardConstraints_IsFeasible()
    {
        var problem = new OptimizationProblem(
            "Simple Selection");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        problem.AddConstraint(
            (x + y)
                .LessThanOrEqual(1)
                .ToHardConstraint());

        problem.Maximize(5 * x + 8 * y);

        var solution = new Solution();

        solution.SetValue(x, 1);
        solution.SetValue(y, 0);

        var evaluator = new ProblemEvaluator();

        var result = evaluator.Evaluate(
            problem,
            solution);

        Assert.True(result.IsFeasible);
        Assert.Equal(0, result.Penalty);
    }

    [Fact]
    public void Evaluate_ViolatedHardConstraint_IsNotFeasible()
    {
        var problem = new OptimizationProblem(
            "Simple Selection");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        problem.AddConstraint(
            (x + y)
                .LessThanOrEqual(1)
                .ToHardConstraint());

        problem.Maximize(5 * x + 8 * y);

        var solution = new Solution();

        solution.SetValue(x, 1);
        solution.SetValue(y, 1);

        var evaluator = new ProblemEvaluator();

        var result = evaluator.Evaluate(
            problem,
            solution);

        Assert.False(result.IsFeasible);
        Assert.Equal(0, result.Penalty);
    }

    [Fact]
    public void Evaluate_ViolatedSoftConstraint_HasPenalty()
    {
        var problem = new OptimizationProblem(
            "Soft Constraint");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        problem.AddConstraint(
            x
                .LessThanOrEqual(5)
                .ToSoftConstraint(20));

        problem.Maximize(x);

        var solution = new Solution();

        solution.SetValue(x, 8);

        var evaluator = new ProblemEvaluator();

        var result = evaluator.Evaluate(
            problem,
            solution);

        Assert.True(result.IsFeasible);
        Assert.Equal(60, result.Penalty);
        Assert.Equal(8, result.ObjectiveValue);
    }

    [Fact]
    public void Evaluate_MultipleSoftConstraints_SumsPenalties()
    {
        var problem = new OptimizationProblem(
            "Multiple Soft Constraints");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            20);

        problem.AddConstraint(
            x
                .LessThanOrEqual(5)
                .ToSoftConstraint(10));

        problem.AddConstraint(
            x
                .LessThanOrEqual(8)
                .ToSoftConstraint(20));

        problem.Maximize(x);

        var solution = new Solution();

        solution.SetValue(x, 10);

        var evaluator = new ProblemEvaluator();

        var result = evaluator.Evaluate(
            problem,
            solution);

        // First constraint:
        // violation = 10 - 5 = 5
        // penalty = 5 * 10 = 50
        //
        // Second constraint:
        // violation = 10 - 8 = 2
        // penalty = 2 * 20 = 40
        //
        // Total = 90

        Assert.True(result.IsFeasible);
        Assert.Equal(90, result.Penalty);
    }

    [Fact]
    public void Evaluate_WithoutObjective_Throws()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();
        solution.SetValue(x, 1);

        var evaluator = new ProblemEvaluator();

        Assert.Throws<InvalidOperationException>(
            () => evaluator.Evaluate(
                problem,
                solution));
    }
}
