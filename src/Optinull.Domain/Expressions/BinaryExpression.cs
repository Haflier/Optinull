namespace Optinull.Domain.Expressions;

public sealed class BinaryExpression : Expression
{
    public Expression Left { get; }

    public Expression Right { get; }

    public BinaryExpression(
        ExpressionType type,
        Expression left,
        Expression right)
        : base(type)
    {
        if (type is not (
            ExpressionType.Add or
            ExpressionType.Subtract or
            ExpressionType.Multiply or
            ExpressionType.Divide))
        {
            throw new ArgumentException(
                "Invalid binary expression type.",
                nameof(type));
        }

        Left = left;
        Right = right;
    }
}
