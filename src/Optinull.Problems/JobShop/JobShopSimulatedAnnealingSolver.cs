namespace Optinull.Problems.JobShop;

public sealed class JobShopSimulatedAnnealingSolver
{
    private readonly IJobShopEvaluator _evaluator;
    private readonly JobShopScheduleDecoder _decoder;
    private readonly Random _random;

    private readonly double _initialTemperature;
    private readonly double _coolingRate;
    private readonly int _iterationsPerTemperature;

    public JobShopSimulatedAnnealingSolver(
        double initialTemperature = 100.0,
        double coolingRate = 0.95,
        int iterationsPerTemperature = 10,
        Random? random = null,
        IJobShopEvaluator? evaluator = null)
    {
        if (initialTemperature <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialTemperature),
                "Initial temperature must be greater than zero.");
        }

        if (coolingRate <= 0 || coolingRate >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coolingRate),
                "Cooling rate must be greater than zero and less than one.");
        }

        if (iterationsPerTemperature <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterationsPerTemperature),
                "Iterations per temperature must be greater than zero.");
        }

        _evaluator = evaluator ?? new JobShopEvaluator();
        _decoder = new JobShopScheduleDecoder();
        _random = random ?? Random.Shared;

        _initialTemperature = initialTemperature;
        _coolingRate = coolingRate;
        _iterationsPerTemperature = iterationsPerTemperature;
    }

    public JobShopSchedule Solve(
        JobShopProblem problem,
        JobSequence initialSequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(initialSequence);

        var currentSequence = initialSequence;

        var currentEvaluation =
            _evaluator.Evaluate(
                problem,
                currentSequence);

        var bestSequence = currentSequence;
        var bestEvaluation = currentEvaluation;

        var temperature = _initialTemperature;

        while (temperature > 0.001)
        {
            for (var iteration = 0;
                 iteration < _iterationsPerTemperature;
                 iteration++)
            {
                var candidateSequence =
                    Mutate(currentSequence);

                var candidateEvaluation =
                    _evaluator.Evaluate(
                        problem,
                        candidateSequence);

                if (ShouldAccept(
                        currentEvaluation.ObjectiveValue,
                        candidateEvaluation.ObjectiveValue,
                        temperature))
                {
                    currentSequence = candidateSequence;
                    currentEvaluation = candidateEvaluation;

                    if (currentEvaluation.ObjectiveValue <
                        bestEvaluation.ObjectiveValue)
                    {
                        bestSequence = currentSequence;
                        bestEvaluation = currentEvaluation;
                    }
                }
            }

            temperature *= _coolingRate;
        }

        return _decoder.Decode(
            problem,
            bestSequence);
    }

    private JobSequence Mutate(
        JobSequence sequence)
    {
        if (sequence.Count < 2)
        {
            return new JobSequence(sequence.JobIds);
        }

        var jobIds =
            sequence.JobIds.ToList();

        var firstIndex =
            _random.Next(jobIds.Count);

        var secondIndex =
            _random.Next(jobIds.Count - 1);

        if (secondIndex >= firstIndex)
        {
            secondIndex++;
        }

        (jobIds[firstIndex], jobIds[secondIndex]) =
            (jobIds[secondIndex], jobIds[firstIndex]);

        return new JobSequence(jobIds);
    }

    private bool ShouldAccept(
        double currentObjective,
        double candidateObjective,
        double temperature)
    {
        if (candidateObjective < currentObjective)
        {
            return true;
        }

        var difference =
            candidateObjective - currentObjective;

        var probability =
            Math.Exp(-difference / temperature);

        return _random.NextDouble() < probability;
    }
}
