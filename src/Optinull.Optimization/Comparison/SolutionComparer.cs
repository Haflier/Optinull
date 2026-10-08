using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;

namespace Optinull.Optimization.Comparison;

public sealed class SolutionComparer : ISolutionComparer
{
    private readonly ObjectiveType _objectiveType;

    public SolutionComparer(ObjectiveType objectiveType)
    {
        _objectiveType = objectiveType;
    }

    public bool IsBetter(
        EvaluationResult candidate,
        EvaluationResult current)
    {
        if (!candidate.IsFeasible)
            return false;

        if (!current.IsFeasible)
            return true;

        var candidateScore = PenalizedValue(candidate);
        var currentScore = PenalizedValue(current);

        return _objectiveType switch
        {
            ObjectiveType.Maximize => candidateScore > currentScore,
            ObjectiveType.Minimize => candidateScore < currentScore,

            _ => throw new InvalidOperationException(
                $"Unsupported objective type: {_objectiveType}")
        };
    }

    // A penalty always makes a solution worse: it lowers a value that is
    // maximized and raises one that is minimized.
    private double PenalizedValue(EvaluationResult evaluation) =>
        _objectiveType switch
        {
            ObjectiveType.Maximize =>
                evaluation.ObjectiveValue - evaluation.Penalty,

            ObjectiveType.Minimize =>
                evaluation.ObjectiveValue + evaluation.Penalty,

            _ => throw new InvalidOperationException(
                $"Unsupported objective type: {_objectiveType}")
        };
}
