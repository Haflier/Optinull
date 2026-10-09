namespace Optinull.Reporting.Charts;

/// <summary>
/// Best-so-far objective of one solver. Points are recorded only when the
/// best value improves, so the chart draws them as steps.
/// </summary>
public sealed record ConvergenceSeries(
    string Name,
    IReadOnlyList<ConvergencePointData> Points,
    int FinalEvaluations);

public readonly record struct ConvergencePointData(
    int Evaluations,
    double Objective);
