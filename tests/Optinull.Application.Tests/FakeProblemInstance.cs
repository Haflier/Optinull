using Optinull.Application.Optimization;
using Optinull.Application.Problems;

namespace Optinull.Application.Tests;

internal sealed class FakeProblemInstance : IProblemInstance
{
    public string Name => "fake";

    public string Description => "fake problem";

    public SolveReport Solve(
        SolverKind solver,
        int seed,
        CancellationToken cancellationToken) =>
        new("fake solver", [new ReportImage("fake.png", "caption", [1, 2, 3])]);
}
