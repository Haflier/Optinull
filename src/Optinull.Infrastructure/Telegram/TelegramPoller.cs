using Microsoft.Extensions.Logging;
using Optinull.Application.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Optinull.Infrastructure.Telegram;

public sealed record TelegramBotOptions(string Token);

/// <summary>Long-polls Telegram and hands text messages to the command processor.</summary>
public sealed class TelegramPoller
{
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

                    if (update.Message is { Text: { } text } message)
                        _ = HandleAsync(message.Chat.Id, text, cancellationToken);
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

    private async Task HandleAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        var replies = new TelegramReplies(_client, chatId);

        try
        {
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
}
