using Optinull.Application.Commands;

namespace Optinull.Application.Jobs;

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>One solve request and its lifecycle. Safe to use from several threads.</summary>
public sealed class OptimizationJob
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();

    private JobStatus _status = JobStatus.Queued;
    private string? _error;
    private bool _cancelRequested;

    public OptimizationJob(
        long id,
        long chatId,
        SolveCommand command,
        IBotReplies replies)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(replies);

        Id = id;
        ChatId = chatId;
        Command = command;
        Replies = replies;
    }

    public long Id { get; }

    public long ChatId { get; }

    public SolveCommand Command { get; }

    public IBotReplies Replies { get; }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    public CancellationToken Token => _cancellation.Token;

    public JobStatus Status
    {
        get { lock (_gate) return _status; }
    }

    public string? Error
    {
        get { lock (_gate) return _error; }
    }

    public bool CancelRequested
    {
        get { lock (_gate) return _cancelRequested; }
    }

    public bool IsActive => Status is JobStatus.Queued or JobStatus.Running;

    /// <summary>Queued to Running. False if the job was cancelled while waiting.</summary>
    public bool TryStart()
    {
        lock (_gate)
        {
            if (_status != JobStatus.Queued)
                return false;

            _status = JobStatus.Running;
            return true;
        }
    }

    /// <summary>
    /// Returns the status the job had when the request arrived, or null if it
    /// had already finished. A queued job is cancelled immediately; a running
    /// one is signalled and finishes cancelling on its own.
    /// </summary>
    public JobStatus? RequestCancel()
    {
        JobStatus previous;

        lock (_gate)
        {
            if (_status is not (JobStatus.Queued or JobStatus.Running))
                return null;

            previous = _status;
            _cancelRequested = true;

            if (_status == JobStatus.Queued)
                _status = JobStatus.Cancelled;
        }

        if (previous == JobStatus.Running)
            _cancellation.Cancel();

        return previous;
    }

    public void Complete() => Finish(JobStatus.Completed, null);

    public void Fail(string error) => Finish(JobStatus.Failed, error);

    public void MarkCancelled() => Finish(JobStatus.Cancelled, null);

    private void Finish(JobStatus status, string? error)
    {
        lock (_gate)
        {
            if (_status != JobStatus.Running)
                return;

            _status = status;
            _error = error;
        }
    }
}
