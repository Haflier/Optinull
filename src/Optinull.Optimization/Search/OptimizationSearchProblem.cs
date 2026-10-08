using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Domain.Variables;
using Optinull.Optimization.Generation;
using Optinull.Optimization.Neighborhoods;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

/// <summary>Adapts the expression-based OptimizationProblem to ISearchProblem.</summary>
public sealed class OptimizationSearchProblem : ISearchProblem<Solution>
{
    private const double ContinuousStep = 1.0;

    private readonly OptimizationProblem _problem;
    private readonly ProblemEvaluator _evaluator = new();
    private readonly ISolutionGenerator _generator;
    private readonly INeighborhoodGenerator _neighborhood;

    public OptimizationSearchProblem(
        OptimizationProblem problem,
        ISolutionGenerator? generator = null,
        INeighborhoodGenerator? neighborhood = null)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.Objective is null)
        {
            throw new InvalidOperationException(
                "Cannot search a problem without an objective.");
        }

        _problem = problem;
        _generator = generator ?? new RandomSolutionGenerator();
        _neighborhood = neighborhood ?? new BasicNeighborhoodGenerator();
    }

    public ObjectiveType ObjectiveType => _problem.Objective!.Type;

    public Solution CreateRandom(IRandomSource random) =>
        _generator.Generate(_problem, random);

    public EvaluationResult Evaluate(Solution solution) =>
        _evaluator.Evaluate(_problem, solution);

    public IEnumerable<Solution> Neighbors(Solution solution) =>
        _neighborhood.Generate(_problem, solution);

    public Solution RandomNeighbor(Solution solution, IRandomSource random)
    {
        var variables = _problem.Variables;

        if (variables.Count == 0)
            return solution;

        // Start at a random variable; move on if it cannot change.
        var start = random.Next(0, variables.Count);

        for (var offset = 0; offset < variables.Count; offset++)
        {
            var variable = variables[(start + offset) % variables.Count];

            if (TryStep(
                    variable,
                    solution.GetValue(variable),
                    random,
                    out var value))
            {
                var neighbor = solution.Clone();
                neighbor.SetValue(variable, value);
                return neighbor;
            }
        }

        return solution;
    }

    private static bool TryStep(
        Variable variable,
        double current,
        IRandomSource random,
        out double value)
    {
        if (variable.Type == VariableType.Binary)
        {
            value = current == 0 ? 1 : 0;
            return true;
        }

        var step = variable.Type == VariableType.Integer
            ? 1.0
            : ContinuousStep;

        var direction = random.Next(0, 2) == 0 ? -1 : 1;

        var first = current + direction * step;
        if (first >= variable.LowerBound && first <= variable.UpperBound)
        {
            value = first;
            return true;
        }

        var second = current - direction * step;
        if (second >= variable.LowerBound && second <= variable.UpperBound)
        {
            value = second;
            return true;
        }

        value = current;
        return false;
    }
}
