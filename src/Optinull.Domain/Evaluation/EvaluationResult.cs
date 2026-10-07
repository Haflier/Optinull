namespace Optinull.Domain.Evaluation;

public sealed class EvaluationResult
{
    public double ObjectiveValue { get; }

    public bool IsFeasible { get; }

    public double Penalty { get; }

    public EvaluationResult(
        double objectiveValue,
        bool isFeasible,
        double penalty)
    {
        ObjectiveValue = objectiveValue;
        IsFeasible = isFeasible;
        Penalty = penalty;
    }
}
