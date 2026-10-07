using Optinull.Domain.Problems;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Solvers;

namespace Optinull.Optimization.Tests;

public class SimulatedAnnealingSolverTests
{
    [Fact]
    public void Solve_Maximize_FindsGoodSolution()
    {
        var problem = new OptimizationProblem(
            "Maximize");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Maximize(x);

        var solver = new SimulatedAnnealingSolver(
            random: new RandomSource(42));

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);

        Assert.True(
            result.Evaluation.ObjectiveValue >= 8);
    }

    [Fact]
    public void Solve_Minimize_FindsGoodSolution()
    {
        var problem = new OptimizationProblem(
            "Minimize");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Minimize(x);

        var solver = new SimulatedAnnealingSolver(
            random: new RandomSource(42));

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);

        Assert.True(
            result.Evaluation.ObjectiveValue <= 2);
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

        var solver = new SimulatedAnnealingSolver(
            random: new RandomSource(42));

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

        var solver = new SimulatedAnnealingSolver(
            random: new RandomSource(42));

        Assert.Throws<InvalidOperationException>(
            () => solver.Solve(problem));
    }

    [Fact]
    public void Constructor_InvalidTemperature_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulatedAnnealingSolver(
                initialTemperature: 0));
    }

    [Fact]
    public void Constructor_InvalidCoolingRate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulatedAnnealingSolver(
                coolingRate: 1));
    }
}
