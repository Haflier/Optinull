using Optinull.Application.Commands;
using Optinull.Application.Optimization;

namespace Optinull.Application.MapSessions;

/// <summary>The algorithm choices that came with the command: what to run once the map problem is submitted.</summary>
public sealed record MapSettings(
    SolverKind Solver,
    int? Iterations,
    int Seed,
    SolverTuning? Tuning);

/// <summary>Where to open the map, and how long the link waits for a problem.</summary>
public sealed record MapLink(string Url, TimeSpan ValidFor);

/// <summary>What the chat commands need from the map: open a session for a user, or close it.</summary>
public interface IMapSessionLauncher
{
    /// <summary>
    /// Creates a session for the user and replaces any session they had before. The replies
    /// are where the job's messages and charts go when a problem is solved through the map.
    /// </summary>
    MapLink Open(long ownerId, long chatId, IBotReplies replies, MapSettings settings);

    /// <summary>Closes the user's session. False if they had none that was still open.</summary>
    bool Close(long ownerId);
}
