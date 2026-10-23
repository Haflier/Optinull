using Xunit.Sdk;

namespace Optinull.Integration.Tests.Support;

internal static class Waiting
{
    /// <summary>Polls until the condition holds. For work that happens on another thread.</summary>
    public static async Task UntilAsync(Func<bool> condition, string what, int seconds = 15)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);

        while (!condition())
        {
            if (DateTime.UtcNow > end)
                throw new XunitException($"Timed out waiting for: {what}");

            await Task.Delay(20);
        }
    }
}
