using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Domain.Variables;

namespace Optinull.Optimization.Neighborhoods;

public sealed class BasicNeighborhoodGenerator : INeighborhoodGenerator
{
    private const double ContinuousStep = 1.0;

    public IEnumerable<Solution> Generate(
        OptimizationProblem problem,
        Solution solution)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(solution);

        foreach (var variable in problem.Variables)
        {
            var currentValue = solution.GetValue(variable);

            switch (variable.Type)
            {
                case VariableType.Binary:
                    yield return CreateNeighbor(
                        solution,
                        variable,
                        currentValue == 0 ? 1 : 0);
                    break;

                case VariableType.Integer:
                    if (currentValue > variable.LowerBound)
                    {
                        yield return CreateNeighbor(
                            solution,
                            variable,
                            currentValue - 1);
                    }

                    if (currentValue < variable.UpperBound)
                    {
                        yield return CreateNeighbor(
                            solution,
                            variable,
                            currentValue + 1);
                    }

                    break;

                case VariableType.Continuous:
                    var lower = currentValue - ContinuousStep;
                    var upper = currentValue + ContinuousStep;

                    if (lower >= variable.LowerBound)
                    {
                        yield return CreateNeighbor(
                            solution,
                            variable,
                            lower);
                    }

                    if (upper <= variable.UpperBound)
                    {
                        yield return CreateNeighbor(
                            solution,
                            variable,
                            upper);
                    }

                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported variable type: {variable.Type}");
            }
        }
    }

    private static Solution CreateNeighbor(
        Solution source,
        Variable variable,
        double value)
    {
        var neighbor = new Solution();

        foreach (var pair in source.Values)
        {
            neighbor.SetValue(
                pair.Key,
                pair.Value);
        }

        neighbor.SetValue(variable, value);

        return neighbor;
    }
}
