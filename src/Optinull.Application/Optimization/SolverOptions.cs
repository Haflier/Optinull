
namespace Optinull.Application.Optimization;

/// <summary>
/// Nullable values mean "use the existing problem-specific default".
/// </summary>
public sealed record SolverOptions
{
    // Simulated annealing
    public double? InitialTemperature { get; init; }
    public double? CoolingRate { get; init; }
    public int? IterationsPerTemperature { get; init; }
    public double? MinimumTemperature { get; init; }

    // Genetic algorithm
    public int? PopulationSize { get; init; }
    public int? Generations { get; init; }
    public int? EliteCount { get; init; }
    public double? MutationRate { get; init; }
    public int? TournamentSize { get; init; }
    public int? LocalSearchTries { get; init; }
    public int? MaxEvaluations { get; init; }

    public bool IsEmpty =>
        InitialTemperature is null &&
        CoolingRate is null &&
        IterationsPerTemperature is null &&
        MinimumTemperature is null &&
        PopulationSize is null &&
        Generations is null &&
        EliteCount is null &&
        MutationRate is null &&
        TournamentSize is null &&
        LocalSearchTries is null &&
        MaxEvaluations is null;
}
