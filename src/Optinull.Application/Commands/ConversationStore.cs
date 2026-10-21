using System.Collections.Concurrent;
using Optinull.Application.Maps;
using Optinull.Application.Problems;

namespace Optinull.Application.Commands;

/// <summary>What a chat is in the middle of, besides running jobs.</summary>
internal abstract record Conversation(IProblemModule Module);

/// <summary>The user is answering the guided questions for their own data.</summary>
internal sealed record Collecting(IProblemModule Module, IProblemDialog Dialog) : Conversation(Module);

/// <summary>The user is sending places for a map problem: the depot, customers, then the fleet.</summary>
internal sealed record MapCollecting(IProblemModule Module, MapVrpDialog Dialog, MapSettings Settings) : Conversation(Module);

/// <summary>The data is complete; the user still has to pick an algorithm.</summary>
internal sealed record Ready(IProblemModule Module, IProblemInstance Instance) : Conversation(Module);

/// <summary>In-memory conversation state, one per chat, forgotten after a while.</summary>
internal sealed class ConversationStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<long, (Conversation State, DateTimeOffset StartedAt)> _byChat = new();

    public ConversationStore(TimeProvider time)
    {
        _time = time;
    }

    public void Set(long chatId, Conversation state)
    {
        var now = _time.GetUtcNow();

        // Keep the store from growing with chats that walked away.
        foreach (var (id, entry) in _byChat)
        {
            if (now - entry.StartedAt > Lifetime)
                _byChat.TryRemove(id, out _);
        }

        _byChat[chatId] = (state, now);
    }

    public Conversation? Get(long chatId)
    {
        if (!_byChat.TryGetValue(chatId, out var entry))
            return null;

        if (_time.GetUtcNow() - entry.StartedAt > Lifetime)
        {
            _byChat.TryRemove(chatId, out _);
            return null;
        }

        return entry.State;
    }

    /// <summary>Forgets the chat's conversation. True if there was one.</summary>
    public bool Clear(long chatId) => _byChat.TryRemove(chatId, out _);
}
