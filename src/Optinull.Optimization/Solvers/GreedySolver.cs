using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;

namespace Optinull.Optimization.Solvers;

public sealed class GreedySolver : IOptimizationSolver
{
    private readonly ProblemEvaluator _evaluator;

    public GreedySolver()
    {
        _evaluator = new ProblemEvaluator();
    }

    public OptimizationResult Solve(
        OptimizationProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.Objective is null)
        {
            throw new InvalidOperationException(
                "Cannot solve a problem without an objective.");
        }

        var current = CreateInitialSolution(problem);

        var currentEvaluation = _evaluator.Evaluate(
            problem,
            current);

        // GreedySolver only accepts feasible candidates.
        // If the initial solution is infeasible, this simple
        // greedy strategy cannot guarantee that it can recover.
        if (!currentEvaluation.IsFeasible)
        {
            throw new InvalidOperationException(
                "The initial solution is infeasible.");
        }

        foreach (var variable in problem.Variables)
        {
            var bestCandidate = current;
            var bestEvaluation = currentEvaluation;

            // Try the lower bound.
            var lowerCandidate = CopySolution(current);

            lowerCandidate.SetValue(
                variable,
                variable.LowerBound);

            var lowerEvaluation = _evaluator.Evaluate(
                problem,
                lowerCandidate);

            if (IsBetter(
                    problem,
                    lowerEvaluation,
                    bestEvaluation) &&
                lowerEvaluation.IsFeasible)
            {
                bestCandidate = lowerCandidate;
                bestEvaluation = lowerEvaluation;
            }

            // Try the upper bound.
            var upperCandidate = CopySolution(current);

            upperCandidate.SetValue(
                variable,
                variable.UpperBound);

            var upperEvaluation = _evaluator.Evaluate(
                problem,
                upperCandidate);

            if (IsBetter(
                    problem,
                    upperEvaluation,
                    bestEvaluation) &&
                upperEvaluation.IsFeasible)
            {
                bestCandidate = upperCandidate;
                bestEvaluation = upperEvaluation;
            }

            current = bestCandidate;
            currentEvaluation = bestEvaluation;
        }

        return new OptimizationResult(
            current,
            currentEvaluation);
    }

    private static Solution CreateInitialSolution(
        OptimizationProblem problem)
    {
        var solution = new Solution();

        foreach (var variable in problem.Variables)
        {
            solution.SetValue(
                variable,
                variable.LowerBound);
        }

        return solution;
    }

    private static Solution CopySolution(
        Solution source)
    {
        var copy = new Solution();

        foreach (var pair in source.Values)
        {
            copy.SetValue(
                pair.Key,
                pair.Value);
        }

        return copy;
    }

    private static bool IsBetter(
        OptimizationProblem problem,
        EvaluationResult candidate,
        EvaluationResult current)
    {
        if (!candidate.IsFeasible)
        {
            return false;
        }

        if (!current.IsFeasible)
        {
            return true;
        }

        return problem.Objective!.Type switch
        {
            Domain.Objectives.ObjectiveType.Maximize =>
                candidate.ObjectiveValue >
                current.ObjectiveValue,

            Domain.Objectives.ObjectiveType.Minimize =>
                candidate.ObjectiveValue <
                current.ObjectiveValue,

            _ => throw new InvalidOperationException(
                $"Unsupported objective type: " +
                $"{problem.Objective.Type}")
        };
    }
}
