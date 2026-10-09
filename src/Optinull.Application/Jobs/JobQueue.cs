using System.Threading.Channels;
using Optinull.Application.Commands;

namespace Optinull.Application.Jobs;

public abstract record EnqueueResult;

public sealed record Enqueued(OptimizationJob Job) : EnqueueResult;

public sealed record AlreadyActive(OptimizationJob Existing) : EnqueueResult;

public sealed record QueueFull : EnqueueResult;

public sealed record CancelResult(OptimizationJob Job, bool WasRunning);

/// <summary>In-memory job queue: one active job per chat, bounded length.</summary>
public sealed class JobQueue
{
    private readonly object _gate = new();
    private readonly Channel<OptimizationJob> _channel;
    private readonly Dictionary<long, OptimizationJob> _activeByChat = [];

    private long _lastId;

    public JobQueue(int capacity = 10)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Queue capacity must be greater than zero.");

        _channel = Channel.CreateBounded<OptimizationJob>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

    public ChannelReader<OptimizationJob> Reader => _channel.Reader;

    public EnqueueResult TryEnqueue(
        long chatId,
        SolveCommand command,
        IBotReplies replies)
    {
        lock (_gate)
        {
            if (_activeByChat.TryGetValue(chatId, out var existing) &&
                existing.IsActive)
            {
                return new AlreadyActive(existing);
            }

            var job = new OptimizationJob(_lastId + 1, chatId, command, replies);

            if (!_channel.Writer.TryWrite(job))
                return new QueueFull();

            _lastId++;
            _activeByChat[chatId] = job;

            return new Enqueued(job);
        }
    }

    /// <summary>Cancels the chat's active job. Null if it has none.</summary>
    public CancelResult? Cancel(long chatId)
    {
        OptimizationJob? job;

        lock (_gate)
            _activeByChat.TryGetValue(chatId, out job);

        if (job is null)
            return null;

        var previous = job.RequestCancel();

        if (previous is null)
            return null;

        // A cancelled queued job is skipped by the worker, so free the chat now.
        if (previous == JobStatus.Queued)
            Release(job);

        return new CancelResult(job, previous == JobStatus.Running);
    }

    /// <summary>Called by the worker when a job is done. Safe to call twice.</summary>
    public void Release(OptimizationJob job)
    {
        lock (_gate)
        {
            if (_activeByChat.TryGetValue(job.ChatId, out var current) &&
                ReferenceEquals(current, job))
            {
                _activeByChat.Remove(job.ChatId);
            }
        }
    }
}
