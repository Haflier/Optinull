using Optinull.Application.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Optinull.Infrastructure.Telegram;

/// <summary>Sends replies to one Telegram chat.</summary>
internal sealed class TelegramReplies : IBotReplies
{
    private readonly ITelegramBotClient _client;
    private readonly long _chatId;

    public TelegramReplies(ITelegramBotClient client, long chatId)
    {
        _client = client;
        _chatId = chatId;
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken)
    {
        await _client.SendMessage(
            _chatId,
            text,
            cancellationToken: cancellationToken);
    }

    public async Task SendPngAsync(
        byte[] png,
        string fileName,
        string caption,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(png);

        await _client.SendPhoto(
            _chatId,
            InputFile.FromStream(stream, fileName),
            caption: caption,
            cancellationToken: cancellationToken);
    }
}
