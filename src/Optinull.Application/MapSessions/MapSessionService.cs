using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Problems.Vrp;

namespace Optinull.Application.MapSessions;

/// <summary>
/// Creates map sessions for Telegram users, authorizes the web requests that use them, and
/// runs the problems they submit on the same job queue the chat commands use. A user has at
/// most one session: opening a new one replaces the old. Sessions live in memory only.
/// </summary>
public sealed class MapSessionService : IMapSessionLauncher
{
    /// <summary>The job's target name, which shows up in the logs next to the solver.</summary>
    public const string JobTarget = "vrp-map";

    private readonly object _gate = new();
    private readonly Dictionary<string, MapSession> _byToken = new(StringComparer.Ordinal);
    private readonly Dictionary<long, string> _tokenByOwner = [];

    private readonly JobQueue _queue;
    private readonly MapSessionOptions _options;
    private readonly TimeProvider _time;
    private readonly string _linkPrefix;

    public MapSessionService(
        JobQueue queue,
        MapSessionOptions? options = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
        _options = options ?? new MapSessionOptions();
        _time = time ?? TimeProvider.System;
        _linkPrefix = _options.LinkPrefix();
    }

    // ---- Telegram side -------------------------------------------------------------------------

    public MapLink Open(long ownerId, long chatId, IBotReplies replies, MapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(replies);
        ArgumentNullException.ThrowIfNull(settings);

        var now = _time.GetUtcNow();
        var token = NewToken();

        var session = new MapSession(
            token,
            ownerId,
            chatId,
            replies,
            settings,
            now,
            now + _options.UnusedLifetime);

        lock (_gate)
        {
            RemoveExpired(now);

            if (_tokenByOwner.TryGetValue(ownerId, out var previous))
                _byToken.Remove(previous);

            _byToken[token] = session;
            _tokenByOwner[ownerId] = token;
        }

        return new MapLink(_linkPrefix + token, _options.UnusedLifetime);
    }

    public bool Close(long ownerId)
    {
        MapSession? session = null;

        lock (_gate)
        {
            if (_tokenByOwner.Remove(ownerId, out var token))
                _byToken.Remove(token, out session);
        }

        return session is not null && session.ExpiresAt > _time.GetUtcNow();
    }

    // ---- Web side ------------------------------------------------------------------------------

    /// <summary>
    /// Finds the session a request's token belongs to. A token that was never issued, was
    /// replaced by a newer link, was closed, or has expired gets nothing, and the caller cannot
    /// tell which. An expired session is forgotten on the spot.
    /// </summary>
    public bool TryAuthorize(string? token, [NotNullWhen(true)] out MapSession? session)
    {
        session = null;

        // Tokens are 43 characters; anything far longer is not worth a lookup.
        if (string.IsNullOrEmpty(token) || token.Length > 128)
            return false;

        MapSession? found;

        lock (_gate)
            _byToken.TryGetValue(token, out found);

        if (found is null)
            return false;

        if (found.ExpiresAt <= _time.GetUtcNow())
        {
            Remove(found);
            return false;
        }

        session = found;

        return true;
    }

    public MapSessionInfo Describe(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var settings = session.Settings;

        return new MapSessionInfo(
            SolverNames.Display(settings.Solver),
            settings.Iterations,
            settings.Seed,
            session.ExpiresAt);
    }

    /// <summary>True while a job submitted through this session is waiting or running.</summary>
    public bool IsBusy(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (session.Gate)
            return session.Run?.Job.IsActive == true;
    }

    /// <summary>
    /// Queues the problem with the algorithm chosen in Telegram. The job belongs to the owner's
    /// chat, so there is still one active job per user and /cancel in the chat stops it.
    /// </summary>
    public MapSubmitResult Submit(MapSession session, VrpProblem problem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(problem);

        var now = _time.GetUtcNow();

        lock (session.Gate)
        {
            if (session.Run is { Job.IsActive: true } running)
                return new MapSubmitResult(MapSubmitOutcome.Busy, running.Job.Id);

            var settings = session.Settings;
            var instance = new MapVrpInstance(problem);
            var replies = new MapJobReplies(session.Replies);

            var command = new SolveCommand(
                JobTarget,
                settings.Solver,
                Iterations: settings.Iterations,
                Seed: settings.Seed,
                Tuning: settings.Tuning);

            switch (_queue.TryEnqueue(session.ChatId, command, replies, instance))
            {
                case Enqueued enqueued:
                    session.Run = new MapRun(enqueued.Job, instance, replies);
                    session.RunObserved = false;

                    // The job may take a while; the link has to outlive it.
                    var keepUntil = now + _options.ResultLifetime;

                    if (keepUntil > session.ExpiresAt)
                        session.SetExpiry(keepUntil);

                    return new MapSubmitResult(MapSubmitOutcome.Started, enqueued.Job.Id);

                case AlreadyActive active:
                    return new MapSubmitResult(MapSubmitOutcome.Busy, active.Existing.Id);

                default:
                    return new MapSubmitResult(MapSubmitOutcome.QueueFull);
            }
        }
    }

    public MapSessionStatus GetStatus(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var now = _time.GetUtcNow();

        lock (session.Gate)
        {
            if (session.Run is not { } run)
                return new MapSessionStatus(MapPhase.Idle, 0, null, null, session.ExpiresAt);

            var phase = run.Job.Status switch
            {
                JobStatus.Queued => MapPhase.Queued,
                JobStatus.Running => MapPhase.Running,
                JobStatus.Completed => MapPhase.Completed,
                JobStatus.Failed => MapPhase.Failed,
                _ => MapPhase.Cancelled
            };

            var finished = phase is MapPhase.Completed or MapPhase.Failed or MapPhase.Cancelled;

            // The page polls while a job runs, so the end is seen almost at once. A finished run
            // keeps its result for the result lifetime; after a failure or a cancellation the
            // user is back to editing, so the link only needs to wait for the next attempt.
            if (finished && !session.RunObserved)
            {
                session.RunObserved = true;
                session.SetExpiry(
                    now + (phase == MapPhase.Completed ? _options.ResultLifetime : _options.UnusedLifetime));
            }

            MapSolution? solution = null;

            if (phase == MapPhase.Completed && run.Instance.Result is { } result)
                solution = MapSolution.From(run.Instance.Problem, result);

            return new MapSessionStatus(
                phase,
                phase == MapPhase.Completed ? 1 : run.Replies.Progress,
                run.Job.Id,
                solution,
                session.ExpiresAt);
        }
    }

    /// <summary>
    /// Where each truck of the finished run stops, taken from the problem the server solved, so
    /// the road geometry always belongs to the stored solution. Null until a run has completed.
    /// </summary>
    public MapRoutePlan? GetRoutePlan(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        MapRun? run;

        lock (session.Gate)
            run = session.Run;

        if (run is null || run.Job.Status != JobStatus.Completed || run.Instance.Result is not { } result)
            return null;

        var problem = run.Instance.Problem;

        // Longitude is x and latitude is y in a map problem.
        var depot = new MapStop(problem.DepotY, problem.DepotX);

        var routes = result.Plan.Routes
            .Select(route => (IReadOnlyList<MapStop>)route
                .Select(customer => new MapStop(problem.Customers[customer].Y, problem.Customers[customer].X))
                .Prepend(depot)
                .Append(depot)
                .ToList())
            .ToList();

        return new MapRoutePlan(run.Job.Id, routes);
    }

    /// <summary>Cancels this session's job. False if there is none in progress.</summary>
    public bool CancelJob(MapSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        MapRun? run;

        lock (session.Gate)
            run = session.Run;

        if (run is null)
            return false;

        var previous = run.Job.RequestCancel();

        if (previous is null)
            return false;

        // A cancelled queued job is skipped by the worker, so free the chat now.
        if (previous == JobStatus.Queued)
            _queue.Release(run.Job);

        return true;
    }

    // ---- Housekeeping --------------------------------------------------------------------------

    // 32 random bytes: far too many to guess, and URL-safe once base64 is made url-safe.
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private void Remove(MapSession session)
    {
        lock (_gate)
            RemoveLocked(session);
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var session in _byToken.Values.Where(candidate => candidate.ExpiresAt <= now).ToList())
            RemoveLocked(session);
    }

    private void RemoveLocked(MapSession session)
    {
        if (!_byToken.TryGetValue(session.Token, out var current) || !ReferenceEquals(current, session))
            return;

        _byToken.Remove(session.Token);

        if (_tokenByOwner.TryGetValue(session.OwnerId, out var token) && token == session.Token)
            _tokenByOwner.Remove(session.OwnerId);
    }
}
