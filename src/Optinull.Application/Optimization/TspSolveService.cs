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
    public const int EvaluationBudget = SolveOptions.DefaultBudget;

    public TspSolveResult Solve(
        TspProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new TspSearchProblem(problem);
        var random = new RandomSource(seed);

        // Only the budgeted solvers have a meaningful progress.
        var budgeted = kind is SolverKind.SimulatedAnnealing or SolverKind.GeneticAlgorithm;
        var tracked = new ProgressTrackingProblem<TspTour>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyLength = TspEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        SearchResult<TspTour> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<TspTour>().Solve(tracked, cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<TspTour>(random).Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                // Start at the average edge of the nearest-neighbour tour and end
                // 100,000 times colder: 225 steps of 90 moves, about 20,000 evaluations.
                var start = greedyLength / problem.CityCount;

                if (start <= 0)
                    start = 1;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<TspTour>(
                        initialTemperature: start,
                        coolingRate: AnnealingBudget.CoolingRate(iterations),
                        iterationsPerTemperature: AnnealingBudget.IterationsPerTemperature,
                        minimumTemperature: start * 1e-5,
                        random: random)
                    .Solve(tracked, cancellationToken);
                break;
            }

            case SolverKind.GeneticAlgorithm:
                name = SolverNames.Display(kind);
                search = new GeneticAlgorithm<TspTour>(
                        populationSize: 50,
                        generations: 1_000_000,
                        eliteCount: 2,
                        mutationRate: 0.2,
                        tournamentSize: 3,
                        localSearchTries: 10,
                        maxEvaluations: iterations ?? EvaluationBudget,
                        random: random)
                    .Solve(tracked, cancellationToken);
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
