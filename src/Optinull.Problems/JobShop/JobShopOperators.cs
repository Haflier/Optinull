namespace Optinull.Problems.JobShop;

public enum JobShopCrossover
{
    /// <summary>Prefix of parent A, remainder in parent B's order.</summary>
    OnePoint,

    /// <summary>
    /// Precedence-preserving order crossover (POX): a random subset of jobs
    /// keeps its positions from parent A; the other slots are filled in
    /// parent B's order.
    /// </summary>
    PrecedencePreserving
}

public enum JobShopMutation
{
    /// <summary>Swap two entries that belong to different jobs.</summary>
    Swap,

    /// <summary>Remove one entry and insert it at another position.</summary>
    Insertion
}
