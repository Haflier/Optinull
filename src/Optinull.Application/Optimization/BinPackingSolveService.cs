using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.BinPacking;

namespace Optinull.Application.Optimization;

public sealed record BinPackingSolveResult(
    string SolverName,
    BinPackingPlan Plan,
    int Bins,
    int GreedyBins,
    SearchResult<BinPackingPlan> Search,
    string? Settings = null);

/// <summary>
/// Solves a bin packing problem with the chosen solver at a fixed evaluation budget. The
/// search objective is the bin count plus a fraction below one, so a move changes it by a
/// small amount that shrinks as there are more bins; the temperature starts there.
/// </summary>
public sealed class BinPackingSolveService
{
    public const int EvaluationBudget = SolveOptions.DefaultBudget;

    public BinPackingSolveResult Solve(
        BinPackingProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var searchProblem = new BinPackingSearchProblem(problem);
        var random = new RandomSource(seed);
        var tune = tuning ?? SolverTuning.None;

        var budgeted = kind is SolverKind.SimulatedAnnealing
            or SolverKind.GeneticAlgorithm
            or SolverKind.IteratedLocalSearch;
        var tracked = new ProgressTrackingProblem<BinPackingPlan>(
            searchProblem,
            iterations ?? EvaluationBudget,
            budgeted ? progress : null);

        var greedyBins = searchProblem.CreateGreedy(searchProblem.Evaluate).BinCount;

        string name;
        string? settings = null;
        SearchResult<BinPackingPlan> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<BinPackingPlan>().Solve(tracked, cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<BinPackingPlan>(random).Solve(tracked, cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                var perturb = tune.PerturbationFor(problem.ItemCount);

                settings = perturb.Applied;
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<BinPackingPlan>(
                        perturbationSize: perturb.Size,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: iterations ?? EvaluationBudget)
                    .Solve(tracked, cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
            {
                var start = 1.0 / Math.Max(1, greedyBins);

                var sa = tune.Annealing(start, iterations ?? EvaluationBudget);

                settings = sa.Applied;

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<BinPackingPlan>(
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
                search = new GeneticAlgorithm<BinPackingPlan>(
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

        return new BinPackingSolveResult(name, search.Best, search.Best.BinCount, greedyBins, search, settings);
    }
}
