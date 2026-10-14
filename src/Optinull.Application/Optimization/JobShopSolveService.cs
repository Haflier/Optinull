using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;

namespace Optinull.Application.Optimization;

public sealed record JobShopSolveResult(
    string SolverName,
    JobShopSchedule Schedule,
    double Makespan,
    SearchResult<JobSequence> Search);

/// <summary>
/// Solves a job shop with the chosen solver. Simulated annealing is the
/// default: it beat the genetic algorithm on FT10 at an equal budget.
/// </summary>
public sealed class JobShopSolveService
{
    /// <summary>The start temperature simulated annealing has always used here.</summary>
    private const double UsualStartTemperature = 100.0;

    public JobShopSolveResult Solve(
        JobShopProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null,
        SolverTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var random = new RandomSource(seed);
        var budget = iterations ?? SolveOptions.DefaultBudget;
        var tune = tuning ?? SolverTuning.None;

        string name;
        SearchResult<JobSequence> search;

        switch (kind)
        {
            case SolverKind.Greedy:
                name = SolverNames.Display(kind);
                search = new GreedyBaseline<JobSequence>()
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, null),
                        cancellationToken);
                break;

            case SolverKind.HillClimbing:
                name = SolverNames.Display(kind);
                search = new HillClimbing<JobSequence>(random)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, null),
                        cancellationToken);
                break;

            case SolverKind.IteratedLocalSearch:
                name = SolverNames.Display(kind);
                search = new IteratedLocalSearch<JobSequence>(
                        perturbationSize: tune.Perturbation ?? 3,
                        iterationCount: int.MaxValue,
                        random: random,
                        maxEvaluations: budget)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, progress),
                        cancellationToken);
                break;

            case SolverKind.SimulatedAnnealing:
                var sa = tune.Annealing(UsualStartTemperature);

                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<JobSequence>(
                        initialTemperature: sa.InitialTemperature,
                        coolingRate: AnnealingBudget.CoolingRate(iterations, sa.Moves, sa.FinalRatio),
                        iterationsPerTemperature: sa.Moves,
                        minimumTemperature: sa.InitialTemperature * sa.FinalRatio,
                        random: random)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, progress),
                        cancellationToken);
                break;

            case SolverKind.GeneticAlgorithm:
                var ga = tune.Genetic(
                    JobShopGeneticAlgorithmPreset.PopulationSize,
                    JobShopGeneticAlgorithmPreset.EliteCount,
                    JobShopGeneticAlgorithmPreset.MutationRate,
                    JobShopGeneticAlgorithmPreset.TournamentSize,
                    JobShopGeneticAlgorithmPreset.LocalSearchTries);

                name = SolverNames.Display(kind);
                search = JobShopGeneticAlgorithmPreset
                    .CreateSolver(
                        budget,
                        random,
                        ga.Population,
                        ga.Elite,
                        ga.Mutation,
                        ga.Tournament,
                        ga.LocalSearch)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            JobShopGeneticAlgorithmPreset.CreateProblem(problem), budget, progress),
                        cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var schedule = new JobShopScheduleDecoder().Decode(problem, search.Best);

        return new JobShopSolveResult(
            name,
            schedule,
            search.Evaluation.ObjectiveValue,
            search);
    }
}
