using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Problems;

namespace Optinull.Application.MapSessions;

/// <summary>One problem submitted through a session: the job that solves it, and what it solves.</summary>
internal sealed record MapRun(OptimizationJob Job, MapVrpInstance Instance, MapJobReplies Replies);

/// <summary>
/// One user's open map. The token is the only credential: it is long and random, and it is sent
/// only to the owner's private chat, so whoever presents it is the owner. Everything done through
/// a session is done as its owner: jobs run in the owner's chat and are cancelled from it.
/// </summary>
public sealed class MapSession
{
    private long _expiresAtTicks;

    internal MapSession(
        string token,
        long ownerId,
        long chatId,
        IBotReplies replies,
        MapSettings settings,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Token = token;
        OwnerId = ownerId;
        ChatId = chatId;
        Replies = replies;
        Settings = settings;
        CreatedAt = createdAt;
        _expiresAtTicks = expiresAt.UtcTicks;
    }

    /// <summary>The secret. Never log it or put it in a response.</summary>
    internal string Token { get; }

    /// <summary>The Telegram user who asked for the map.</summary>
    public long OwnerId { get; }

    public long ChatId { get; }

    public MapSettings Settings { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset ExpiresAt =>
        new(Interlocked.Read(ref _expiresAtTicks), TimeSpan.Zero);

    internal IBotReplies Replies { get; }

    /// <summary>Guards <see cref="Run"/> and <see cref="RunObserved"/>.</summary>
    internal object Gate { get; } = new();

    internal MapRun? Run { get; set; }

    /// <summary>True once the end of the current run has been seen and the expiry reset for it.</summary>
    internal bool RunObserved { get; set; }

    internal void SetExpiry(DateTimeOffset expiresAt) =>
        Interlocked.Exchange(ref _expiresAtTicks, expiresAt.UtcTicks);
}
