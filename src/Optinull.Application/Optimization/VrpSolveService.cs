using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.Vrp;

namespace Optinull.Application.Optimization;

public sealed record VrpSolveResult(
    string SolverName,
    VrpPlan Plan,
    double Distance,
    double GreedyDistance,
    SearchResult<VrpPlan> Search,
    string? Settings = null);

/// <summary>
/// Solves a routing problem with the chosen solver at a fixed evaluation budget. Settings the
/// user did not give are chosen from the problem and the budget; the temperature is scaled to
/// the problem because distances differ wildly between instances.
/// </summary>
public sealed class VrpSolveService
{
    public const int EvaluationBudget = SolveOptions.DefaultBudget;

    public VrpSolveResult Solve(
        VrpProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new VrpSearchProblem(problem);
        var random = new RandomSource(seed);
        var tune = tuning ?? SolverTuning.None;

        // Only the budgeted solvers have a meaningful progress.
        var budgeted = kind is SolverKind.SimulatedAnnealing
            or SolverKind.GeneticAlgorithm
            or SolverKind.IteratedLocalSearch;
        var tracked = new ProgressTrackingProblem<VrpPlan>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyPlan = searchProblem.CreateGreedy(searchProblem.Evaluate);
        var greedyDistance = VrpEvaluator.TotalDistance(problem, greedyPlan);

        string name;
        string? settings = null;
        SearchResult<VrpPlan> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<VrpPlan>().Solve(tracked, cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<VrpPlan>(random).Solve(tracked, cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.CustomerCount);

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<VrpPlan>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: iterations ?? EvaluationBudget)
                    .Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                // Start at the average leg of the nearest-neighbour plan: a tour of n customers
                // and r routes has n + r legs.
                var start = greedyDistance / (problem.CustomerCount + greedyPlan.RouteCount);

                if (start <= 0)
                    start = 1;

                var sa = tune.Annealing(start, iterations ?? EvaluationBudget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<VrpPlan>(
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
                search = new GeneticAlgorithm<VrpPlan>(
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

        return new VrpSolveResult(
            name,
            search.Best,
            search.Evaluation.ObjectiveValue,
            greedyDistance,
            search,
            settings);
    }
}
