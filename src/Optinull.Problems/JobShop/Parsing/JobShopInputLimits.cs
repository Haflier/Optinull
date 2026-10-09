namespace Optinull.Problems.JobShop.Parsing;

/// <summary>Size limits for user-supplied instances.</summary>
public sealed record JobShopInputLimits(
    int MaxJobs = 20,
    int MaxMachines = 20,
    int MaxOperations = 400,
    int MaxProcessingTime = 100_000,
    int MaxCharacters = 20_000)
{
    public static JobShopInputLimits Default { get; } = new();
}
