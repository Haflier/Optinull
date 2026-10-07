using Optinull.Domain.Constraints;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Evaluation;

public sealed class ConstraintEvaluator
{
    private readonly ExpressionEvaluator _expressionEvaluator;

    public ConstraintEvaluator()
    {
        _expressionEvaluator = new ExpressionEvaluator();
    }

    public bool IsSatisfied(
        Constraint constraint,
        Solution solution)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ArgumentNullException.ThrowIfNull(solution);

        var left = _expressionEvaluator.Evaluate(
            constraint.Left,
            solution);

        var right = _expressionEvaluator.Evaluate(
            constraint.Right,
            solution);

        return constraint.Type switch
        {
            ConstraintType.LessThanOrEqual =>
                left <= right,

            ConstraintType.GreaterThanOrEqual =>
                left >= right,

            ConstraintType.Equal =>
                Math.Abs(left - right) < 1e-9,

            _ => throw new InvalidOperationException(
                $"Unsupported constraint type: {constraint.Type}")
        };
    }

    public double CalculatePenalty(
        Constraint constraint,
        Solution solution)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ArgumentNullException.ThrowIfNull(solution);

        if (!constraint.IsSoft)
        {
            return 0;
        }

        if (IsSatisfied(constraint, solution))
        {
            return 0;
        }

        var left = _expressionEvaluator.Evaluate(
            constraint.Left,
            solution);

        var right = _expressionEvaluator.Evaluate(
            constraint.Right,
            solution);

        var violation = constraint.Type switch
        {
            ConstraintType.LessThanOrEqual =>
                Math.Max(0, left - right),

            ConstraintType.GreaterThanOrEqual =>
                Math.Max(0, right - left),

            ConstraintType.Equal =>
                Math.Abs(left - right),

            _ => throw new InvalidOperationException(
                $"Unsupported constraint type: {constraint.Type}")
        };

        return violation * constraint.Penalty;
    }
}
