using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Tests;

public class ExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_Constant_ReturnsValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();
        solution.SetValue(x, 0);

        var evaluator = new ExpressionEvaluator();

        var result = evaluator.Evaluate(
            42,
            solution);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Evaluate_Variable_ReturnsAssignedValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");

        var solution = new Solution();
        solution.SetValue(x, 1);

        var evaluator = new ExpressionEvaluator();

        var result = evaluator.Evaluate(
            x,
            solution);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Evaluate_Addition_ReturnsCorrectResult()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        var solution = new Solution();

        solution.SetValue(x, 1);
        solution.SetValue(y, 0);

        var evaluator = new ExpressionEvaluator();

        var result = evaluator.Evaluate(
            x + y,
            solution);

        Assert.Equal(1, result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_ReturnsCorrectResult()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        var solution = new Solution();

        solution.SetValue(x, 1);
        solution.SetValue(y, 0);

        var evaluator = new ExpressionEvaluator();

        var result = evaluator.Evaluate(
            5 * x + 8 * y,
            solution);

        Assert.Equal(5, result);
    }

    [Fact]
    public void Evaluate_Negation_ReturnsNegativeValue()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            -10,
            10);

        var solution = new Solution();

        solution.SetValue(x, 7);

        var evaluator = new ExpressionEvaluator();

        var result = evaluator.Evaluate(
            -x,
            solution);

        Assert.Equal(-7, result);
    }

    [Fact]
    public void Evaluate_Division_ReturnsCorrectResult()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            1,
            10);

        var solution = new Solution();

        solution.SetValue(x, 8);

        var evaluator = new ExpressionEvaluator();

        var result = evaluator.Evaluate(
            x / 2,
            solution);

        Assert.Equal(4, result);
    }

    [Fact]
    public void Evaluate_DivisionByZero_Throws()
    {
        var problem = new OptimizationProblem("Test");

        var x = problem.AddContinuousVariable(
            "x",
            0,
            10);

        var solution = new Solution();

        solution.SetValue(x, 1);

        var evaluator = new ExpressionEvaluator();

        Assert.Throws<DivideByZeroException>(
            () => evaluator.Evaluate(
                x / 0,
                solution));
    }
}
