using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopGeneticAlgorithmExperimentTests
{
    private const int Runs = 30;
    private const int Budget = 20_000;
    private const double KnownOptimum = 55;

    private sealed record Variant(
        string Name,
        JobShopCrossover Crossover,
        JobShopMutation Mutation,
        int Population,
        double MutationRate,
        int LocalSearchTries);

    [Fact(Skip = "Manual GA tuning experiment; remove Skip to run (a minute or two).")]
    public void CompareGeneticAlgorithmVariants()
    {
        var variants = new List<Variant>
        {
            new("OnePoint + Swap, p50, m0.1 (current)",
                JobShopCrossover.OnePoint, JobShopMutation.Swap, 50, 0.1, 0),
            new("POX + Swap, p50, m0.1",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Swap, 50, 0.1, 0),
            new("POX + Insertion, p50, m0.1",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 50, 0.1, 0),
            new("POX + Insertion, p50, m0.4",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 50, 0.4, 0),
            new("POX + Insertion, p100, m0.2",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 100, 0.2, 0),
            new("POX + Insertion, p20, m0.2",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 20, 0.2, 0),
            new("POX + Insertion, p30, m0.2, LS10",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 30, 0.2, 10),
            new("POX + Insertion, p30, m0.2, LS30",
                JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 30, 0.2, 30),
        };

        Console.WriteLine(
            $"FT06, {Runs} seeds, budget {Budget} evaluations per run");
        Console.WriteLine();
        Console.WriteLine(
            $"{"Variant",-40} {"Avg",7} {"Best",5} {"Worst",5} {"Hit55",6} {"Evals",7}");

        foreach (var variant in variants)
        {
            var problem = new JobShopSearchProblem(
                JobShopBenchmarkInstances.Ft06(),
                crossover: variant.Crossover,
                mutation: variant.Mutation);

            var results = RunAll(seed =>
                new GeneticAlgorithm<JobSequence>(
                        populationSize: variant.Population,
                        generations: 1_000_000,
                        eliteCount: 2,
                        mutationRate: variant.MutationRate,
                        tournamentSize: 3,
                        localSearchTries: variant.LocalSearchTries,
                        maxEvaluations: Budget,
                        random: new RandomSource(seed))
                    .Solve(problem));

            Print(variant.Name, results);
        }

        // Reference: simulated annealing at the same budget.
        var reference = new JobShopSearchProblem(JobShopBenchmarkInstances.Ft06());

        Print(
            "Simulated annealing (reference)",
            RunAll(seed =>
                new SimulatedAnnealing<JobSequence>(
                        iterationsPerTemperature: 90,
                        random: new RandomSource(seed))
                    .Solve(reference)));
    }

    private static SearchResult<JobSequence>[] RunAll(
        Func<int, SearchResult<JobSequence>> solve)
    {
        var results = new SearchResult<JobSequence>[Runs];

        Parallel.For(0, Runs, run => results[run] = solve(42 + run));

        return results;
    }

    private static void Print(
        string name,
        IReadOnlyList<SearchResult<JobSequence>> results)
    {
        var makespans = results
            .Select(result => result.Evaluation.ObjectiveValue)
            .ToList();

        Console.WriteLine(
            $"{name,-40} " +
            $"{makespans.Average(),7:F2} " +
            $"{makespans.Min(),5:F0} " +
            $"{makespans.Max(),5:F0} " +
            $"{makespans.Count(m => m == KnownOptimum) * 100.0 / makespans.Count,5:F0}% " +
            $"{results.Average(result => result.EvaluationCount),7:F0}");

        Assert.All(makespans, makespan => Assert.True(makespan >= KnownOptimum));
    }
}
