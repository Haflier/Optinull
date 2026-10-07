using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Domain.Variables;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Generation;

public sealed class RandomSolutionGenerator : ISolutionGenerator
{
    public Solution Generate(
        OptimizationProblem problem,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(random);

        var solution = new Solution();

        foreach (var variable in problem.Variables)
        {
            var value = GenerateValue(
                variable,
                random);

            solution.SetValue(
                variable,
                value);
        }

        return solution;
    }

    private static double GenerateValue(
        Variable variable,
        IRandomSource random)
    {
        return variable.Type switch
        {
            VariableType.Binary =>
                random.Next(0, 2),

            VariableType.Integer =>
                random.Next(
                    (int)variable.LowerBound,
                    (int)variable.UpperBound + 1),

            VariableType.Continuous =>
                variable.LowerBound +
                random.NextDouble() *
                (variable.UpperBound - variable.LowerBound),

            _ => throw new InvalidOperationException(
                $"Unsupported variable type: {variable.Type}")
        };
    }
}
