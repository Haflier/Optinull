namespace Optinull.Application.Commands;

/// <summary>A button: what the user sees, and what comes back when it is pressed.</summary>
public sealed record Choice(string Label, string Data);

/// <summary>Where the replies to one chat go. Telegram implements this.</summary>
public interface IBotReplies
{
    Task SendTextAsync(string text, CancellationToken cancellationToken);

    Task SendPngAsync(
        byte[] png,
        string fileName,
        string caption,
        CancellationToken cancellationToken);

    /// <summary>
    /// Shows buttons, one row per inner list. When the reply answers a button press
    /// the same message is updated. With no rows the buttons are removed. Channels
    /// without buttons fall back to a plain list.
    /// </summary>
    Task ShowChoicesAsync(
        string text,
        IReadOnlyList<IReadOnlyList<Choice>> rows,
        CancellationToken cancellationToken)
    {
        var options = string.Join("\n", rows.SelectMany(row => row).Select(choice => choice.Label));

        return SendTextAsync(
            options.Length == 0 ? text : $"{text}\n\n{options}",
            cancellationToken);
    }

    /// <summary>Shows or updates a progress bar for a running job. fraction is 0..1.</summary>
    Task ReportProgressAsync(
        double fraction,
        string label,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
