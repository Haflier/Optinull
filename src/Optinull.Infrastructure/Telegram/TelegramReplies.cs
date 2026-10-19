using Optinull.Application.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Optinull.Infrastructure.Telegram;

/// <summary>Sends replies to one Telegram chat.</summary>
internal sealed class TelegramReplies : IBotReplies
{
    private const int BarWidth = 14;
    private static readonly TimeSpan MinProgressInterval = TimeSpan.FromSeconds(2);

    private readonly ITelegramBotClient _client;
    private readonly long _chatId;
    private readonly int? _editMessageId;

    private readonly SemaphoreSlim _progressGate = new(1, 1);
    private int _progressMessageId;
    private int _lastPercent = -1;
    private DateTime _lastProgressAt = DateTime.MinValue;

    /// <param name="editMessageId">
    /// The message a button press came from; buttons shown in answer replace it.
    /// </param>
    public TelegramReplies(ITelegramBotClient client, long chatId, int? editMessageId = null)
    {
        _client = client;
        _chatId = chatId;
        _editMessageId = editMessageId;
    }

    public async Task SendTextAsync(string text, CancellationToken cancellationToken)
    {
        await _client.SendMessage(
            _chatId,
            text,
            cancellationToken: cancellationToken);
    }

    public async Task SendTextWithCodeAsync(
        string before,
        string code,
        string after,
        CancellationToken cancellationToken)
    {
        // Entity offsets and lengths count UTF-16 code units, which is what string indexes are.
        await _client.SendMessage(
            _chatId,
            before + code + after,
            entities:
            [
                new MessageEntity
                {
                    Type = MessageEntityType.Code,
                    Offset = before.Length,
                    Length = code.Length
                }
            ],
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

    public async Task ShowChoicesAsync(
        string text,
        IReadOnlyList<IReadOnlyList<Choice>> rows,
        CancellationToken cancellationToken)
    {
        // No rows means no buttons, which also removes the old ones when editing.
        InlineKeyboardMarkup? markup = rows.Count == 0
            ? null
            : new InlineKeyboardMarkup(
                rows.Select(row => row.Select(choice =>
                    InlineKeyboardButton.WithCallbackData(choice.Label, choice.Data))));

        if (_editMessageId is { } messageId)
        {
            try
            {
                await _client.EditMessageText(
                    _chatId,
                    messageId,
                    text,
                    replyMarkup: markup,
                    cancellationToken: cancellationToken);

                return;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The message may be too old to edit; show the buttons in a new one.
            }
        }

        await _client.SendMessage(
            _chatId,
            text,
            replyMarkup: markup,
            cancellationToken: cancellationToken);
    }

    public async Task ReportProgressAsync(
        double fraction,
        string label,
        CancellationToken cancellationToken)
    {
        var percent = (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100);

        // Updates that arrive while another is being sent are dropped; the final
        // 100% waits for its turn.
        var wait = percent >= 100 ? TimeSpan.FromSeconds(3) : TimeSpan.Zero;

        if (!await _progressGate.WaitAsync(wait, cancellationToken))
            return;

        try
        {
            var now = DateTime.UtcNow;

            if (percent == _lastPercent)
                return;

            // Telegram throttles message edits, so show a new value every couple of seconds.
            if (_progressMessageId != 0 && percent < 100 && now - _lastProgressAt < MinProgressInterval)
                return;

            var text = $"{label}\n{Bar(percent)} {percent}%";

            if (_progressMessageId == 0)
            {
                var message = await _client.SendMessage(
                    _chatId,
                    text,
                    cancellationToken: cancellationToken);

                _progressMessageId = message.MessageId;
            }
            else
            {
                await _client.EditMessageText(
                    _chatId,
                    _progressMessageId,
                    text,
                    cancellationToken: cancellationToken);
            }

            _lastPercent = percent;
            _lastProgressAt = now;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A progress bar is never worth failing a job.
        }
        finally
        {
            _progressGate.Release();
        }
    }

    private static string Bar(int percent)
    {
        var filled = percent * BarWidth / 100;

        return new string('█', filled) + new string('░', BarWidth - filled);
    }
}
