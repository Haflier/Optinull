namespace Optinull.Reporting.Charts;

/// <summary>One 256-pixel map tile in the usual slippy-map scheme (zoom, column, row).</summary>
public readonly record struct MapTile(int Zoom, int X, int Y);

/// <summary>
/// The part of the world a map picture shows, in Web Mercator like OpenStreetMap tiles: the zoom
/// level, and where the picture's top-left corner sits in the world's pixel grid. It also knows
/// which tiles cover the picture.
/// </summary>
public sealed class MapViewport
{
    public const int TileSize = 256;

    private const double MaxLatitude = 85.0511;

    private readonly double _originX;
    private readonly double _originY;

    private MapViewport(int zoom, int width, int height, double originX, double originY)
    {
        Zoom = zoom;
        Width = width;
        Height = height;
        _originX = originX;
        _originY = originY;
    }

    public int Zoom { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The closest zoom at which every point fits inside the picture, with a margin.</summary>
    public static MapViewport Fit(
        IEnumerable<RoutePoint> points,
        int width = 1000,
        int height = 1000,
        int padding = 110,
        int maxZoom = 18)
    {
        ArgumentNullException.ThrowIfNull(points);

        var list = points.ToList();

        if (list.Count == 0)
            throw new ArgumentException("At least one point is required.", nameof(points));

        var xs = list.Select(point => WorldX(point.X)).ToList();
        var ys = list.Select(point => WorldY(point.Y)).ToList();

        var spanX = xs.Max() - xs.Min();
        var spanY = ys.Max() - ys.Min();
        var availableWidth = Math.Max(1, width - 2 * padding);
        var availableHeight = Math.Max(1, height - 2 * padding);

        var zoom = 0;

        for (var candidate = maxZoom; candidate >= 0; candidate--)
        {
            var size = TileSize * Math.Pow(2, candidate);

            if (spanX * size <= availableWidth && spanY * size <= availableHeight)
            {
                zoom = candidate;
                break;
            }
        }

        var worldSize = TileSize * Math.Pow(2, zoom);
        var centerX = (xs.Min() + xs.Max()) / 2 * worldSize;
        var centerY = (ys.Min() + ys.Max()) / 2 * worldSize;

        return new MapViewport(zoom, width, height, centerX - width / 2.0, centerY - height / 2.0);
    }

    /// <summary>Where a point (X longitude, Y latitude) lands in the picture, in pixels.</summary>
    public (double X, double Y) ToPixel(RoutePoint point)
    {
        var worldSize = TileSize * Math.Pow(2, Zoom);

        return (WorldX(point.X) * worldSize - _originX, WorldY(point.Y) * worldSize - _originY);
    }

    /// <summary>Where a tile's top-left corner lands in the picture, in pixels.</summary>
    public (double X, double Y) TileOrigin(MapTile tile) =>
        (tile.X * TileSize - _originX, tile.Y * TileSize - _originY);

    /// <summary>Every tile the picture touches.</summary>
    public IEnumerable<MapTile> Tiles()
    {
        var count = 1 << Zoom;

        var firstX = (int)Math.Floor(_originX / TileSize);
        var lastX = (int)Math.Floor((_originX + Width) / TileSize);
        var firstY = (int)Math.Floor(_originY / TileSize);
        var lastY = (int)Math.Floor((_originY + Height) / TileSize);

        for (var y = Math.Max(0, firstY); y <= Math.Min(count - 1, lastY); y++)
        {
            for (var x = Math.Max(0, firstX); x <= Math.Min(count - 1, lastX); x++)
                yield return new MapTile(Zoom, x, y);
        }
    }

    // The world is a square of side 1 (west to east, north to south) before it is scaled by zoom.
    private static double WorldX(double longitude) => (longitude + 180) / 360;

    private static double WorldY(double latitude)
    {
        var radians = Math.Clamp(latitude, -MaxLatitude, MaxLatitude) * Math.PI / 180;

        return (1 - Math.Log(Math.Tan(radians) + 1 / Math.Cos(radians)) / Math.PI) / 2;
    }
}
