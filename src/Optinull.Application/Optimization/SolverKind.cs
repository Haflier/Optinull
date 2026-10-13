namespace Optinull.Application.Optimization;

public enum SolverKind
{
    Greedy,
    HillClimbing,
    IteratedLocalSearch,
    SimulatedAnnealing,
    GeneticAlgorithm
}

public static class SolverNames
{
    public static string Display(SolverKind kind) =>
        kind switch
        {
            SolverKind.Greedy => "Greedy",
            SolverKind.HillClimbing => "Hill Climbing",
            SolverKind.IteratedLocalSearch => "Iterated Local Search",
            SolverKind.SimulatedAnnealing => "Simulated Annealing",
            SolverKind.GeneticAlgorithm => "Genetic Algorithm",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
