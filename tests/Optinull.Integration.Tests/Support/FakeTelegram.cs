using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Optinull.Integration.Tests.Support;

/// <summary>One call the bot made to the Telegram Bot API.</summary>
internal sealed record TelegramCall(string Method, string Body, long ChatId, string Text);

/// <summary>
/// A stand-in for api.telegram.org behind an HttpClient. Tests push updates (what users send) and
/// read the calls the bot made (what it answered). getUpdates waits until there is something to
/// deliver, like the real long poll.
/// </summary>
internal sealed class FakeTelegram : HttpMessageHandler
{
    private readonly Channel<string> _updates = Channel.CreateUnbounded<string>();
    private readonly ConcurrentQueue<TelegramCall> _calls = new();

    private int _updateId;
    private int _messageId = 1000;

    public IReadOnlyList<TelegramCall> Calls => _calls.ToArray();

    public IReadOnlyList<TelegramCall> Of(string method) =>
        Calls.Where(call => call.Method == method.ToLowerInvariant()).ToList();

    /// <summary>Every text the bot sent or edited into a message.</summary>
    public IReadOnlyList<string> Texts =>
        Calls.Where(call => call.Method is "sendmessage" or "editmessagetext").Select(call => call.Text).ToList();

    // ---- What users do ------------------------------------------------------------------------

    public void UserSendsText(long chat, string text) =>
        Push(Message(chat, "\"text\":" + JsonSerializer.Serialize(text)));

    public void UserSendsLocation(long chat, double latitude, double longitude) =>
        Push(Message(chat, "\"location\":" + Location(latitude, longitude)));

    // A venue message, without the copy of its location that Telegram also puts on the message.
    public void UserSendsVenue(long chat, double latitude, double longitude) =>
        Push(Message(chat, "\"venue\":{\"location\":" + Location(latitude, longitude) +
                           ",\"title\":\"Somewhere\",\"address\":\"Some street\"}"));

    public void UserPressesButton(long chat, int messageId, string data) =>
        Push("\"callback_query\":{\"id\":\"cb" + _updateId + "\",\"from\":" + User(chat) +
             ",\"message\":{\"message_id\":" + messageId + ",\"date\":1700000000,\"chat\":" + Chat(chat) + "}" +
             ",\"chat_instance\":\"test\",\"data\":" + JsonSerializer.Serialize(data) + "}");

    // ---- The Bot API --------------------------------------------------------------------------

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.RequestUri!.AbsolutePath.Split('/').Last().ToLowerInvariant();

        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var (chat, text, messageId) = Parse(body);

        _calls.Enqueue(new TelegramCall(method, body, chat, text));

        switch (method)
        {
            case "getme":
                return Ok("{\"id\":1,\"is_bot\":true,\"first_name\":\"Optinull\",\"username\":\"optinull_test_bot\"}");

            case "getupdates":
                // Waits like a long poll; cancelling the poller ends the wait.
                await _updates.Reader.WaitToReadAsync(cancellationToken);

                var batch = new List<string>();

                while (_updates.Reader.TryRead(out var update))
                    batch.Add(update);

                return Ok("[" + string.Join(",", batch) + "]");

            case "sendmessage":
            case "editmessagetext":
            case "sendphoto":
                var id = messageId ?? Interlocked.Increment(ref _messageId);

                return Ok("{\"message_id\":" + id + ",\"date\":1700000000,\"chat\":" + Chat(chat) +
                          ",\"text\":" + JsonSerializer.Serialize(text) + "}");

            default:
                return Ok("true");
        }
    }

    private void Push(string payload) =>
        _updates.Writer.TryWrite("{\"update_id\":" + Interlocked.Increment(ref _updateId) + "," + payload + "}");

    private string Message(long chat, string extra) =>
        "\"message\":{\"message_id\":" + Interlocked.Increment(ref _messageId) + ",\"date\":1700000000,\"chat\":" +
        Chat(chat) + ",\"from\":" + User(chat) + "," + extra + "}";

    private static string Chat(long chat) => "{\"id\":" + chat + ",\"type\":\"private\"}";

    private static string User(long id) => "{\"id\":" + id + ",\"is_bot\":false,\"first_name\":\"Tester\"}";

    private static string Location(double latitude, double longitude) =>
        "{\"latitude\":" + latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ",\"longitude\":" + longitude.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";

    private static HttpResponseMessage Ok(string result) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true,\"result\":" + result + "}", Encoding.UTF8, "application/json")
        };

    // Reads what the bot sent. Uploads (photos) are not JSON, so they give no chat or text.
    private static (long Chat, string Text, int? MessageId) Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var chat = root.TryGetProperty("chat_id", out var chatId)
                ? chatId.ValueKind == JsonValueKind.Number ? chatId.GetInt64() : long.Parse(chatId.GetString()!)
                : 1;

            var text = root.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? "" : "";

            int? messageId = root.TryGetProperty("message_id", out var id) && id.ValueKind == JsonValueKind.Number
                ? id.GetInt32()
                : null;

            return (chat, text, messageId);
        }
        catch (JsonException)
        {
            return (1, string.Empty, null);
        }
    }
}
