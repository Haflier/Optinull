using System.Text.RegularExpressions;
using Optinull.Application.Commands;

namespace Optinull.Application.Tests;

public sealed class BotMenuTests
{
    [Fact]
    public void EveryCommand_HasAValidTelegramNameAndDescription()
    {
        Assert.NotEmpty(BotMenu.Commands);
        Assert.True(BotMenu.Commands.Count <= 100);

        foreach (var command in BotMenu.Commands)
        {
            Assert.Matches("^[a-z0-9_]{1,32}$", command.Name);
            Assert.InRange(command.Description.Length, 3, 256);
        }

        Assert.Equal(
            BotMenu.Commands.Count,
            BotMenu.Commands.Select(command => command.Name).Distinct().Count());
    }

    [Fact]
    public void EveryCommand_IsOneTheBotUnderstands()
    {
        foreach (var command in BotMenu.Commands)
        {
            var parsed = BotCommandParser.Parse("/" + command.Name);

            // /solve and /compare without a problem answer with their usage, which is fine.
            if (parsed is InvalidCommand invalid)
                Assert.DoesNotContain("Unknown command", invalid.Message);
        }
    }

    [Fact]
    public void TheMenu_ListsEveryCommandUsersNeed()
    {
        Assert.Equal(
            new[] { "cancel", "compare", "help", "optimize", "solve" },
            BotMenu.Commands.Select(command => command.Name).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void Tapping_SolveOrCompareWithoutAProblem_ExplainsTheUsage()
    {
        Assert.StartsWith("Usage: /solve", Assert.IsType<InvalidCommand>(BotCommandParser.Parse("/solve")).Message);
        Assert.StartsWith("Usage: /compare", Assert.IsType<InvalidCommand>(BotCommandParser.Parse("/compare")).Message);
    }

    [Fact]
    public void Descriptions_MentionNothingTheBotCannotDo()
    {
        // The hint in /solve must list only solvers the parser accepts.
        foreach (var solver in new[] { "greedy", "hc", "sa", "ga", "all" })
            Assert.IsType<SolveCommand>(BotCommandParser.Parse($"/solve ft06 {solver}"));

        Assert.Contains(
            "[greedy|hc|sa|ga|all]",
            BotMenu.Commands.Single(command => command.Name == "solve").Description);
    }
}
