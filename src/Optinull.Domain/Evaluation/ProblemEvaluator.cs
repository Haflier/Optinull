using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Domain.Evaluation;

public sealed class ProblemEvaluator
{
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly ConstraintEvaluator _constraintEvaluator;

    public ProblemEvaluator()
    {
        _expressionEvaluator = new ExpressionEvaluator();
        _constraintEvaluator = new ConstraintEvaluator();
    }

    public EvaluationResult Evaluate(
        OptimizationProblem problem,
        Solution solution)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(solution);

        if (problem.Objective is null)
        {
            throw new InvalidOperationException(
                "Cannot evaluate a problem without an objective.");
        }

        var objectiveValue =
            _expressionEvaluator.Evaluate(
                problem.Objective.Expression,
                solution);

        var isFeasible = true;
        var penalty = 0.0;

        foreach (var constraint in problem.Constraints)
        {
            if (!_constraintEvaluator.IsSatisfied(
                    constraint,
                    solution))
            {
                if (!constraint.IsSoft)
                {
                    isFeasible = false;
                }
            }

            penalty += _constraintEvaluator.CalculatePenalty(
                constraint,
                solution);
        }

        return new EvaluationResult(
            objectiveValue,
            isFeasible,
            penalty);
    }
}
