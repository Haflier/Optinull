using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Neighborhoods;

namespace Optinull.Optimization.Solvers;

public sealed class HillClimbingSolver : IOptimizationSolver
{
    private readonly ProblemEvaluator _evaluator;
    private readonly INeighborhoodGenerator _neighborhoodGenerator;

    public HillClimbingSolver()
    {
        _evaluator = new ProblemEvaluator();
        _neighborhoodGenerator = new BasicNeighborhoodGenerator();
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

        if (!currentEvaluation.IsFeasible)
        {
            throw new InvalidOperationException(
                "The initial solution is infeasible.");
        }

        while (true)
        {
            var bestNeighbor = current;
            var bestEvaluation = currentEvaluation;

            foreach (var neighbor in
                     _neighborhoodGenerator.Generate(problem, current))
            {
                var evaluation = _evaluator.Evaluate(
                    problem,
                    neighbor);

                if (!evaluation.IsFeasible)
                    continue;

                if (IsBetter(
                        problem,
                        evaluation,
                        bestEvaluation))
                {
                    bestNeighbor = neighbor;
                    bestEvaluation = evaluation;
                }
            }

            // No neighbor improves the current solution.
            // We have reached a local optimum.
            if (ReferenceEquals(bestNeighbor, current))
            {
                break;
            }

            current = bestNeighbor;
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

    private static bool IsBetter(
        OptimizationProblem problem,
        EvaluationResult candidate,
        EvaluationResult current)
    {
        if (!candidate.IsFeasible)
            return false;

        if (!current.IsFeasible)
            return true;

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
