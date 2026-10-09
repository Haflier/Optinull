using System.Text;
using Microsoft.Extensions.Logging;
using Optinull.Application.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Optinull.Infrastructure.Telegram;

public sealed record TelegramBotOptions(string Token);

/// <summary>Long-polls Telegram and hands messages to the command processor.</summary>
public sealed class TelegramPoller
{
    private const int MaxDocumentBytes = 30_000;

    private readonly TelegramBotClient _client;
    private readonly BotCommandProcessor _processor;
    private readonly ILogger<TelegramPoller> _logger;

    public TelegramPoller(
        TelegramBotOptions options,
        BotCommandProcessor processor,
        ILogger<TelegramPoller> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Token);

        _client = new TelegramBotClient(options.Token);
        _processor = processor;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var me = await _client.GetMe(cancellationToken);
        _logger.LogInformation("Bot @{Username} is running.", me.Username);

        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _client.GetUpdates(
                    offset,
                    timeout: 30,
                    allowedUpdates: [UpdateType.Message],
                    cancellationToken: cancellationToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;

                    if (update.Message is { } message)
                        _ = HandleAsync(message, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Polling failed; retrying in 5 seconds.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var replies = new TelegramReplies(_client, chatId);

        try
        {
            var text = message.Text;

            if (text is null && message.Document is { } document)
                text = await ReadDocumentCommandAsync(message, document, replies, cancellationToken);

            if (text is null)
                return;

            await _processor.ProcessAsync(text, chatId, replies, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to handle a message.");

            try
            {
                await replies.SendTextAsync(
                    "Something went wrong while handling that request.",
                    CancellationToken.None);
            }
            catch (Exception replyException)
            {
                _logger.LogError(replyException, "Failed to send the error reply.");
            }
        }
    }

    // A .txt file whose caption is a command, e.g. "/solve custom ga": the file
    // content becomes the lines after the command.
    private async Task<string?> ReadDocumentCommandAsync(
        Message message,
        Document document,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var caption = message.Caption ?? string.Empty;

        if (!caption.TrimStart().StartsWith('/'))
            return null;

        if (document.FileSize is > MaxDocumentBytes)
        {
            await replies.SendTextAsync(
                $"That file is too large (limit {MaxDocumentBytes / 1000} KB).",
                cancellationToken);
            return null;
        }

        using var stream = new MemoryStream();

        await _client.GetInfoAndDownloadFile(document.FileId, stream, cancellationToken);

        return caption.TrimEnd() + "\n" + Encoding.UTF8.GetString(stream.ToArray());
    }
}
