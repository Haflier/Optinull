using Optinull.Domain.Problems;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Solvers;

namespace Optinull.Optimization.Tests;

public class GeneticAlgorithmSolverTests
{
    [Fact]
    public void Solve_ReturnsFeasibleSolution()
    {
        var problem = new OptimizationProblem(
            "Genetic Algorithm");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        var y = problem.AddIntegerVariable(
            "y",
            0,
            10);

        problem.AddConstraint(
            (x + y).LessThanOrEqual(10)
                .ToHardConstraint());

        problem.Maximize(
            5 * x + 8 * y);

        var solver = new GeneticAlgorithmSolver(
            populationSize: 30,
            generations: 50,
            eliteCount: 2,
            mutationRate: 0.1,
            random: new RandomSource(42));

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);

        Assert.InRange(
            result.Solution.GetValue(x),
            0,
            10);

        Assert.InRange(
            result.Solution.GetValue(y),
            0,
            10);
    }

    [Fact]
    public void Solve_RejectsProblemWithoutObjective()
    {
        var problem = new OptimizationProblem(
            "No Objective");

        problem.AddIntegerVariable(
            "x",
            0,
            10);

        var solver = new GeneticAlgorithmSolver(
            random: new RandomSource(42));

        Assert.Throws<InvalidOperationException>(
            () => solver.Solve(problem));
    }

    [Fact]
    public void Constructor_RejectsInvalidPopulationSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeneticAlgorithmSolver(
                populationSize: 0));
    }

    [Fact]
    public void Constructor_RejectsInvalidEliteCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeneticAlgorithmSolver(
                populationSize: 10,
                eliteCount: 11));
    }

    [Fact]
    public void Solve_DoesNotReturnInfeasibleSolution()
    {
        var problem = new OptimizationProblem(
            "Constrained Genetic Algorithm");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        var y = problem.AddIntegerVariable(
            "y",
            0,
            10);

        problem.AddConstraint(
            (x + y)
                .LessThanOrEqual(10)
                .ToHardConstraint());

        problem.Maximize(
            5 * x + 8 * y);

        var solver = new GeneticAlgorithmSolver(
            populationSize: 40,
            generations: 50,
            eliteCount: 2,
            mutationRate: 0.1,
            random: new RandomSource(42));

        var result = solver.Solve(problem);

        Assert.True(result.Evaluation.IsFeasible);

        var xValue = result.Solution.GetValue(x);
        var yValue = result.Solution.GetValue(y);

        Assert.True(
            xValue + yValue <= 10);
    }
}
