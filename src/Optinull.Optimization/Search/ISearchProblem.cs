using Optinull.Domain.Evaluation;
using Optinull.Domain.Objectives;
using Optinull.Optimization.Randomness;

namespace Optinull.Optimization.Search;

/// <summary>
/// Everything a search algorithm needs to know about a problem.
/// Algorithms never see jobs, variables or Telegram: only this.
/// </summary>
public interface ISearchProblem<TSolution>
{
    ObjectiveType ObjectiveType { get; }

    TSolution CreateRandom(IRandomSource random);

    EvaluationResult Evaluate(TSolution solution);

    /// <summary>One random move; used by annealing and perturbation.</summary>
    TSolution RandomNeighbor(TSolution solution, IRandomSource random);

    /// <summary>The full neighborhood; used by hill climbing.</summary>
    IEnumerable<TSolution> Neighbors(TSolution solution);
}
