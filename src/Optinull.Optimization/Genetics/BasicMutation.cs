using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Domain.Variables;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Genetics;

public sealed class BasicMutation : IMutationStrategy
{
    private readonly IRandomSource _random;
    private readonly double _mutationRate;

    public BasicMutation(
        IRandomSource random,
        double mutationRate = 0.1)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (mutationRate < 0 || mutationRate > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mutationRate),
                "Mutation rate must be between 0 and 1.");
        }

        _random = random;
        _mutationRate = mutationRate;
    }

    public Solution Mutate(
        OptimizationProblem problem,
        Solution solution)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(solution);

        var mutated = Clone(solution);

        foreach (var variable in problem.Variables)
        {
            if (_random.NextDouble() > _mutationRate)
                continue;

            var currentValue = mutated.GetValue(variable);

            var newValue = variable.Type switch
            {
                VariableType.Binary =>
                    currentValue == 0 ? 1 : 0,

                VariableType.Integer =>
                    MutateInteger(variable, currentValue),

                VariableType.Continuous =>
                    MutateContinuous(variable, currentValue),

                _ => throw new InvalidOperationException(
                    $"Unsupported variable type: {variable.Type}")
            };

            mutated.SetValue(variable, newValue);
        }

        return mutated;
    }

    private double MutateInteger(
        Variable variable,
        double currentValue)
    {
        var direction = _random.Next(0, 2) == 0
            ? -1
            : 1;

        var candidate = currentValue + direction;

        if (candidate < variable.LowerBound ||
            candidate > variable.UpperBound)
        {
            candidate = currentValue - direction;
        }

        return candidate;
    }

    private double MutateContinuous(
        Variable variable,
        double currentValue)
    {
        var range =
            variable.UpperBound -
            variable.LowerBound;

        var perturbation =
            (_random.NextDouble() * 2 - 1) *
            range *
            0.1;

        return Math.Clamp(
            currentValue + perturbation,
            variable.LowerBound,
            variable.UpperBound);
    }

    private static Solution Clone(Solution source)
    {
        var clone = new Solution();

        foreach (var pair in source.Values)
        {
            clone.SetValue(
                pair.Key,
                pair.Value);
        }

        return clone;
    }
}
