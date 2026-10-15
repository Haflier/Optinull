using Optinull.Application.Optimization;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.Knapsack;
using Optinull.Problems.Tsp;

namespace Optinull.Application.Tests.Tuning;

/// <summary>
/// By default the engine chooses the settings from the problem and the budget; whatever the
/// user sets replaces only that value. At the default budget the automatic values are the
/// ones the solvers have always had.
/// </summary>
public sealed class AutomaticSettingsTests
{
    private static string Tsp(SolverKind kind, int? iterations = null, SolverTuning? tuning = null, int cities = 20) =>
        new TspSolveService().Solve(
            TspBenchmarkInstances.RandomCities(cities, seed: 1), kind, seed: 1,
            iterations: iterations, tuning: tuning).Settings!;

    private static string Knapsack(SolverKind kind, int? iterations = null, int items = 20) =>
        new KnapsackSolveService().Solve(
            KnapsackInstances.Random(items, seed: 1), kind, seed: 1, iterations: iterations).Settings!;

    private static string JobShop(SolverKind kind, int? iterations = null) =>
        new JobShopSolveService().Solve(
            JobShopBenchmarkInstances.Ft06(), kind, seed: 1, iterations: iterations).Settings!;

    [Fact]
    public void AtTheDefaultBudget_TheAutomaticValuesAreTheLongStandingOnes()
    {
        Assert.Equal(
            "temperature=1 (auto), final=0.00001 (auto), moves=90 (auto)",
            Tsp(SolverKind.SimulatedAnnealing));

        Assert.Equal(
            "population=50 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=10 (auto)",
            Tsp(SolverKind.GeneticAlgorithm));

        Assert.Equal(
            "population=50 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=10 (auto)",
            Knapsack(SolverKind.GeneticAlgorithm));

        // The job shop has its own, FT06-tuned genetic settings.
        Assert.Equal(
            "population=30 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=30 (auto)",
            JobShop(SolverKind.GeneticAlgorithm));

        Assert.Equal(
            "temperature=1 (auto), final=0.00001 (auto), moves=90 (auto)",
            JobShop(SolverKind.SimulatedAnnealing));
    }

    [Fact]
    public void TheAnnealingScheduleKeepsItsShape_AtAnyBudget()
    {
        Assert.Contains("moves=5 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 500));
        Assert.Contains("moves=14 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 3_000));
        Assert.Contains("moves=90 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 20_000));
        Assert.Contains("moves=450 (auto)", Tsp(SolverKind.SimulatedAnnealing, iterations: 100_000));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(3_000)]
    [InlineData(20_000)]
    [InlineData(100_000)]
    public void TheAutomaticSchedule_StillFitsTheBudget(int iterations)
    {
        var result = new TspSolveService().Solve(
            TspBenchmarkInstances.RandomCities(20, seed: 1), SolverKind.SimulatedAnnealing, seed: 1,
            iterations: iterations);

        Assert.InRange(result.Search.EvaluationCount, iterations * 0.8, iterations * 1.2);
    }

    [Fact]
    public void ASmallBudget_GetsASmallerPopulation_SoItHasTimeToEvolve()
    {
        Assert.Contains("population=10 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 1_000));
        Assert.Contains("population=22 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 5_000));
        Assert.Contains("population=50 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 20_000));

        // A larger budget does not grow it beyond the problem's default.
        Assert.Contains("population=50 (auto)", Tsp(SolverKind.GeneticAlgorithm, iterations: 100_000));
    }

    [Fact]
    public void TheEliteAndTournament_FollowAnAutomaticPopulation()
    {
        var small = Tsp(SolverKind.GeneticAlgorithm, iterations: 1_000);

        Assert.Contains("population=10 (auto)", small);
        Assert.Contains("elite=2 (auto)", small);
        Assert.Contains("tournament=3 (auto)", small);
    }

    [Fact]
    public void WhatTheUserSets_IsUsedAsGiven_AndTheRestStaysAutomatic()
    {
        var settings = Tsp(
            SolverKind.GeneticAlgorithm,
            tuning: new SolverTuning { Population = 20, Mutation = 0.35 });

        Assert.Contains("population=20 (yours)", settings);
        Assert.Contains("mutation=0.35 (yours)", settings);
        Assert.Contains("elite=2 (auto)", settings);
        Assert.Contains("tournament=3 (auto)", settings);
        Assert.Contains("localsearch=10 (auto)", settings);
    }

    [Fact]
    public void ALargeUserPopulation_IsNotShrunkByTheBudget()
    {
        var settings = Tsp(
            SolverKind.GeneticAlgorithm,
            iterations: 1_000,
            tuning: new SolverTuning { Population = 200 });

        Assert.Contains("population=200 (yours)", settings);
    }

    [Fact]
    public void ASmallUserPopulation_GetsAFittingAutomaticEliteAndTournament()
    {
        var settings = Tsp(
            SolverKind.GeneticAlgorithm,
            tuning: new SolverTuning { Population = 2 });

        Assert.Contains("population=2 (yours)", settings);
        Assert.Contains("elite=2 (auto)", settings);
        Assert.Contains("tournament=2 (auto)", settings);
    }

    [Fact]
    public void AnnealingOverrides_AreMarked_AndCombineWithTheAutomaticMoves()
    {
        var settings = Tsp(
            SolverKind.SimulatedAnnealing,
            iterations: 3_000,
            tuning: new SolverTuning { Temperature = 2 });

        Assert.Equal("temperature=2 (yours), final=0.00001 (auto), moves=14 (auto)", settings);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(20, 3)]
    [InlineData(50, 3)]
    [InlineData(60, 3)]
    [InlineData(100, 5)]
    [InlineData(150, 8)]
    [InlineData(200, 8)]
    [InlineData(100_000, 8)]
    public void ThePerturbationRule_GrowsWithTheProblem_ButStaysBetween3And8(int size, int expected)
    {
        var settings = SolverTuning.None.PerturbationFor(size);

        Assert.Equal(expected, settings.Size);
        Assert.Equal($"perturbation={expected} (auto)", settings.Applied);
    }

    [Fact]
    public void ThePerturbationRule_IsReachedByTheSolver() =>
        Assert.Equal("perturbation=3 (auto)", Tsp(SolverKind.IteratedLocalSearch, iterations: 500, cities: 20));

    [Theory]
    [InlineData(500, 5)]
    [InlineData(1_000, 5)]
    [InlineData(3_000, 14)]
    [InlineData(20_000, 90)]
    [InlineData(100_000, 450)]
    [InlineData(10_000_000, 1000)]
    public void TheAnnealingMovesRule_FollowsTheBudget_WithinItsLimits(int budget, int expected) =>
        Assert.Equal(expected, SolverTuning.None.Annealing(1.0, budget).Moves);

    [Theory]
    [InlineData(100, 10)]
    [InlineData(1_000, 10)]
    [InlineData(5_000, 22)]
    [InlineData(20_000, 50)]
    [InlineData(10_000_000, 50)]
    public void TheGeneticPopulationRule_FollowsTheBudget_NeverAboveTheProblemsDefault(int budget, int expected) =>
        Assert.Equal(expected, SolverTuning.None.Genetic(50, 0.2, 3, 10, budget).Population);

    [Theory]
    [InlineData(10, 2)]
    [InlineData(30, 2)]
    [InlineData(50, 2)]
    [InlineData(100, 4)]
    [InlineData(200, 8)]
    public void TheEliteRule_ScalesWithAnAutomaticPopulation(int population, int expected) =>
        Assert.Equal(
            expected,
            new SolverTuning { Population = population }.Genetic(50, 0.2, 3, 10, 20_000).Elite);

    [Fact]
    public void ATemperatureTheUserSets_ScalesTheProblemsUsualStart()
    {
        var settings = new SolverTuning { Temperature = 4 }.Annealing(usualStartTemperature: 25, budget: 20_000);

        Assert.Equal(100, settings.InitialTemperature);
        Assert.Equal(90, settings.Moves);
        Assert.Equal(0.00001, settings.FinalRatio);
    }

    [Fact]
    public void ThePerturbation_UsesTheItemCountOfAKnapsack_AndTheOperationsOfAJobShop()
    {
        Assert.Equal("perturbation=3 (auto)", Knapsack(SolverKind.IteratedLocalSearch, iterations: 500, items: 40));
        Assert.Equal("perturbation=5 (auto)", Knapsack(SolverKind.IteratedLocalSearch, iterations: 500, items: 100));
        Assert.Equal("perturbation=3 (auto)", JobShop(SolverKind.IteratedLocalSearch, iterations: 500));
    }

    [Fact]
    public void APerturbationTheUserSets_WinsOverTheAutomaticOne()
    {
        Assert.Equal("perturbation=7 (yours)", new SolverTuning { Perturbation = 7 }.PerturbationFor(1_000).Applied);
        Assert.Equal(
            "perturbation=7 (yours)",
            Tsp(SolverKind.IteratedLocalSearch, iterations: 500, tuning: new SolverTuning { Perturbation = 7 }));
    }

    [Fact]
    public void GreedyAndHillClimbing_HaveNoSettings()
    {
        var problem = TspBenchmarkInstances.RandomCities(20, seed: 1);

        Assert.Null(new TspSolveService().Solve(problem, SolverKind.Greedy).Settings);
        Assert.Null(new TspSolveService().Solve(problem, SolverKind.HillClimbing).Settings);
    }

    [Fact]
    public void ARunIsReproducible_WhenTheEngineChoosesTheSettings()
    {
        var problem = KnapsackInstances.Random(40, seed: 3);
        var service = new KnapsackSolveService();

        foreach (var kind in new[] { SolverKind.SimulatedAnnealing, SolverKind.GeneticAlgorithm, SolverKind.IteratedLocalSearch })
        {
            var first = service.Solve(problem, kind, seed: 5, iterations: 2_000);
            var second = service.Solve(problem, kind, seed: 5, iterations: 2_000);

            Assert.Equal(first.Value, second.Value);
            Assert.Equal(first.Settings, second.Settings);
        }
    }
}
