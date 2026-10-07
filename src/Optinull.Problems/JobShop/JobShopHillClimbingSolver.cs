using Optinull.Domain.Evaluation;

namespace Optinull.Problems.JobShop;

public sealed class JobShopHillClimbingSolver
{
    private readonly JobShopEvaluator _evaluator;
    private readonly JobShopNeighborhoodGenerator _neighborhoodGenerator;

    public JobShopHillClimbingSolver()
    {
        _evaluator = new JobShopEvaluator();
        _neighborhoodGenerator =
            new JobShopNeighborhoodGenerator();
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

            // No neighbors means we are already at a local optimum.
            if (bestNeighbor is null ||
                bestEvaluation is null)
            {
                break;
            }

            // Hill Climbing only accepts an actual improvement.
            if (bestEvaluation.ObjectiveValue >=
                currentEvaluation.ObjectiveValue)
            {
                break;
            }

            currentSequence = bestNeighbor;
            currentEvaluation = bestEvaluation;
        }

        return new JobShopScheduleDecoder()
            .Decode(
                problem,
                currentSequence);
    }
}
