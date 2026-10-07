using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Comparison;

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

        var comparer = new SolutionComparer(
            problem.Objective.Type);

        var current = CreateInitialSolution(problem);

        var currentEvaluation = _evaluator.Evaluate(
            problem,
            current);

        if (!currentEvaluation.IsFeasible)
        {
            throw new InvalidOperationException(
                "The initial solution is infeasible.");
        }

        foreach (var variable in problem.Variables)
        {
            var bestCandidate = current;
            var bestEvaluation = currentEvaluation;

            var lowerCandidate = CopySolution(current);

            lowerCandidate.SetValue(
                variable,
                variable.LowerBound);

            var lowerEvaluation = _evaluator.Evaluate(
                problem,
                lowerCandidate);

            if (lowerEvaluation.IsFeasible &&
                comparer.IsBetter(
                    lowerEvaluation,
                    bestEvaluation))
            {
                bestCandidate = lowerCandidate;
                bestEvaluation = lowerEvaluation;
            }

            var upperCandidate = CopySolution(current);

            upperCandidate.SetValue(
                variable,
                variable.UpperBound);

            var upperEvaluation = _evaluator.Evaluate(
                problem,
                upperCandidate);

            if (upperEvaluation.IsFeasible &&
                comparer.IsBetter(
                    upperEvaluation,
                    bestEvaluation))
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
}
