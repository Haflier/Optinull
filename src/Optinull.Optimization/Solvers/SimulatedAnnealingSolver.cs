using Optinull.Domain.Evaluation;
using Optinull.Domain.Problems;
using Optinull.Domain.Solutions;
using Optinull.Optimization.Comparison;
using Optinull.Optimization.Neighborhoods;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Solvers;

public sealed class SimulatedAnnealingSolver : IOptimizationSolver
{
    private readonly ProblemEvaluator _evaluator;
    private readonly INeighborhoodGenerator _neighborhoodGenerator;
    private readonly IRandomSource _random;
    private ISolutionComparer _comparer = null!;

    private readonly double _initialTemperature;
    private readonly double _coolingRate;
    private readonly int _iterationsPerTemperature;

    public SimulatedAnnealingSolver(
        double initialTemperature = 100.0,
        double coolingRate = 0.95,
        int iterationsPerTemperature = 10,
        IRandomSource? random = null)
    {
        if (initialTemperature <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialTemperature),
                "Initial temperature must be greater than zero.");
        }

        if (coolingRate <= 0 || coolingRate >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coolingRate),
                "Cooling rate must be greater than zero and less than one.");
        }

        if (iterationsPerTemperature <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterationsPerTemperature),
                "Iterations per temperature must be greater than zero.");
        }

        _evaluator = new ProblemEvaluator();
        _neighborhoodGenerator = new BasicNeighborhoodGenerator();
        _random = random ?? new RandomSource();

        _initialTemperature = initialTemperature;
        _coolingRate = coolingRate;
        _iterationsPerTemperature = iterationsPerTemperature;
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

        // The comparer is specific to this problem's objective.
        _comparer = new SolutionComparer(
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

        // "current" may temporarily become worse.
        //
        // "best" always remembers the best solution found
        // during the entire search.
        var best = current;
        var bestEvaluation = currentEvaluation;

        var temperature = _initialTemperature;

        while (temperature > 0.001)
        {
            for (var iteration = 0;
                 iteration < _iterationsPerTemperature;
                 iteration++)
            {
                var neighbors = _neighborhoodGenerator
                    .Generate(problem, current)
                    .Select(neighbor => (
                        Solution: neighbor,
                        Evaluation: _evaluator.Evaluate(
                            problem,
                            neighbor)))
                    .Where(item => item.Evaluation.IsFeasible)
                    .ToList();

                if (neighbors.Count == 0)
                    continue;

                // Choose a random neighbor.
                //
                // Unlike Hill Climbing, we do not automatically
                // choose the best neighbor.
                var selected = neighbors[
                    _random.Next(0, neighbors.Count)];

                if (ShouldAccept(
                        currentEvaluation,
                        selected.Evaluation,
                        temperature))
                {
                    current = selected.Solution;
                    currentEvaluation = selected.Evaluation;

                    if (_comparer.IsBetter(
                            currentEvaluation,
                            bestEvaluation))
                    {
                        best = current;
                        bestEvaluation = currentEvaluation;
                    }
                }
            }

            // Cooling schedule:
            //
            // 100 → 95 → 90.25 → 85.74 → ...
            temperature *= _coolingRate;
        }

        return new OptimizationResult(
            best,
            bestEvaluation);
    }

    private bool ShouldAccept(
        EvaluationResult current,
        EvaluationResult candidate,
        double temperature)
    {
        if (!candidate.IsFeasible)
            return false;

        // Always accept an improvement.
        if (_comparer.IsBetter(
                candidate,
                current))
        {
            return true;
        }

        // Candidate is worse.
        //
        // At high temperature this probability is larger.
        // As temperature approaches zero it approaches zero.
        var difference = Math.Abs(
            candidate.ObjectiveValue -
            current.ObjectiveValue);

        var probability = Math.Exp(
            -difference / temperature);

        return _random.NextDouble() < probability;
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
