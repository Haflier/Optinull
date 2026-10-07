using Optinull.Domain.Constraints;
using Optinull.Domain.Expressions;

namespace Optinull.Domain.Variables;

public sealed class Variable
{
    public string Name { get; }

    public VariableType Type { get; }

    public double LowerBound { get; }

    public double UpperBound { get; }

    public Variable(
        string name,
        VariableType type,
        double lowerBound,
        double upperBound)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Variable name cannot be empty.",
                nameof(name));

        if (lowerBound > upperBound)
            throw new ArgumentException(
                "Lower bound cannot be greater than upper bound.");

        if (type == VariableType.Binary &&
            (lowerBound != 0 || upperBound != 1))
        {
            throw new ArgumentException(
                "Binary variables must have bounds [0, 1].");
        }

        Name = name;
        Type = type;
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public Expression AsExpression()
    {
        return new VariableExpression(this);
    }

    // Allows:
    //
    // Expression expression = x;
    //
    // C# automatically converts Variable -> Expression.
    public static implicit operator Expression(Variable variable)
    {
        return variable.AsExpression();
    }

    // ------------------------------------------------------------
    // Arithmetic operators
    // ------------------------------------------------------------

    public static Expression operator +(
        Variable left,
        Variable right)
    {
        return left.AsExpression() + right.AsExpression();
    }

    public static Expression operator -(
        Variable left,
        Variable right)
    {
        return left.AsExpression() - right.AsExpression();
    }

    public static Expression operator *(
        Variable left,
        Variable right)
    {
        return left.AsExpression() * right.AsExpression();
    }

    public static Expression operator /(
        Variable left,
        Variable right)
    {
        return left.AsExpression() / right.AsExpression();
    }

    public static Expression operator -(
        Variable variable)
    {
        return -variable.AsExpression();
    }

    public static Expression operator *(
        Variable variable,
        double value)
    {
        return variable.AsExpression() * value;
    }

    public static Expression operator *(
        double value,
        Variable variable)
    {
        return value * variable.AsExpression();
    }

    public static Expression operator /(
    Variable variable,
    double value)
    {
        return variable.AsExpression() / value;
    }

    public static Expression operator /(
        double value,
        Variable variable)
    {
        return value / variable.AsExpression();
    }

    public static Expression operator +(
        Variable variable,
        double value)
    {
        return variable.AsExpression() + value;
    }

    public static Expression operator +(
        double value,
        Variable variable)
    {
        return value + variable.AsExpression();
    }

    public static Expression operator -(
        Variable variable,
        double value)
    {
        return variable.AsExpression() - value;
    }

    public static Expression operator -(
        double value,
        Variable variable)
    {
        return value - variable.AsExpression();
    }

    // ------------------------------------------------------------
    // Constraint methods
    // ------------------------------------------------------------

    // These methods exist because C# does not use the implicit
    // Variable -> Expression conversion when looking for an
    // instance method.
    //
    // So this:
    //
    //     x.LessThanOrEqual(1)
    //
    // needs a method on Variable itself.

    public ConstraintExpression LessThanOrEqual(
        Expression right)
    {
        return AsExpression().LessThanOrEqual(right);
    }

    public ConstraintExpression GreaterThanOrEqual(
        Expression right)
    {
        return AsExpression().GreaterThanOrEqual(right);
    }

    public ConstraintExpression EqualTo(
        Expression right)
    {
        return AsExpression().EqualTo(right);
    }
}
