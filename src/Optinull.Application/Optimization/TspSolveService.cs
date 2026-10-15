using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Optimization;

public sealed record TspSolveResult(
    string SolverName,
    TspTour Tour,
    double Length,
    double GreedyLength,
    SearchResult<TspTour> Search,
    string? Settings = null);

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
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new TspSearchProblem(problem);
        var random = new RandomSource(seed);
        var tune = tuning ?? SolverTuning.None;

        // Only the budgeted solvers have a meaningful progress.
        var budgeted = kind is SolverKind.SimulatedAnnealing
            or SolverKind.GeneticAlgorithm
            or SolverKind.IteratedLocalSearch;
        var tracked = new ProgressTrackingProblem<TspTour>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyLength = TspEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        string? settings = null;
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

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.CityCount);

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<TspTour>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: iterations ?? EvaluationBudget)
                    .Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                // Start at the average edge of the nearest-neighbour tour and end
                // 100,000 times colder: 225 steps of 90 moves, about 20,000 evaluations.
                var start = greedyLength / problem.CityCount;

                if (start <= 0)
                    start = 1;

                var sa = tune.Annealing(start, iterations ?? EvaluationBudget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<TspTour>(
                        initialTemperature: sa.InitialTemperature,
                        coolingRate: AnnealingBudget.CoolingRate(iterations, sa.Moves, sa.FinalRatio),
                        iterationsPerTemperature: sa.Moves,
                        minimumTemperature: sa.InitialTemperature * sa.FinalRatio,
                        random: random)
                    .Solve(tracked, cancellationToken);
                break;
            }

            case SolverKind.GeneticAlgorithm:
                var ga = tune.Genetic(50, 0.2, 3, 10, iterations ?? EvaluationBudget);

                settings = ga.Applied;

                name = SolverNames.Display(kind);
                search = new GeneticAlgorithm<TspTour>(
                        populationSize: ga.Population,
                        generations: 1_000_000,
                        eliteCount: ga.Elite,
                        mutationRate: ga.Mutation,
                        tournamentSize: ga.Tournament,
                        localSearchTries: ga.LocalSearch,
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
            search,
            settings);
    }
}
