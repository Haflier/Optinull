namespace Optinull.Problems.JobShop;

public sealed class JobSequence
{
    private readonly List<int> _jobIds = [];

    public IReadOnlyList<int> JobIds =>
        _jobIds;

    public int Count =>
        _jobIds.Count;

    public JobSequence(
        IEnumerable<int> jobIds)
    {
        ArgumentNullException.ThrowIfNull(jobIds);

        _jobIds.AddRange(jobIds);

        if (_jobIds.Count == 0)
        {
            throw new ArgumentException(
                "A job sequence cannot be empty.",
                nameof(jobIds));
        }

        if (_jobIds.Any(jobId => jobId < 0))
        {
            throw new ArgumentException(
                "A job sequence cannot contain negative job IDs.",
                nameof(jobIds));
        }
    }
}
