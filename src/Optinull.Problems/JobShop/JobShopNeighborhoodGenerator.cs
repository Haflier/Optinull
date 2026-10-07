namespace Optinull.Problems.JobShop;

public sealed class JobShopNeighborhoodGenerator
{
    public IEnumerable<JobSequence> Generate(
        JobSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        var jobIds = sequence.JobIds;

        // Every pair of different positions represents
        // one possible swap move.
        //
        // Example:
        // [0, 1, 0, 1]
        //
        // (0,1) -> [1,0,0,1]
        // (0,2) -> [0,1,0,1]  same values -> duplicate
        // (0,3) -> [1,1,0,0]
        // ...
        //
        // We skip swaps where both positions contain
        // the same job because they produce no new neighbor.
        for (var firstIndex = 0;
             firstIndex < jobIds.Count - 1;
             firstIndex++)
        {
            for (var secondIndex = firstIndex + 1;
                 secondIndex < jobIds.Count;
                 secondIndex++)
            {
                if (jobIds[firstIndex] ==
                    jobIds[secondIndex])
                {
                    continue;
                }

                var neighbor =
                    jobIds.ToArray();

                (neighbor[firstIndex], neighbor[secondIndex]) =
                    (neighbor[secondIndex], neighbor[firstIndex]);

                yield return new JobSequence(neighbor);
            }
        }
    }
}
