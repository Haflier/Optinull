using Optinull.Application.Optimization;
using Optinull.Problems.Vrp;
using Xunit.Abstractions;

namespace Optinull.Application.Tests.Diagnostics;

/// <summary>
/// Runs the solvers on the two built-in routing instances over several seeds and prints how
/// they did against the nearest-neighbour plan, so the real numbers are visible
/// (dotnet test --filter VrpDiagnostics --logger "console;verbosity=detailed"). Only things that
/// must hold are asserted: every plan respects the limits, and annealing beats the heuristic it
/// starts from. How the other solvers rank is printed, not asserted.
/// </summary>
public sealed class VrpDiagnosticsTests
{
    private readonly ITestOutputHelper _output;

    public VrpDiagnosticsTests(ITestOutputHelper output) => _output = output;

    private static int SeedCount(SolverKind kind) =>
        // The climbing solvers walk the whole neighbourhood and take a few seconds each.
        kind is SolverKind.HillClimbing or SolverKind.IteratedLocalSearch ? 4 : 10;

    [Theory]
    [InlineData(15, 3)]
    [InlineData(40, 6)]
    public void Solvers_AgainstTheNearestNeighbourPlan(int customers, int vehicles)
    {
        var problem = VrpInstances.Random(customers, vehicles, seed: 21);
        var service = new VrpSolveService();

        _output.WriteLine(
            $"VRP, {customers} customers, {vehicles} vehicles, capacity {problem.Capacity}");

        foreach (var kind in new[]
                 {
                     SolverKind.HillClimbing,
                     SolverKind.IteratedLocalSearch,
                     SolverKind.SimulatedAnnealing,
                     SolverKind.GeneticAlgorithm
                 })
        {
            var distances = new List<double>();
            var greedy = 0.0;

            foreach (var seed in Enumerable.Range(1, SeedCount(kind)))
            {
                var result = service.Solve(problem, kind, seed);
                var check = VrpEvaluator.Evaluate(problem, result.Plan);

                Assert.True(check.IsFeasible, $"{kind}, seed {seed}: the plan breaks a limit.");
                Assert.Equal(check.ObjectiveValue, result.Distance, 6);

                distances.Add(result.Distance);
                greedy = result.GreedyDistance;
            }

            _output.WriteLine(
                $"  {SolverNames.Display(kind),-22} mean {distances.Average(),8:0.0}  best {distances.Min(),8:0.0}  " +
                $"worst {distances.Max(),8:0.0}  (nearest neighbour {greedy:0.0}, " +
                $"mean {(greedy - distances.Average()) / greedy:P1} shorter, {distances.Count} seeds)");

            if (kind == SolverKind.SimulatedAnnealing)
            {
                Assert.True(
                    distances.Average() < greedy,
                    $"Annealing averaged {distances.Average():0.0}, not under the nearest-neighbour plan {greedy:0.0}.");
            }
        }
    }
}
