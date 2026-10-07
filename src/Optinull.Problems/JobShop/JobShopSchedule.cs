namespace Optinull.Problems.JobShop;

public sealed class JobShopSchedule
{
    private readonly List<ScheduledOperation> _operations = [];

    public IReadOnlyList<ScheduledOperation> Operations =>
        _operations;

    public double Makespan { get; }

    public JobShopSchedule(
        IEnumerable<ScheduledOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        _operations.AddRange(operations);

        if (_operations.Count == 0)
        {
            throw new ArgumentException(
                "A schedule must contain at least one operation.",
                nameof(operations));
        }

        Makespan = _operations.Max(
            operation => operation.EndTime);
    }
}
