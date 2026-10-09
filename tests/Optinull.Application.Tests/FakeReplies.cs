using System.Collections.Concurrent;
using Optinull.Application.Commands;

namespace Optinull.Application.Tests;

public sealed class FakeReplies : IBotReplies
{
    private readonly ConcurrentQueue<string> _texts = new();
    private readonly ConcurrentQueue<(string FileName, string Caption, int Length)> _pngs = new();

    public IReadOnlyList<string> Texts => _texts.ToArray();

    public IReadOnlyList<(string FileName, string Caption, int Length)> Pngs => _pngs.ToArray();

    public Task SendTextAsync(string text, CancellationToken cancellationToken)
    {
        _texts.Enqueue(text);
        return Task.CompletedTask;
    }

    public Task SendPngAsync(
        byte[] png,
        string fileName,
        string caption,
        CancellationToken cancellationToken)
    {
        _pngs.Enqueue((fileName, caption, png.Length));
        return Task.CompletedTask;
    }
}
