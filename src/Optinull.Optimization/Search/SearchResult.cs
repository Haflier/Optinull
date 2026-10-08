using Optinull.Domain.Evaluation;

namespace Optinull.Optimization.Search;

public readonly record struct ConvergencePoint(
    int Evaluations,
    double Objective);

public sealed record SearchResult<TSolution>(
    TSolution Best,
    EvaluationResult Evaluation,
    int EvaluationCount,
    TimeSpan Elapsed,
    IReadOnlyList<ConvergencePoint> Convergence);
