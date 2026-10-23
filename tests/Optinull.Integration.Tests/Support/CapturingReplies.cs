using System.Collections.Concurrent;
using Optinull.Application.Commands;

namespace Optinull.Integration.Tests.Support;

internal sealed class CapturingReplies : IBotReplies
{
    private readonly ConcurrentQueue<string> _texts = new();
    private readonly ConcurrentQueue<(string FileName, string Caption, int Length)> _pngs = new();
    private readonly ConcurrentQueue<(string Text, IReadOnlyList<Choice> Choices)> _choices = new();

    public IReadOnlyList<string> Texts => _texts.ToArray();

    public IReadOnlyList<(string FileName, string Caption, int Length)> Pngs => _pngs.ToArray();

    public IReadOnlyList<(string Text, IReadOnlyList<Choice> Choices)> Choices => _choices.ToArray();

    public Task SendTextAsync(string text, CancellationToken cancellationToken)
    {
        _texts.Enqueue(text);
        return Task.CompletedTask;
    }

    public Task SendPngAsync(byte[] png, string fileName, string caption, CancellationToken cancellationToken)
    {
        _pngs.Enqueue((fileName, caption, png.Length));
        return Task.CompletedTask;
    }

    public Task ShowChoicesAsync(
        string text,
        IReadOnlyList<IReadOnlyList<Choice>> rows,
        CancellationToken cancellationToken)
    {
        _choices.Enqueue((text, rows.SelectMany(row => row).ToList()));
        return Task.CompletedTask;
    }
}
