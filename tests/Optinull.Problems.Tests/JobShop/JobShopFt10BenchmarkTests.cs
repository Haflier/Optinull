using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopFt10BenchmarkTests
{
    private const int Runs = 30;
    private const int Budget = 20_000;
    private const double KnownOptimum = 930;

    [Fact]
    public void Ft10_HasTenJobsVisitingEveryMachineOnce()
    {
        var problem = JobShopBenchmarkInstances.Ft10();

        Assert.Equal(10, problem.MachineCount);
        Assert.Equal(10, problem.Jobs.Count);

        Assert.All(problem.Jobs, job =>
            Assert.Equal(
                Enumerable.Range(0, 10),
                job.Operations.Select(o => o.MachineId).OrderBy(m => m)));
    }

    [Fact(Skip = "Manual GA vs SA experiment on FT10; remove Skip to run (several minutes).")]
    public void CompareSolversOnFt10()
    {
        Console.WriteLine($"FT10, {Runs} seeds, budget {Budget} evaluations per run");
        Console.WriteLine();
        Console.WriteLine(
            $"{"Variant",-40} {"Avg",7} {"Best",5} {"Worst",5} {"Gap%",6} {"Evals",7}");

        PrintGa("OnePoint + Swap, p50, m0.1 (old default)",
            JobShopCrossover.OnePoint, JobShopMutation.Swap, 50, 0.1, 0);

        PrintGa("POX + Insertion, p100, m0.2",
            JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 100, 0.2, 0);

        PrintGa("POX + Insertion, p30, m0.2, LS10",
            JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 30, 0.2, 10);

        PrintGa("POX + Insertion, p30, m0.2, LS30 (preset)",
            JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 30, 0.2, 30);

        PrintGa("POX + Insertion, p60, m0.2, LS30",
            JobShopCrossover.PrecedencePreserving, JobShopMutation.Insertion, 60, 0.2, 30);

        var reference = new JobShopSearchProblem(JobShopBenchmarkInstances.Ft10());

        Print(
            "Simulated annealing (reference)",
            RunAll(seed =>
                new SimulatedAnnealing<JobSequence>(
                        iterationsPerTemperature: 90,
                        random: new RandomSource(seed))
                    .Solve(reference)));
    }

    private static void PrintGa(
        string name,
        JobShopCrossover crossover,
        JobShopMutation mutation,
        int population,
        double mutationRate,
        int localSearchTries)
    {
        var problem = new JobShopSearchProblem(
            JobShopBenchmarkInstances.Ft10(),
            crossover: crossover,
            mutation: mutation);

        Print(
            name,
            RunAll(seed =>
                new GeneticAlgorithm<JobSequence>(
                        populationSize: population,
                        generations: 1_000_000,
                        eliteCount: 2,
                        mutationRate: mutationRate,
                        tournamentSize: 3,
                        localSearchTries: localSearchTries,
                        maxEvaluations: Budget,
                        random: new RandomSource(seed))
                    .Solve(problem)));
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

        var gap = (makespans.Average() - KnownOptimum) / KnownOptimum * 100;

        Console.WriteLine(
            $"{name,-40} " +
            $"{makespans.Average(),7:F1} " +
            $"{makespans.Min(),5:F0} " +
            $"{makespans.Max(),5:F0} " +
            $"{gap,5:F1}% " +
            $"{results.Average(result => result.EvaluationCount),7:F0}");

        // A makespan below the known optimum means a typo in the instance data.
        Assert.All(makespans, makespan => Assert.True(makespan >= KnownOptimum));
    }
}
