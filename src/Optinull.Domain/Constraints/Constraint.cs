using Optinull.Domain.Expressions;

namespace Optinull.Domain.Constraints;

public sealed class Constraint
{
    public Expression Left { get; }

    public ConstraintType Type { get; }

    public Expression Right { get; }

    public bool IsSoft { get; }

    public double Penalty { get; }

    public Constraint(
        Expression left,
        ConstraintType type,
        Expression right,
        bool isSoft = false,
        double penalty = 0)
    {
        if (isSoft && penalty <= 0)
        {
            throw new ArgumentException(
                "Soft constraint penalty must be greater than zero.",
                nameof(penalty));
        }

        if (!isSoft && penalty != 0)
        {
            throw new ArgumentException(
                "Hard constraints cannot have a penalty.",
                nameof(penalty));
        }

        Left = left;
        Type = type;
        Right = right;
        IsSoft = isSoft;
        Penalty = penalty;
    }
}
