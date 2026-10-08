using Optinull.Domain.Evaluation;

namespace Optinull.Optimization.Search;

/// <summary>
/// A problem that can build a decent solution directly (a greedy
/// heuristic). Used as the baseline other solvers are measured against.
/// </summary>
public interface IConstructiveProblem<TSolution> : ISearchProblem<TSolution>
{
    /// <param name="evaluate">
    /// Evaluation function supplied by the run, so evaluations the
    /// heuristic performs are counted.
    /// </param>
    TSolution CreateGreedy(Func<TSolution, EvaluationResult> evaluate);
}
