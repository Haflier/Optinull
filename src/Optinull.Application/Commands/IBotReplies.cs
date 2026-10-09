namespace Optinull.Application.Commands;

/// <summary>Where the replies to one chat go. Telegram implements this.</summary>
public interface IBotReplies
{
    Task SendTextAsync(string text, CancellationToken cancellationToken);

    Task SendPngAsync(
        byte[] png,
        string fileName,
        string caption,
        CancellationToken cancellationToken);
}
