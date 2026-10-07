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

        return _objectiveType switch
        {
            ObjectiveType.Maximize =>
                candidate.ObjectiveValue >
                current.ObjectiveValue,

            ObjectiveType.Minimize =>
                candidate.ObjectiveValue <
                current.ObjectiveValue,

            _ => throw new InvalidOperationException(
                $"Unsupported objective type: {_objectiveType}")
        };
    }
}
