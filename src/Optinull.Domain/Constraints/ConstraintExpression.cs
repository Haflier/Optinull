using Optinull.Domain.Expressions;

namespace Optinull.Domain.Constraints;

public readonly struct ConstraintExpression
{
    public Expression Left { get; }

    public ConstraintType Type { get; }

    public Expression Right { get; }

    public ConstraintExpression(
        Expression left,
        ConstraintType type,
        Expression right)
    {
        Left = left;
        Type = type;
        Right = right;
    }

    public Constraint ToHardConstraint()
    {
        return new Constraint(
            Left,
            Type,
            Right);
    }

    public Constraint ToSoftConstraint(double penalty)
    {
        return new Constraint(
            Left,
            Type,
            Right,
            isSoft: true,
            penalty: penalty);
    }
}
