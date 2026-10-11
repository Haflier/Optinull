using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;
using Optinull.Optimization.Search;

namespace Optinull.Application.Optimization;

/// <summary>
/// Wraps a search problem and reports how much of an evaluation budget has been
/// used. The solvers never know: they just see a problem.
/// </summary>
internal sealed class ProgressTrackingProblem<TSolution> :
    IRecombinableProblem<TSolution>,
    IConstructiveProblem<TSolution>
{
    private readonly ISearchProblem<TSolution> _inner;
    private readonly IProgress<double>? _progress;
    private readonly int _budget;
    private readonly int _step;

    private int _count;

    public ProgressTrackingProblem(
        ISearchProblem<TSolution> inner,
        int budget,
        IProgress<double>? progress)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _budget = Math.Max(1, budget);
        _progress = progress;
        _step = Math.Max(1, _budget / 100);
    }

    public ObjectiveType ObjectiveType => _inner.ObjectiveType;

    public TSolution CreateRandom(IRandomSource random) => _inner.CreateRandom(random);

    public EvaluationResult Evaluate(TSolution solution)
    {
        var result = _inner.Evaluate(solution);

        if (_progress is not null)
        {
            var count = Interlocked.Increment(ref _count);

            if (count % _step == 0)
                _progress.Report(Math.Min(1.0, (double)count / _budget));
        }

        return result;
    }

    public TSolution RandomNeighbor(TSolution solution, IRandomSource random) =>
        _inner.RandomNeighbor(solution, random);

    public IEnumerable<TSolution> Neighbors(TSolution solution) =>
        _inner.Neighbors(solution);

    public TSolution Crossover(TSolution first, TSolution second, IRandomSource random) =>
        Recombinable().Crossover(first, second, random);

    public TSolution Mutate(TSolution solution, IRandomSource random) =>
        Recombinable().Mutate(solution, random);

    public TSolution CreateGreedy(Func<TSolution, EvaluationResult> evaluate) =>
        _inner is IConstructiveProblem<TSolution> constructive
            ? constructive.CreateGreedy(evaluate)
            : throw new InvalidOperationException("This problem has no greedy heuristic.");

    private IRecombinableProblem<TSolution> Recombinable() =>
        _inner as IRecombinableProblem<TSolution>
        ?? throw new InvalidOperationException("This problem cannot combine solutions.");
}
