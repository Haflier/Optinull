using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Comparison;
using Optinull.Optimization.Neighborhoods;
using Optinull.Domain.Evaluation;

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

        while (true)
        {
            var bestNeighbor = current;
            var bestEvaluation = currentEvaluation;

            foreach (var neighbor in
                     _neighborhoodGenerator.Generate(
                         problem,
                         current))
            {
                var evaluation = _evaluator.Evaluate(
                    problem,
                    neighbor);

                if (!evaluation.IsFeasible)
                    continue;

                if (comparer.IsBetter(
                        evaluation,
                        bestEvaluation))
                {
                    bestNeighbor = neighbor;
                    bestEvaluation = evaluation;
                }
            }

            if (ReferenceEquals(bestNeighbor, current))
                break;

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
}
