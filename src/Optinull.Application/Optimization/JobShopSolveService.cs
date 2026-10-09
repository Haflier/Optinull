using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;

namespace Optinull.Application.Optimization;

public enum JobShopSolverKind
{
    SimulatedAnnealing,
    GeneticAlgorithm
}

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
        JobShopSolverKind kind = JobShopSolverKind.SimulatedAnnealing,
        int seed = 42,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var random = new RandomSource(seed);

        string name;
        SearchResult<JobSequence> search;

        switch (kind)
        {
            case JobShopSolverKind.SimulatedAnnealing:
                name = "Simulated Annealing";
                search = new SimulatedAnnealing<JobSequence>(
                        iterationsPerTemperature: 90,
                        random: random)
                    .Solve(new JobShopSearchProblem(problem), cancellationToken);
                break;

            case JobShopSolverKind.GeneticAlgorithm:
                name = "Genetic Algorithm";
                search = JobShopGeneticAlgorithmPreset
                    .CreateSolver(random: random)
                    .Solve(
                        JobShopGeneticAlgorithmPreset.CreateProblem(problem),
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
