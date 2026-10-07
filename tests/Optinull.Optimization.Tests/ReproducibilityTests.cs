using Optinull.Domain.Problems;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Solvers;

namespace Optinull.Optimization.Tests;

public class ReproducibilityTests
{
    [Fact]
    public void GeneticAlgorithm_SameSeed_ProducesSameResult()
    {
        var firstProblem = CreateProblem();
        var secondProblem = CreateProblem();

        var firstSolver = new GeneticAlgorithmSolver(
            populationSize: 30,
            generations: 50,
            eliteCount: 2,
            mutationRate: 0.1,
            random: new RandomSource(42));

        var secondSolver = new GeneticAlgorithmSolver(
            populationSize: 30,
            generations: 50,
            eliteCount: 2,
            mutationRate: 0.1,
            random: new RandomSource(42));

        var firstResult = firstSolver.Solve(firstProblem);
        var secondResult = secondSolver.Solve(secondProblem);

        Assert.Equal(
            firstResult.Evaluation.ObjectiveValue,
            secondResult.Evaluation.ObjectiveValue);

        Assert.Equal(
            firstResult.Evaluation.IsFeasible,
            secondResult.Evaluation.IsFeasible);

        Assert.Equal(
            firstResult.Solution.GetValue(firstProblem.Variables[0]),
            secondResult.Solution.GetValue(secondProblem.Variables[0]));

        Assert.Equal(
            firstResult.Solution.GetValue(firstProblem.Variables[1]),
            secondResult.Solution.GetValue(secondProblem.Variables[1]));
    }

    [Fact]
    public void SimulatedAnnealing_SameSeed_ProducesSameResult()
    {
        var firstProblem = CreateProblem();
        var secondProblem = CreateProblem();

        var firstSolver = new SimulatedAnnealingSolver(
            initialTemperature: 100,
            coolingRate: 0.95,
            iterationsPerTemperature: 10,
            random: new RandomSource(42));

        var secondSolver = new SimulatedAnnealingSolver(
            initialTemperature: 100,
            coolingRate: 0.95,
            iterationsPerTemperature: 10,
            random: new RandomSource(42));

        var firstResult = firstSolver.Solve(firstProblem);
        var secondResult = secondSolver.Solve(secondProblem);

        Assert.Equal(
            firstResult.Evaluation.ObjectiveValue,
            secondResult.Evaluation.ObjectiveValue);

        Assert.Equal(
            firstResult.Evaluation.IsFeasible,
            secondResult.Evaluation.IsFeasible);

        Assert.Equal(
            firstResult.Solution.GetValue(firstProblem.Variables[0]),
            secondResult.Solution.GetValue(secondProblem.Variables[0]));

        Assert.Equal(
            firstResult.Solution.GetValue(firstProblem.Variables[1]),
            secondResult.Solution.GetValue(secondProblem.Variables[1]));
    }

    [Fact]
    public void RandomSource_SameSeed_ProducesSameSequence()
    {
        var first = new RandomSource(42);
        var second = new RandomSource(42);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(
                first.NextDouble(),
                second.NextDouble());

            Assert.Equal(
                first.Next(0, 100),
                second.Next(0, 100));
        }
    }

    private static OptimizationProblem CreateProblem()
    {
        var problem = new OptimizationProblem(
            "Reproducibility");

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

        return problem;
    }
}
