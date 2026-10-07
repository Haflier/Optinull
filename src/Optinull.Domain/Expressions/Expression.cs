using Optinull.Domain.Constraints;

namespace Optinull.Domain.Expressions;

public abstract class Expression
{
    public ExpressionType Type { get; }

    protected Expression(ExpressionType type)
    {
        Type = type;
    }

    public static BinaryExpression operator +(
        Expression left,
        Expression right)
    {
        return new BinaryExpression(
            ExpressionType.Add,
            left,
            right);
    }

    public static BinaryExpression operator -(
        Expression left,
        Expression right)
    {
        return new BinaryExpression(
            ExpressionType.Subtract,
            left,
            right);
    }

    public static BinaryExpression operator *(
        Expression left,
        Expression right)
    {
        return new BinaryExpression(
            ExpressionType.Multiply,
            left,
            right);
    }

    public static BinaryExpression operator /(
        Expression left,
        Expression right)
    {
        return new BinaryExpression(
            ExpressionType.Divide,
            left,
            right);
    }

    public static UnaryExpression operator -(
        Expression operand)
    {
        return new UnaryExpression(
            ExpressionType.Negate,
            operand);
    }

    public static implicit operator Expression(double value)
    {
        return new ConstantExpression(value);
    }

    public ConstraintExpression LessThanOrEqual(
        Expression right)
    {
        return new ConstraintExpression(
            this,
            ConstraintType.LessThanOrEqual,
            right);
    }

    public ConstraintExpression GreaterThanOrEqual(
        Expression right)
    {
        return new ConstraintExpression(
            this,
            ConstraintType.GreaterThanOrEqual,
            right);
    }

    public ConstraintExpression EqualTo(
        Expression right)
    {
        return new ConstraintExpression(
            this,
            ConstraintType.Equal,
            right);
    }
}
