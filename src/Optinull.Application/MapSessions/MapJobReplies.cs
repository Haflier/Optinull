using Optinull.Application.Commands;

namespace Optinull.Application.MapSessions;

/// <summary>
/// The replies of a job started from the map. Everything the job says still goes to the chat,
/// and the latest progress is kept so the page can show it too. The chat is a courtesy here:
/// if Telegram cannot be reached the job carries on, because the result is read on the map.
/// </summary>
internal sealed class MapJobReplies : IBotReplies
{
    private readonly IBotReplies _chat;
    private long _progressBits;

    public MapJobReplies(IBotReplies chat)
    {
        ArgumentNullException.ThrowIfNull(chat);

        _chat = chat;
    }

    public double Progress => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _progressBits));

    public Task SendTextAsync(string text, CancellationToken cancellationToken) =>
        BestEffortAsync(() => _chat.SendTextAsync(text, cancellationToken));

    public Task SendTextWithCodeAsync(
        string before,
        string code,
        string after,
        CancellationToken cancellationToken) =>
        BestEffortAsync(() => _chat.SendTextWithCodeAsync(before, code, after, cancellationToken));

    public Task SendPngAsync(
        byte[] png,
        string fileName,
        string caption,
        CancellationToken cancellationToken) =>
        BestEffortAsync(() => _chat.SendPngAsync(png, fileName, caption, cancellationToken));

    public Task ShowChoicesAsync(
        string text,
        IReadOnlyList<IReadOnlyList<Choice>> rows,
        CancellationToken cancellationToken) =>
        BestEffortAsync(() => _chat.ShowChoicesAsync(text, rows, cancellationToken));

    public Task ReportProgressAsync(
        double fraction,
        string label,
        CancellationToken cancellationToken)
    {
        if (double.IsFinite(fraction))
        {
            Interlocked.Exchange(
                ref _progressBits,
                BitConverter.DoubleToInt64Bits(Math.Clamp(fraction, 0, 1)));
        }

        return BestEffortAsync(() => _chat.ReportProgressAsync(fraction, label, cancellationToken));
    }

    // Cancellation must still reach the job, so only other failures are dropped.
    private static async Task BestEffortAsync(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}
