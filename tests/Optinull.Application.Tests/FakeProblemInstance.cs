using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Tests;

internal sealed class FakeProblemInstance : IProblemInstance
{
    public string Name => "fake";

    public string Description => "fake problem";

    public string ScoreLabel => "Score";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken) =>
        new(
            solver,
            "fake solver",
            1,
            TimeSpan.Zero,
            1,
            new ConvergenceSeries("fake solver", [new ConvergencePointData(1, 1)], 1),
            () => [new ReportImage("fake.png", "caption", [1, 2, 3])]);
}
