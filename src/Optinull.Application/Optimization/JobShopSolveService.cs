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
    public JobShopSolveResult Solve(
        JobShopProblem problem,
        SolverKind kind = SolverKind.SimulatedAnnealing,
        int seed = SolveOptions.DefaultSeed,
        CancellationToken cancellationToken = default,
        int? iterations = null,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var random = new RandomSource(seed);
        var budget = iterations ?? SolveOptions.DefaultBudget;

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

            case SolverKind.SimulatedAnnealing:
                name = SolverNames.Display(kind);
                search = new SimulatedAnnealing<JobSequence>(
                        coolingRate: AnnealingBudget.CoolingRate(iterations),
                        iterationsPerTemperature: AnnealingBudget.IterationsPerTemperature,
                        random: random)
                    .Solve(
                        new ProgressTrackingProblem<JobSequence>(
                            new JobShopSearchProblem(problem), budget, progress),
                        cancellationToken);
                break;

            case SolverKind.GeneticAlgorithm:
                name = SolverNames.Display(kind);
                search = JobShopGeneticAlgorithmPreset
                    .CreateSolver(budget, random)
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
