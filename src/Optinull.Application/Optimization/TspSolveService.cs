using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Optimization;

public sealed record TspSolveResult(
    string SolverName,
    TspTour Tour,
    double Length,
    double GreedyLength,
    SearchResult<TspTour> Search);

/// <summary>
/// Solves a TSP with the chosen solver at a fixed evaluation budget. The settings
/// are untuned starting points; the temperature is scaled to the instance because
/// tour lengths differ wildly between instances.
/// </summary>
public sealed class TspSolveService
{
    public const int EvaluationBudget = 20_000;

    public TspSolveResult Solve(
        TspProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new TspSearchProblem(problem);
        var random = new RandomSource(seed);

        var greedyLength = TspEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        SearchResult<TspTour> search;

        switch (kind)
        {
            case SolverKind.SimulatedAnnealing:
            {
                // Start at the average edge of the nearest-neighbour tour and end
                // 100,000 times colder: 225 steps of 90 moves, about 20,000 evaluations.
                var start = greedyLength / problem.CityCount;

                if (start <= 0)
                    start = 1;

                name = "Simulated Annealing";
                search = new SimulatedAnnealing<TspTour>(
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
                search = new GeneticAlgorithm<TspTour>(
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

        return new TspSolveResult(
            name,
            search.Best,
            search.Evaluation.ObjectiveValue,
            greedyLength,
            search);
    }
}
