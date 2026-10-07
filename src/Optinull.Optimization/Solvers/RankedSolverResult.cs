namespace Optinull.Optimization.Solvers;

public sealed class RankedSolverResult
{
    public int Rank { get; }
    public SolverResult Result { get; }

    public RankedSolverResult(
        int rank,
        SolverResult result)
    {
        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                "Rank must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(result);

        Rank = rank;
        Result = result;
    }
}
