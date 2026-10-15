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
    SearchResult<KnapsackSelection> Search,
    string? Settings = null);

/// <summary>
/// Solves a knapsack with the chosen solver at a fixed evaluation budget. The settings
/// are untuned starting points; the temperature is scaled to the item values.
/// </summary>
public sealed class KnapsackSolveService
{
    public const int EvaluationBudget = SolveOptions.DefaultBudget;

    public KnapsackSolveResult Solve(
        KnapsackProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new KnapsackSearchProblem(problem);
        var random = new RandomSource(seed);
        var tune = tuning ?? SolverTuning.None;

        var budgeted = kind is SolverKind.SimulatedAnnealing
            or SolverKind.GeneticAlgorithm
            or SolverKind.IteratedLocalSearch;
        var tracked = new ProgressTrackingProblem<KnapsackSelection>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyValue = KnapsackEvaluator
            .Evaluate(problem, searchProblem.CreateGreedy(searchProblem.Evaluate))
            .ObjectiveValue;

        string name;
        string? settings = null;
        SearchResult<KnapsackSelection> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<KnapsackSelection>().Solve(tracked, cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<KnapsackSelection>(random).Solve(tracked, cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.ItemCount);

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<KnapsackSelection>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: iterations ?? EvaluationBudget)
                    .Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                // A move changes the value by about one item's value, so start there.
                var start = Math.Max(1.0, problem.Items.Average(item => (double)item.Value));

                var sa = tune.Annealing(start, iterations ?? EvaluationBudget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<KnapsackSelection>(
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
                search = new GeneticAlgorithm<KnapsackSelection>(
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

        var (value, weight) = KnapsackEvaluator.Totals(problem, search.Best);

        return new KnapsackSolveResult(name, search.Best, value, weight, greedyValue, search, settings);
    }
}
