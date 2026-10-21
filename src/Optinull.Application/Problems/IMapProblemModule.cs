namespace Optinull.Application.Problems;

/// <summary>
/// A problem whose instance can also be drawn on a real map instead of typed in, as in
/// /solve vrp map or Map in the /optimize wizard: the user sends places as Telegram locations.
/// </summary>
public interface IMapProblemModule : IProblemModule
{
}
