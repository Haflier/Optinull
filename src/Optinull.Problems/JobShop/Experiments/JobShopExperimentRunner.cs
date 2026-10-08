using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Problems.JobShop.Experiments;

public sealed class JobShopExperimentRunner
{
    public IReadOnlyList<JobShopExperimentResult> Run(
        JobShopProblem problem,
        JobSequence initialSequence,
        double? knownOptimalMakespan = null,
        int randomSeed = 42)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(initialSequence);

        ValidateKnownOptimalMakespan(knownOptimalMakespan);

        var searchProblem = new JobShopSearchProblem(problem);

        var geneticProblem = JobShopGeneticAlgorithmPreset.CreateProblem(problem);

        var initialMakespan =
            searchProblem.Evaluate(initialSequence).ObjectiveValue;

        return
        [
            RunHillClimbing(
                searchProblem,
                initialSequence,
                initialMakespan,
                knownOptimalMakespan),

            RunSimulatedAnnealing(
                searchProblem,
                initialSequence,
                initialMakespan,
                knownOptimalMakespan,
                randomSeed),

            RunGeneticAlgorithm(
                geneticProblem,
                knownOptimalMakespan,
                randomSeed),

            RunIteratedLocalSearch(
                searchProblem,
                initialSequence,
                initialMakespan,
                knownOptimalMakespan,
                randomSeed)
        ];
    }

    public IReadOnlyList<JobShopExperimentSummary> RunMultiple(
        JobShopProblem problem,
        JobSequence initialSequence,
        int runCount,
        double? knownOptimalMakespan = null,
        int startingRandomSeed = 42)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(initialSequence);

        if (runCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runCount),
                "Run count must be greater than zero.");
        }

        ValidateKnownOptimalMakespan(knownOptimalMakespan);

        var searchProblem = new JobShopSearchProblem(problem);

        var geneticProblem = JobShopGeneticAlgorithmPreset.CreateProblem(problem);

        var hillClimbingRuns = new List<JobShopExperimentResult>(runCount);
        var simulatedAnnealingRuns = new List<JobShopExperimentResult>(runCount);
        var geneticAlgorithmRuns = new List<JobShopExperimentResult>(runCount);
        var iteratedLocalSearchRuns = new List<JobShopExperimentResult>(runCount);

        for (var run = 0; run < runCount; run++)
        {
            var seed = startingRandomSeed + run;

            // Every run gets its own random start, shared by the
            // solvers that need one so the comparison is fair.
            var runInitialSequence =
                searchProblem.CreateRandom(new RandomSource(seed));

            var runInitialMakespan =
                searchProblem.Evaluate(runInitialSequence).ObjectiveValue;

            hillClimbingRuns.Add(
                RunHillClimbing(
                    searchProblem,
                    runInitialSequence,
                    runInitialMakespan,
                    knownOptimalMakespan));

            simulatedAnnealingRuns.Add(
                RunSimulatedAnnealing(
                    searchProblem,
                    runInitialSequence,
                    runInitialMakespan,
                    knownOptimalMakespan,
                    seed));

            geneticAlgorithmRuns.Add(
                RunGeneticAlgorithm(
                    geneticProblem,
                    knownOptimalMakespan,
                    seed));

            iteratedLocalSearchRuns.Add(
                RunIteratedLocalSearch(
                    searchProblem,
                    runInitialSequence,
                    runInitialMakespan,
                    knownOptimalMakespan,
                    seed));
        }

        return
        [
            new JobShopExperimentSummary(
                "Hill Climbing",
                hillClimbingRuns,
                knownOptimalMakespan),

            new JobShopExperimentSummary(
                "Simulated Annealing",
                simulatedAnnealingRuns,
                knownOptimalMakespan),

            new JobShopExperimentSummary(
                "Genetic Algorithm",
                geneticAlgorithmRuns,
                knownOptimalMakespan),

            new JobShopExperimentSummary(
                "Iterated Local Search",
                iteratedLocalSearchRuns,
                knownOptimalMakespan)
        ];
    }

    private static JobShopExperimentResult RunHillClimbing(
        JobShopSearchProblem problem,
        JobSequence initialSequence,
        double initialMakespan,
        double? knownOptimalMakespan)
    {
        var result =
            new HillClimbing<JobSequence>()
                .Solve(problem, initialSequence);

        return CreateResult(
            "Hill Climbing",
            result,
            initialMakespan,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult RunSimulatedAnnealing(
        JobShopSearchProblem problem,
        JobSequence initialSequence,
        double initialMakespan,
        double? knownOptimalMakespan,
        int randomSeed)
    {
        var result =
            new SimulatedAnnealing<JobSequence>(
                    iterationsPerTemperature: 90,
                    random: new RandomSource(randomSeed))
                .Solve(problem, initialSequence);

        return CreateResult(
            "Simulated Annealing",
            result,
            initialMakespan,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult RunGeneticAlgorithm(
        JobShopSearchProblem problem,
        double? knownOptimalMakespan,
        int randomSeed)
    {
        var result = JobShopGeneticAlgorithmPreset
              .CreateSolver(random: new RandomSource(randomSeed))
              .Solve(problem);

        return CreateResult(
            "Genetic Algorithm",
            result,
            initialMakespan: null,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult RunIteratedLocalSearch(
        JobShopSearchProblem problem,
        JobSequence initialSequence,
        double initialMakespan,
        double? knownOptimalMakespan,
        int randomSeed)
    {
        var result =
            new IteratedLocalSearch<JobSequence>(
                    perturbationSize: 3,
                    iterationCount: 10,
                    random: new RandomSource(randomSeed))
                .Solve(problem, initialSequence);

        return CreateResult(
            "Iterated Local Search",
            result,
            initialMakespan,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult CreateResult(
        string solverName,
        SearchResult<JobSequence> result,
        double? initialMakespan,
        double? knownOptimalMakespan)
    {
        var makespan = result.Evaluation.ObjectiveValue;

        double? optimalityGap = null;

        if (knownOptimalMakespan.HasValue)
        {
            optimalityGap =
                (makespan - knownOptimalMakespan.Value)
                / knownOptimalMakespan.Value;
        }

        return new JobShopExperimentResult(
            solverName,
            makespan,
            result.Elapsed,
            result.EvaluationCount,
            initialMakespan,
            optimalityGap);
    }

    private static void ValidateKnownOptimalMakespan(
        double? knownOptimalMakespan)
    {
        if (knownOptimalMakespan is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(knownOptimalMakespan),
                "Known optimal makespan must be greater than zero.");
        }
    }
}
