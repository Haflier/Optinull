using Optinull.Domain.Evaluation;

namespace Optinull.Problems.JobShop;

public sealed class JobShopIteratedLocalSearchSolver
{
    private readonly JobShopHillClimbingSolver _hillClimbingSolver;
    private readonly JobShopSequencePerturbation _perturbation;
    private readonly IJobShopEvaluator _evaluator;
    private readonly JobShopScheduleDecoder _decoder;
    private readonly Random _random;

    public JobShopIteratedLocalSearchSolver(
        int perturbationSwapCount = 3,
        int iterationCount = 100,
        Random? random = null,
        IJobShopEvaluator? evaluator = null)
    {
        if (perturbationSwapCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(perturbationSwapCount),
                "Perturbation swap count must be greater than zero.");
        }

        if (iterationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterationCount),
                "Iteration count must be greater than zero.");
        }

        _evaluator =
            evaluator ?? new JobShopEvaluator();

        _hillClimbingSolver =
            new JobShopHillClimbingSolver(
                _evaluator);

        _perturbation =
            new JobShopSequencePerturbation();

        _decoder =
            new JobShopScheduleDecoder();

        _random =
            random ?? new Random();

        PerturbationSwapCount = perturbationSwapCount;
        IterationCount = iterationCount;
    }

    public int PerturbationSwapCount { get; }

    public int IterationCount { get; }

    public JobShopSchedule Solve(
        JobShopProblem problem,
        JobSequence initialSequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(initialSequence);

        var currentSequence =
            _hillClimbingSolver.SolveSequence(
                problem,
                initialSequence);

        var currentMakespan =
            Evaluate(
                problem,
                currentSequence);

        var bestSequence =
            currentSequence;

        var bestMakespan =
            currentMakespan;

        for (var iteration = 0;
             iteration < IterationCount;
             iteration++)
        {
            var perturbedSequence =
                _perturbation.Perturb(
                    currentSequence,
                    PerturbationSwapCount,
                    _random);

            var improvedSequence =
                _hillClimbingSolver.SolveSequence(
                    problem,
                    perturbedSequence);

            var improvedMakespan =
                Evaluate(
                    problem,
                    improvedSequence);

            if (improvedMakespan < bestMakespan)
            {
                bestSequence =
                    improvedSequence;

                bestMakespan =
                    improvedMakespan;
            }

            currentSequence =
                improvedSequence;
        }

        return _decoder.Decode(
            problem,
            bestSequence);
    }

    private double Evaluate(
        JobShopProblem problem,
        JobSequence sequence)
    {
        return _evaluator
            .Evaluate(
                problem,
                sequence)
            .ObjectiveValue;
    }
}
