using System.Diagnostics;

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

        ValidateKnownOptimalMakespan(
            knownOptimalMakespan);

        var evaluator =
            new JobShopEvaluator();

        var initialMakespan =
            evaluator
                .Evaluate(
                    problem,
                    initialSequence)
                .ObjectiveValue;

        var results =
            new List<JobShopExperimentResult>();

        results.Add(
            RunHillClimbing(
                problem,
                initialSequence,
                initialMakespan,
                knownOptimalMakespan));

        results.Add(
            RunSimulatedAnnealing(
                problem,
                initialSequence,
                initialMakespan,
                knownOptimalMakespan,
                randomSeed));

        results.Add(
            RunGeneticAlgorithm(
                problem,
                knownOptimalMakespan,
                randomSeed));

        results.Add(
            RunIteratedLocalSearch(
                problem,
                initialSequence,
                initialMakespan,
                knownOptimalMakespan,
                randomSeed));

        return results;
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

        ValidateKnownOptimalMakespan(
            knownOptimalMakespan);

        var evaluator =
            new JobShopEvaluator();

        var initialMakespan =
            evaluator
                .Evaluate(
                    problem,
                    initialSequence)
                .ObjectiveValue;

        var hillClimbingRuns =
            new List<JobShopExperimentResult>(
                runCount);

        var simulatedAnnealingRuns =
            new List<JobShopExperimentResult>(
                runCount);

        var geneticAlgorithmRuns =
            new List<JobShopExperimentResult>(
                runCount);

        var iteratedLocalSearchRuns =
            new List<JobShopExperimentResult>(
                runCount);

        for (var run = 0;
             run < runCount;
             run++)
        {
            var seed =
                startingRandomSeed + run;

            hillClimbingRuns.Add(
                RunHillClimbing(
                    problem,
                    initialSequence,
                    initialMakespan,
                    knownOptimalMakespan));

            simulatedAnnealingRuns.Add(
                RunSimulatedAnnealing(
                    problem,
                    initialSequence,
                    initialMakespan,
                    knownOptimalMakespan,
                    seed));

            geneticAlgorithmRuns.Add(
                RunGeneticAlgorithm(
                    problem,
                    knownOptimalMakespan,
                    seed));

            iteratedLocalSearchRuns.Add(
                RunIteratedLocalSearch(
                    problem,
                    initialSequence,
                    initialMakespan,
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
        JobShopProblem problem,
        JobSequence initialSequence,
        double initialMakespan,
        double? knownOptimalMakespan)
    {
        var countingEvaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        var solver =
            new JobShopHillClimbingSolver(
                countingEvaluator);

        var stopwatch =
            Stopwatch.StartNew();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        stopwatch.Stop();

        return CreateResult(
            "Hill Climbing",
            schedule.Makespan,
            stopwatch.Elapsed,
            countingEvaluator.EvaluationCount,
            initialMakespan,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult RunSimulatedAnnealing(
        JobShopProblem problem,
        JobSequence initialSequence,
        double initialMakespan,
        double? knownOptimalMakespan,
        int randomSeed)
    {
        var countingEvaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        var solver =
            new JobShopSimulatedAnnealingSolver(
                random: new Random(randomSeed),
                evaluator: countingEvaluator);

        var stopwatch =
            Stopwatch.StartNew();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        stopwatch.Stop();

        return CreateResult(
            "Simulated Annealing",
            schedule.Makespan,
            stopwatch.Elapsed,
            countingEvaluator.EvaluationCount,
            initialMakespan,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult RunGeneticAlgorithm(
        JobShopProblem problem,
        double? knownOptimalMakespan,
        int randomSeed)
    {
        var countingEvaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        var solver =
            new JobShopGeneticAlgorithmSolver(
                populationSize: 50,
                generations: 44,
                eliteCount: 2,
                mutationRate: 0.1,
                random: new Random(randomSeed),
                evaluator: countingEvaluator);

        var stopwatch =
            Stopwatch.StartNew();

        var schedule =
            solver.Solve(
                problem);

        stopwatch.Stop();

        return CreateResult(
            "Genetic Algorithm",
            schedule.Makespan,
            stopwatch.Elapsed,
            countingEvaluator.EvaluationCount,
            initialMakespan: null,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult RunIteratedLocalSearch(
        JobShopProblem problem,
        JobSequence initialSequence,
        double initialMakespan,
        double? knownOptimalMakespan,
        int randomSeed)
    {
        var countingEvaluator =
            new CountingJobShopEvaluator(
                new JobShopEvaluator());

        var solver =
            new JobShopIteratedLocalSearchSolver(
                perturbationSwapCount: 3,
                iterationCount: 10,
                random: new Random(randomSeed),
                evaluator: countingEvaluator);

        var stopwatch =
            Stopwatch.StartNew();

        var schedule =
            solver.Solve(
                problem,
                initialSequence);

        stopwatch.Stop();

        return CreateResult(
            "Iterated Local Search",
            schedule.Makespan,
            stopwatch.Elapsed,
            countingEvaluator.EvaluationCount,
            initialMakespan,
            knownOptimalMakespan);
    }

    private static JobShopExperimentResult CreateResult(
        string solverName,
        double makespan,
        TimeSpan elapsed,
        int evaluationCount,
        double? initialMakespan,
        double? knownOptimalMakespan)
    {
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
            elapsed,
            evaluationCount,
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
