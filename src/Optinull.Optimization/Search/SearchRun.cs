using System.Diagnostics;
using Optinull.Domain.Evaluation;
using Optinull.Optimization.Comparison;

namespace Optinull.Optimization.Search;

/// <summary>
/// Per-run bookkeeping shared by all solvers: counts evaluations,
/// remembers the best solution ever seen, records convergence.
/// </summary>
internal sealed class SearchRun<TSolution>
{
    private readonly ISearchProblem<TSolution> _problem;
    private readonly SolutionComparer _comparer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly List<ConvergencePoint> _trace = [];

    private TSolution _best = default!;
    private EvaluationResult? _bestEvaluation;

    public SearchRun(ISearchProblem<TSolution> problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
        _comparer = new SolutionComparer(problem.ObjectiveType);
    }

    public int EvaluationCount { get; private set; }

    public EvaluationResult Evaluate(TSolution solution)
    {
        var evaluation = _problem.Evaluate(solution);
        EvaluationCount++;

        if (_bestEvaluation is null ||
            _comparer.IsBetter(evaluation, _bestEvaluation))
        {
            _best = solution;
            _bestEvaluation = evaluation;

            _trace.Add(
                new ConvergencePoint(
                    EvaluationCount,
                    evaluation.ObjectiveValue));
        }

        return evaluation;
    }

    public bool IsBetter(
        EvaluationResult candidate,
        EvaluationResult current) =>
        _comparer.IsBetter(candidate, current);

    public SearchResult<TSolution> Complete()
    {
        if (_bestEvaluation is null)
        {
            throw new InvalidOperationException(
                "No solution was evaluated.");
        }

        _stopwatch.Stop();

        return new SearchResult<TSolution>(
            _best,
            _bestEvaluation,
            EvaluationCount,
            _stopwatch.Elapsed,
            _trace.ToArray());
    }
}
