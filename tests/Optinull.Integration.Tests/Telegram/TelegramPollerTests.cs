using Optinull.Integration.Tests.Support;

namespace Optinull.Integration.Tests.Telegram;

/// <summary>
/// The real poller reading updates from a fake Telegram: how a location sent in a chat reaches
/// the map dialog, and how the answers go back.
/// </summary>
public sealed class TelegramPollerTests
{
    [Fact]
    public async Task LocationsAndVenues_BecomeTheDepotAndTheCustomers()
    {
        await using var bot = PollerHarness.Start();

        bot.Telegram.UserSendsText(42, "/solve vrp map greedy");
        bot.Telegram.UserSendsLocation(42, 52.50, 13.40);
        bot.Telegram.UserSendsLocation(42, 52.51, 13.41);
        bot.Telegram.UserSendsVenue(42, 52.52, 13.42);

        await Waiting.UntilAsync(
            () => bot.Telegram.Texts.Any(text => text.Contains("Customer 2 added")),
            "the second customer to be confirmed");

        Assert.Contains(bot.Telegram.Texts, text => text.Contains("Depot set (52.5, 13.4)"));
        Assert.Contains(bot.Telegram.Texts, text => text.Contains("Customer 1 added, demand 1 (52.51, 13.41)"));
        Assert.Contains(bot.Telegram.Texts, text => text.Contains("Customer 2 added, demand 1 (52.52, 13.42)"));

        // Every answer goes back to the chat that asked.
        Assert.All(bot.Telegram.Of("sendMessage"), call => Assert.Equal(42, call.ChatId));
    }

    [Fact]
    public async Task ALocation_WithoutADialog_IsExplained()
    {
        await using var bot = PollerHarness.Start();

        bot.Telegram.UserSendsLocation(42, 52.50, 13.40);

        await Waiting.UntilAsync(
            () => bot.Telegram.Texts.Any(text => text.Contains("not expecting a location")),
            "the explanation");

        Assert.Contains(bot.Telegram.Texts, text => text.Contains("/solve vrp map"));
    }

    [Fact]
    public async Task ANumber_AfterAPlace_SetsItsDemand()
    {
        await using var bot = PollerHarness.Start();

        bot.Telegram.UserSendsText(42, "/solve vrp map greedy");
        bot.Telegram.UserSendsLocation(42, 52.50, 13.40);
        bot.Telegram.UserSendsLocation(42, 52.51, 13.41);
        bot.Telegram.UserSendsText(42, "6");

        await Waiting.UntilAsync(
            () => bot.Telegram.Texts.Any(text => text.Contains("demand set to 6")),
            "the demand to be set");
    }

    [Fact]
    public async Task AButtonPress_EditsTheMessageItCameFrom_AndStopsTheSpinner()
    {
        await using var bot = PollerHarness.Start();

        bot.Telegram.UserSendsText(42, "/solve vrp map greedy");
        bot.Telegram.UserSendsLocation(42, 52.50, 13.40);
        bot.Telegram.UserSendsLocation(42, 52.51, 13.41);
        bot.Telegram.UserPressesButton(42, 555, "m:done");

        await Waiting.UntilAsync(
            () => bot.Telegram.Of("editMessageText").Any(call => call.Text.Contains("Total demand")),
            "the buttons' message to be edited");

        var edit = bot.Telegram.Of("editMessageText").First(call => call.Text.Contains("Total demand"));

        Assert.Equal(42, edit.ChatId);
        Assert.Contains("\"message_id\":555", edit.Body);
        Assert.NotEmpty(bot.Telegram.Of("answerCallbackQuery"));
    }

    [Fact]
    public async Task EachChat_HasItsOwnDialog()
    {
        await using var bot = PollerHarness.Start();

        bot.Telegram.UserSendsText(1, "/solve vrp map greedy");
        bot.Telegram.UserSendsText(2, "/solve vrp map greedy");
        bot.Telegram.UserSendsLocation(1, 52.50, 13.40);
        bot.Telegram.UserSendsLocation(2, 52.50, 13.40);
        bot.Telegram.UserSendsLocation(1, 52.51, 13.41);
        bot.Telegram.UserSendsText(2, "done");

        await Waiting.UntilAsync(
            () => bot.Telegram.Texts.Any(text => text.Contains("Customer 1 added")) &&
                  bot.Telegram.Texts.Any(text => text.Contains("at least one customer")),
            "both chats to be answered");

        var first = bot.Telegram.Calls.Where(call => call.ChatId == 1).Select(call => call.Text).ToList();
        var second = bot.Telegram.Calls.Where(call => call.ChatId == 2).Select(call => call.Text).ToList();

        Assert.Contains(first, text => text.Contains("Customer 1 added"));
        Assert.DoesNotContain(first, text => text.Contains("at least one customer"));
        Assert.Contains(second, text => text.Contains("at least one customer"));
        Assert.DoesNotContain(second, text => text.Contains("Customer 1 added"));
    }
}
