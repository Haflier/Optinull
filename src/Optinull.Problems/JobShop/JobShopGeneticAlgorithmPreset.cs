using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.JobShop;

/// <summary>
/// GA configuration tuned on FT06 (30 seeds, 20,000 evaluations): the optimum
/// of 55 was reached on every seed. On FT10 it trails simulated annealing
/// at the same budget (about 1028 vs 993), so treat it as FT06-tuned. The operators and the GA parameters belong
/// together, so both are created here.
/// </summary>
public static class JobShopGeneticAlgorithmPreset
{
    public const int DefaultMaxEvaluations = 20_000;
    public const int PopulationSize = 30;
    public const int EliteCount = 2;
    public const double MutationRate = 0.2;
    public const int TournamentSize = 3;
    public const int LocalSearchTries = 30;

    public static JobShopSearchProblem CreateProblem(
        JobShopProblem problem,
        IJobShopEvaluator? evaluator = null) =>
        new(
            problem,
            evaluator,
            JobShopCrossover.PrecedencePreserving,
            JobShopMutation.Insertion);

    public static GeneticAlgorithm<JobSequence> CreateSolver(
        int maxEvaluations = DefaultMaxEvaluations,
        IRandomSource? random = null) =>
        new(
            populationSize: PopulationSize,
            generations: 1_000_000,
            eliteCount: EliteCount,
            mutationRate: MutationRate,
            tournamentSize: TournamentSize,
            localSearchTries: LocalSearchTries,
            maxEvaluations: maxEvaluations,
            random: random);
}
