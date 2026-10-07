namespace Optinull.Domain.Expressions;

public sealed class UnaryExpression : Expression
{
    public Expression Operand { get; }

    public UnaryExpression(
        ExpressionType type,
        Expression operand)
        : base(type)
    {
        if (type != ExpressionType.Negate)
        {
            throw new ArgumentException(
                "Invalid unary expression type.",
                nameof(type));
        }

        Operand = operand;
    }
}
