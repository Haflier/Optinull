using Optinull.Domain.Variables;

namespace Optinull.Domain.Solutions;

public sealed class Solution
{
    private readonly Dictionary<Variable, double> _values = [];

    public IReadOnlyDictionary<Variable, double> Values => _values;

    public Solution Clone()
    {
        var clone = new Solution();

        foreach (var pair in _values)
        {
            clone._values[pair.Key] = pair.Value;
        }

        return clone;
    }

    public void SetValue(
        Variable variable,
        double value)
    {
        ArgumentNullException.ThrowIfNull(variable);

        if (value < variable.LowerBound ||
            value > variable.UpperBound)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Value for '{variable.Name}' must be between " +
                $"{variable.LowerBound} and {variable.UpperBound}.");
        }

        if (variable.Type == VariableType.Binary &&
            value != 0 &&
            value != 1)
        {
            throw new ArgumentException(
                $"Binary variable '{variable.Name}' must be 0 or 1.",
                nameof(value));
        }

        if (variable.Type == VariableType.Integer &&
            value != Math.Truncate(value))
        {
            throw new ArgumentException(
                $"Integer variable '{variable.Name}' must have an integer value.",
                nameof(value));
        }

        _values[variable] = value;
    }

    public double GetValue(Variable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);

        if (!_values.TryGetValue(variable, out var value))
        {
            throw new InvalidOperationException(
                $"No value has been assigned to variable '{variable.Name}'.");
        }

        return value;
    }
}
