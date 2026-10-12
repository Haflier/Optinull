namespace Optinull.Application.Commands;

public sealed record MenuCommand(string Name, string Description);

/// <summary>
/// The commands listed behind the "/" button next to the message box. Telegram wants
/// lower-case names without the slash, and descriptions of 3 to 256 characters.
/// </summary>
public static class BotMenu
{
    public static IReadOnlyList<MenuCommand> Commands { get; } =
    [
        new("optimize", "Problem => Dataset => Algorithm"),
        new("compare", "Run every algorithm on one problem"),
        new("solve", "Solve one problem"),
        new("cancel", "Cancel the current operation"),
        new("help", "Show what I can do")
    ];
}
