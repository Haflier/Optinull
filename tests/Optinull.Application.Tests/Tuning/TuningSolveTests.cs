using Optinull.Application.Optimization;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests.Tuning;

public sealed class TuningSolveTests
{
    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    [InlineData(SolverKind.IteratedLocalSearch)]
    public void EmptyTuning_ChangesNothing_OnEveryProblem(SolverKind kind)
    {
        var tsp = TspBenchmarkInstances.RandomCities(25, seed: 2);
        var knapsack = KnapsackInstances.Random(30, seed: 2);
        var jobShop = JobShopBenchmarkInstances.Ft06();

        Assert.Equal(
            new TspSolveService().Solve(tsp, kind, seed: 1, iterations: 1000).Length,
            new TspSolveService().Solve(tsp, kind, seed: 1, iterations: 1000, tuning: SolverTuning.None).Length);

        Assert.Equal(
            new KnapsackSolveService().Solve(knapsack, kind, seed: 1, iterations: 1000).Value,
            new KnapsackSolveService().Solve(knapsack, kind, seed: 1, iterations: 1000, tuning: SolverTuning.None).Value);

        Assert.Equal(
            new JobShopSolveService().Solve(jobShop, kind, seed: 1, iterations: 1000).Makespan,
            new JobShopSolveService().Solve(jobShop, kind, seed: 1, iterations: 1000, tuning: SolverTuning.None).Makespan);
    }

    [Fact]
    public void TheDefaultAnnealingSchedule_IsUnchanged_WithoutAnIterationCount()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        var plain = new TspSolveService().Solve(problem, SolverKind.SimulatedAnnealing, seed: 4);
        var tuned = new TspSolveService().Solve(
            problem, SolverKind.SimulatedAnnealing, seed: 4,
            tuning: new SolverTuning { Temperature = 1, Moves = 90, Final = 0.00001 });

        Assert.Equal(plain.Length, tuned.Length);
        Assert.Equal(plain.Search.EvaluationCount, tuned.Search.EvaluationCount);
    }

    [Theory]
    [InlineData(10, 0.00001)]
    [InlineData(300, 0.00001)]
    [InlineData(90, 0.001)]
    [InlineData(90, 0.4)]
    [InlineData(5, 0.05)]
    public void ACustomSchedule_StillFitsTheEvaluationBudget(int moves, double final)
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        var result = new TspSolveService().Solve(
            problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 3000,
            tuning: new SolverTuning { Moves = moves, Final = final });

        // A schedule of whole temperature levels may end a little under or over the budget.
        Assert.InRange(result.Search.EvaluationCount, 2000, 4000);
    }

    [Fact]
    public void Temperature_ReachesTheAnnealer_OnEveryProblem()
    {
        var cold = new SolverTuning { Temperature = 0.01 };
        var hot = new SolverTuning { Temperature = 50 };

        var tsp = TspBenchmarkInstances.RandomCities(50, seed: 7);
        var jobShop = JobShopBenchmarkInstances.Ft10();
        var knapsack = KnapsackInstances.Random(100, seed: 11);

        Assert.NotEqual(
            new TspSolveService().Solve(tsp, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: cold).Length,
            new TspSolveService().Solve(tsp, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: hot).Length);

        Assert.NotEqual(
            new JobShopSolveService().Solve(jobShop, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: cold).Makespan,
            new JobShopSolveService().Solve(jobShop, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: hot).Makespan);

        Assert.NotEqual(
            new KnapsackSolveService().Solve(knapsack, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: cold).Value,
            new KnapsackSolveService().Solve(knapsack, SolverKind.SimulatedAnnealing, 1, iterations: 3000, tuning: hot).Value);
    }

    [Fact]
    public void GeneticSettings_ReachTheGeneticAlgorithm()
    {
        var tsp = TspBenchmarkInstances.RandomCities(50, seed: 7);

        double Length(SolverTuning tuning) =>
            new TspSolveService().Solve(tsp, SolverKind.GeneticAlgorithm, 1, iterations: 3000, tuning: tuning).Length;

        Assert.NotEqual(Length(new SolverTuning { Population = 10 }), Length(new SolverTuning { Population = 100 }));
        Assert.NotEqual(Length(new SolverTuning { Mutation = 0 }), Length(new SolverTuning { Mutation = 0.9 }));
        Assert.NotEqual(Length(new SolverTuning { LocalSearch = 0 }), Length(new SolverTuning { LocalSearch = 50 }));
    }

    [Fact]
    public void JobShopGeneticSettings_ReachThePreset_AndTheScheduleStaysValid()
    {
        var problem = JobShopBenchmarkInstances.Ft06();

        var result = new JobShopSolveService().Solve(
            problem, SolverKind.GeneticAlgorithm, seed: 2, iterations: 2000,
            tuning: new SolverTuning { Population = 10, Mutation = 0.5, Elite = 1, Tournament = 2, LocalSearch = 5 });

        Assert.True(result.Makespan >= 55);
        Assert.Equal(result.Makespan, result.Schedule.Makespan);
        Assert.Equal(36, result.Schedule.Operations.Count);
    }

    [Fact]
    public void Perturbation_ReachesIteratedLocalSearch()
    {
        // Iterated local search finishes its first climb before it looks at the budget, so
        // the instance is small and the budget large enough for several restarts. Both runs
        // may well end on the same best tour, but the restarts that led there differ.
        var tsp = TspBenchmarkInstances.RandomCities(25, seed: 7);

        TspSolveResult Run(int perturbation) =>
            new TspSolveService().Solve(
                tsp, SolverKind.IteratedLocalSearch, 1, iterations: 60_000,
                tuning: new SolverTuning { Perturbation = perturbation });

        var gentle = Run(1);
        var violent = Run(15);

        Assert.False(
            gentle.Length == violent.Length &&
            gentle.Search.EvaluationCount == violent.Search.EvaluationCount,
            "Perturbation 1 and 15 produced the same run, so the setting is not reaching the solver.");
    }

    [Fact]
    public void Perturbation_NeedsRoomForARestart_SoATinyBudgetOnALargeInstanceIgnoresIt()
    {
        // Documents a limit of the solver, not of the tuning: the first climb alone costs more
        // than the whole budget here, so no restart (and no perturbation) ever happens.
        var tsp = TspBenchmarkInstances.RandomCities(50, seed: 7);

        double Length(int perturbation) =>
            new TspSolveService().Solve(
                tsp, SolverKind.IteratedLocalSearch, 1, iterations: 500,
                tuning: new SolverTuning { Perturbation = perturbation }).Length;

        Assert.Equal(Length(1), Length(15));
    }

    [Fact]
    public void SolversWithoutSettings_IgnoreTuning()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 3);
        var tuning = new SolverTuning { Temperature = 5, Population = 9 };

        foreach (var kind in new[] { SolverKind.Greedy, SolverKind.HillClimbing })
        {
            Assert.Equal(
                new TspSolveService().Solve(problem, kind, seed: 1).Length,
                new TspSolveService().Solve(problem, kind, seed: 1, tuning: tuning).Length);
        }
    }

    [Fact]
    public void ASolverIgnoresTheOtherSolversSettings_WhenAllRunTogether()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 3);

        Assert.Equal(
            new TspSolveService().Solve(problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 1000).Length,
            new TspSolveService().Solve(
                problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 1000,
                tuning: new SolverTuning { Population = 9, Mutation = 0.9, Perturbation = 20 }).Length);
    }

    [Fact]
    public void EliteLargerThanThePopulation_IsRefusedWithAClearMessage()
    {
        var problem = KnapsackInstances.Random(20, seed: 1);

        var error = Assert.Throws<TuningException>(() =>
            new KnapsackSolveService().Solve(
                problem, SolverKind.GeneticAlgorithm, iterations: 500,
                tuning: new SolverTuning { Population = 5, Elite = 9 }));

        Assert.Equal("elite (9) cannot be more than population (5).", error.Message);
    }

    [Fact]
    public void TournamentLargerThanThePopulation_IsRefused()
    {
        var problem = KnapsackInstances.Random(20, seed: 1);

        var error = Assert.Throws<TuningException>(() =>
            new KnapsackSolveService().Solve(
                problem, SolverKind.GeneticAlgorithm, iterations: 500,
                tuning: new SolverTuning { Population = 5, Tournament = 9 }));

        Assert.Contains("tournament (9)", error.Message);
    }

    [Fact]
    public void TheDefaultPopulationDiffersPerProblem_SoTheSameEliteCanBeFineForOneAndNotTheOther()
    {
        var tuning = new SolverTuning { Elite = 40 };

        // At the default budget the job shop's population is 30 and the TSP's is 50.
        var error = Assert.Throws<TuningException>(() =>
            new JobShopSolveService().Solve(
                JobShopBenchmarkInstances.Ft06(), SolverKind.GeneticAlgorithm, tuning: tuning));

        Assert.Equal("elite (40) cannot be more than population (30).", error.Message);

        var result = new TspSolveService().Solve(
            TspBenchmarkInstances.RandomCities(15, seed: 1), SolverKind.GeneticAlgorithm, tuning: tuning);

        Assert.True(result.Length > 0);
    }

    [Fact]
    public void TheCatalog_CoversTheThreeTunableSolvers() =>
        Assert.Equal(
            new[] { SolverKind.SimulatedAnnealing, SolverKind.GeneticAlgorithm, SolverKind.IteratedLocalSearch },
            TuningCatalog.Keys.Select(key => key.Solver).Distinct().ToArray());
}
