namespace Optinull.Problems.JobShop;

public sealed class Job
{
    private readonly List<JobOperation> _operations = [];

    public int Id { get; }

    public IReadOnlyList<JobOperation> Operations =>
        _operations;

    public Job(
        int id,
        IEnumerable<JobOperation> operations)
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                "Job ID cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(operations);

        _operations.AddRange(operations);

        if (_operations.Count == 0)
        {
            throw new ArgumentException(
                "A job must contain at least one operation.",
                nameof(operations));
        }
    }
}
