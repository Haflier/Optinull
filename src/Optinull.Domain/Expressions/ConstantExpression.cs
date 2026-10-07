namespace Optinull.Domain.Expressions;

public sealed class ConstantExpression : Expression
{
    public double Value { get; }

    public ConstantExpression(double value)
        : base(ExpressionType.Constant)
    {
        Value = value;
    }
}
