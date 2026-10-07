using Optinull.Domain.Problems;
using Optinull.Optimization.Solvers;

namespace Optinull.Optimization.Tests;

public class GreedySolverTests
{
    [Fact]
    public void Solve_FindsFeasibleSolution()
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

        var solver = new GreedySolver();

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(5, result.Evaluation.ObjectiveValue);

        Assert.Equal(
            1,
            result.Solution.GetValue(x));

        Assert.Equal(
            0,
            result.Solution.GetValue(y));
    }

    [Fact]
    public void Solve_Minimize_ChoosesLowerValue()
    {
        var problem = new OptimizationProblem(
            "Minimize");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Minimize(x);

        var solver = new GreedySolver();

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(0, result.Evaluation.ObjectiveValue);
        Assert.Equal(0, result.Solution.GetValue(x));
    }

    [Fact]
    public void Solve_Maximize_ChoosesUpperValue()
    {
        var problem = new OptimizationProblem(
            "Maximize");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Maximize(x);

        var solver = new GreedySolver();

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(10, result.Evaluation.ObjectiveValue);
        Assert.Equal(10, result.Solution.GetValue(x));
    }

    [Fact]
    public void Solve_RespectsHardConstraints()
    {
        var problem = new OptimizationProblem(
            "Constrained");

        var x = problem.AddBinaryVariable("x");
        var y = problem.AddBinaryVariable("y");

        problem.AddConstraint(
            (x + y)
                .LessThanOrEqual(1)
                .ToHardConstraint());

        problem.Maximize(5 * x + 4 * y);

        var solver = new GreedySolver();

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);

        Assert.True(
            result.Solution.GetValue(x) +
            result.Solution.GetValue(y) <= 1);
    }

    [Fact]
    public void Solve_WithoutObjective_Throws()
    {
        var problem = new OptimizationProblem(
            "No Objective");

        problem.AddBinaryVariable("x");

        var solver = new GreedySolver();

        Assert.Throws<InvalidOperationException>(
            () => solver.Solve(problem));
    }

    [Fact]
    public void Solve_WithInfeasibleInitialSolution_Throws()
    {
        var problem = new OptimizationProblem(
            "Infeasible");

        var x = problem.AddBinaryVariable("x");

        // x >= 1, while the initial solution is x = 0.
        problem.AddConstraint(
            x
                .GreaterThanOrEqual(1)
                .ToHardConstraint());

        problem.Maximize(x);

        var solver = new GreedySolver();

        Assert.Throws<InvalidOperationException>(
            () => solver.Solve(problem));
    }
}
