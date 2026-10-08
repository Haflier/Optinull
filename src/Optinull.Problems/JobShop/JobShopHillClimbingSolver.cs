using Optinull.Domain.Evaluation;

namespace Optinull.Problems.JobShop;

public sealed class JobShopHillClimbingSolver
{
    private readonly IJobShopEvaluator _evaluator;
    private readonly JobShopNeighborhoodGenerator _neighborhoodGenerator;
    private readonly JobShopScheduleDecoder _decoder;

    public JobShopHillClimbingSolver(
        IJobShopEvaluator? evaluator = null)
    {
        _evaluator = evaluator ?? new JobShopEvaluator();
        _neighborhoodGenerator = new JobShopNeighborhoodGenerator();
        _decoder = new JobShopScheduleDecoder();
    }

    public JobShopSchedule Solve(
        JobShopProblem problem,
        JobSequence initialSequence)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(initialSequence);

        var sequence =
            SolveSequence(
                problem,
                initialSequence);

        return _decoder.Decode(
            problem,
            sequence);
    }

    public JobSequence SolveSequence(
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

        while (true)
        {
            JobSequence? bestNeighbor = null;
            EvaluationResult? bestEvaluation = null;

            foreach (var neighbor in
                     _neighborhoodGenerator.Generate(
                         currentSequence))
            {
                var evaluation =
                    _evaluator.Evaluate(
                        problem,
                        neighbor);

                if (bestEvaluation is null ||
                    evaluation.ObjectiveValue <
                    bestEvaluation.ObjectiveValue)
                {
                    bestNeighbor = neighbor;
                    bestEvaluation = evaluation;
                }
            }

            if (bestNeighbor is null ||
                bestEvaluation is null)
                break;

            if (bestEvaluation.ObjectiveValue >=
                currentEvaluation.ObjectiveValue)
                break;

            currentSequence = bestNeighbor;
            currentEvaluation = bestEvaluation;
        }

        return currentSequence;
    }
}
