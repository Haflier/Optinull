using Optinull.Domain.Expressions;

namespace Optinull.Domain.Objectives;

public sealed class Objective
{
    public Expression Expression { get; }

    public ObjectiveType Type { get; }

    public Objective(
        Expression expression,
        ObjectiveType type)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Expression = expression;
        Type = type;
    }
}
