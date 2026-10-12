using System.Text;
using Microsoft.Extensions.Logging;
using Optinull.Application.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotCommand = global::Telegram.Bot.Types.BotCommand;

namespace Optinull.Infrastructure.Telegram;

public sealed record TelegramBotOptions(string Token);

/// <summary>Long-polls Telegram and hands messages to the command processor.</summary>
public sealed class TelegramPoller
{
    private const int MaxDocumentBytes = 30_000;

    // The last task of each chat: a chat's messages are handled one after another,
    // because answers in a guided dialog must not overtake each other.
    private readonly Dictionary<long, Task> _chatTails = [];

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

        await PublishMenuAsync(cancellationToken);

        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _client.GetUpdates(
                    offset,
                    timeout: 30,
                    allowedUpdates: [UpdateType.Message, UpdateType.CallbackQuery],
                    cancellationToken: cancellationToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;

                    if (update.Message is { } message)
                        InOrder(message.Chat.Id, () => HandleAsync(message, cancellationToken));
                    else if (update.CallbackQuery is { } callback)
                    {
                        InOrder(
                            callback.Message?.Chat.Id ?? callback.From.Id,
                            () => HandleCallbackAsync(callback, cancellationToken));
                    }
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

    // Registers the commands behind the "/" button next to the message box. Without
    // this list Telegram shows no button, so users would have to remember the commands.
    private async Task PublishMenuAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.SetMyCommands(
                BotMenu.Commands.Select(command => new TelegramBotCommand
                {
                    Command = command.Name,
                    Description = command.Description
                }),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Registered {Count} commands in the bot menu.",
                BotMenu.Commands.Count);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The bot works without the menu, so this must not stop it from starting.
            _logger.LogWarning(exception, "Could not register the command menu.");
        }
    }

    // Only the polling loop calls this, so the dictionary needs no lock. The work
    // catches its own errors, so a failure never blocks the chat's next message.
    private void InOrder(long chatId, Func<Task> work)
    {
        _chatTails[chatId] = _chatTails.TryGetValue(chatId, out var tail)
            ? tail.ContinueWith(_ => work(), TaskScheduler.Default).Unwrap()
            : work();
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

    // A button of the /optimize wizard was pressed.
    private async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken cancellationToken)
    {
        // Stops the spinner on the button; failing to do so is harmless.
        try
        {
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(exception, "Could not answer a callback query.");
        }

        var chatId = callback.Message?.Chat.Id ?? callback.From.Id;
        var replies = new TelegramReplies(_client, chatId, callback.Message?.MessageId);

        try
        {
            await _processor.ProcessCallbackAsync(callback.Data, chatId, replies, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to handle a button press.");

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
    // content becomes the lines after the command. Without a caption the file is
    // an answer to the question the bot just asked.
    private async Task<string?> ReadDocumentCommandAsync(
        Message message,
        Document document,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var caption = message.Caption ?? string.Empty;
        var isCommand = caption.TrimStart().StartsWith('/');

        if (!isCommand && !string.IsNullOrWhiteSpace(caption))
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

        var content = Encoding.UTF8.GetString(stream.ToArray());

        return isCommand ? caption.TrimEnd() + "\n" + content : content;
    }
}
