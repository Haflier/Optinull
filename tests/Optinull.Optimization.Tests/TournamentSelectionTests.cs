using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Comparison;
using Optinull.Optimization.Genetics;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Tests;

public class TournamentSelectionTests
{
    [Fact]
    public void Select_Maximize_ReturnsBestCandidateFromTournament()
    {
        var problem = new OptimizationProblem("Selection");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            100);

        problem.Maximize(x);

        var solution1 = new Solution();
        solution1.SetValue(x, 10);

        var solution2 = new Solution();
        solution2.SetValue(x, 50);

        var solution3 = new Solution();
        solution3.SetValue(x, 30);

        var population = new Population(
            [solution1, solution2, solution3]);

        var evaluator = new Optinull.Domain.Evaluation.ProblemEvaluator();

        var evaluations = population.Solutions.ToDictionary(
            solution => solution,
            solution => evaluator.Evaluate(
                problem,
                solution));

        var selection = new TournamentSelection(
            new SolutionComparer(ObjectiveType.Maximize),
            new RandomSource(42),
            tournamentSize: 10);

        var selected = selection.Select(
            population,
            evaluations);

        Assert.Same(solution2, selected);
    }

    [Fact]
    public void Constructor_RejectsInvalidTournamentSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TournamentSelection(
                new SolutionComparer(ObjectiveType.Maximize),
                new RandomSource(42),
                tournamentSize: 0));
    }

    [Fact]
    public void Select_RejectsEmptyPopulation()
    {
        var problem = new OptimizationProblem("Selection");

        var x = problem.AddIntegerVariable(
            "x",
            0,
            10);

        problem.Maximize(x);

        var solution = new Solution();

        var evaluations =
            new Dictionary<Solution, EvaluationResult>();

        // Population itself cannot be empty, so the failure
        // is enforced by Population's constructor.
        Assert.Throws<ArgumentException>(
            () => new Population([]));
    }
}
