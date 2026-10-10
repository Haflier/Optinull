using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Knapsack;

namespace Optinull.Application.Optimization;

public sealed record KnapsackSolveResult(
    string SolverName,
    KnapsackSelection Selection,
    long Value,
    long Weight,
    double GreedyValue,
    SearchResult<KnapsackSelection> Search);

/// <summary>
/// Solves a knapsack with the chosen solver at a fixed evaluation budget. The settings
/// are untuned starting points; the temperature is scaled to the item values.
/// </summary>
public sealed class KnapsackSolveService
{
    public const int EvaluationBudget = 20_000;

    public KnapsackSolveResult Solve(
        KnapsackProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new KnapsackSearchProblem(problem);
        var random = new RandomSource(seed);

        var greedyValue = KnapsackEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        SearchResult<KnapsackSelection> search;

        switch (kind)
        {
            case SolverKind.SimulatedAnnealing:
            {
                // A move changes the value by about one item's value, so start there.
                var start = Math.Max(1.0, problem.Items.Average(item => (double)item.Value));

                name = "Simulated Annealing";
                search = new SimulatedAnnealing<KnapsackSelection>(
                        initialTemperature: start,
                        coolingRate: AnnealingBudget.CoolingRate(iterations),
                        iterationsPerTemperature: AnnealingBudget.IterationsPerTemperature,
                        minimumTemperature: start * 1e-5,
                        random: random)
                    .Solve(searchProblem, cancellationToken);
                break;
            }

            case SolverKind.GeneticAlgorithm:
                name = "Genetic Algorithm";
                search = new GeneticAlgorithm<KnapsackSelection>(
                        populationSize: 50,
                        generations: 1_000_000,
                        eliteCount: 2,
                        mutationRate: 0.2,
                        tournamentSize: 3,
                        localSearchTries: 10,
                        maxEvaluations: iterations ?? EvaluationBudget,
                        random: random)
                    .Solve(searchProblem, cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var (value, weight) = KnapsackEvaluator.Totals(problem, search.Best);

        return new KnapsackSolveResult(name, search.Best, value, weight, greedyValue, search);
    }
}
