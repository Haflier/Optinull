using Optinull.Domain.Expressions;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Evaluation;

public sealed class ExpressionEvaluator
{
    public double Evaluate(
        Expression expression,
        Solution solution)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(solution);

        return expression switch
        {
            ConstantExpression constant =>
                constant.Value,

            VariableExpression variable =>
                solution.GetValue(variable.Variable),

            BinaryExpression binary =>
                EvaluateBinary(binary, solution),

            UnaryExpression unary =>
                EvaluateUnary(unary, solution),

            _ => throw new InvalidOperationException(
                $"Unsupported expression type: {expression.GetType().Name}")
        };
    }

    private double EvaluateBinary(
        BinaryExpression expression,
        Solution solution)
    {
        var left = Evaluate(
            expression.Left,
            solution);

        var right = Evaluate(
            expression.Right,
            solution);

        return expression.Type switch
        {
            ExpressionType.Add =>
                left + right,

            ExpressionType.Subtract =>
                left - right,

            ExpressionType.Multiply =>
                left * right,

            ExpressionType.Divide =>
                right == 0
                    ? throw new DivideByZeroException()
                    : left / right,

            _ => throw new InvalidOperationException(
                $"Invalid binary expression type: {expression.Type}")
        };
    }

    private double EvaluateUnary(
        UnaryExpression expression,
        Solution solution)
    {
        var operand = Evaluate(
            expression.Operand,
            solution);

        return expression.Type switch
        {
            ExpressionType.Negate =>
                -operand,

            _ => throw new InvalidOperationException(
                $"Invalid unary expression type: {expression.Type}")
        };
    }
}
