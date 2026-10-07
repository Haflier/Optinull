using Optinull.Domain.Variables;

namespace Optinull.Domain.Expressions;

public sealed class VariableExpression : Expression
{
    public Variable Variable { get; }

    public VariableExpression(Variable variable)
        : base(ExpressionType.Variable)
    {
        Variable = variable;
    }
}
