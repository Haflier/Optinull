using SkiaSharp;

// SkiaSharp 3 prefers SKFont for text; the SKPaint text members still work and are simpler here.
#pragma warning disable CS0618

namespace Optinull.Reporting.Charts;

/// <summary>
/// One truck on a map: the line it drives, the customers it stops at in order, and its legend
/// text. Points are longitude (X) and latitude (Y) in degrees.
/// </summary>
public sealed record RoadChartRoute(
    IReadOnlyList<RoutePoint> Line,
    IReadOnlyList<RoutePoint> Stops,
    string Label);

/// <summary>
/// Draws routes as PNG bytes: map tiles as the background when there are any, then one colour per
/// truck, numbered stops, a depot square, a title, a legend and the map credit. Without tiles the
/// background is plain.
/// </summary>
public sealed class RoadRouteChartRenderer
{
    private static readonly string[] Colors =
    [
        "#d62728", "#1f77b4", "#2ca02c", "#ff7f0e", "#9467bd",
        "#8c564b", "#e377c2", "#17becf", "#bcbd22", "#4d4d4d"
    ];

    public byte[] Render(
        RoutePoint depot,
        IReadOnlyList<RoadChartRoute> routes,
        string title,
        MapViewport? viewport = null,
        IReadOnlyDictionary<MapTile, byte[]>? tiles = null,
        string? attribution = null)
    {
        ArgumentNullException.ThrowIfNull(routes);

        if (routes.Count == 0)
            throw new ArgumentException("At least one route is required.", nameof(routes));

        viewport ??= MapViewport.Fit(routes.SelectMany(route => route.Line).Append(depot));

        using var surface = SKSurface.Create(new SKImageInfo(viewport.Width, viewport.Height));
        var canvas = surface.Canvas;

        canvas.Clear(SKColor.Parse("#e9e6df"));

        var drewTiles = DrawTiles(canvas, viewport, tiles);

        // A light veil makes the coloured routes stand out from the streets.
        if (drewTiles)
        {
            using var veil = new SKPaint { Color = new SKColor(255, 255, 255, 70) };

            canvas.DrawRect(0, 0, viewport.Width, viewport.Height, veil);
        }

        for (var index = 0; index < routes.Count; index++)
            DrawLine(canvas, viewport, routes[index].Line, ColorOf(index));

        for (var index = 0; index < routes.Count; index++)
        {
            for (var stop = 0; stop < routes[index].Stops.Count; stop++)
                DrawStop(canvas, viewport, routes[index].Stops[stop], ColorOf(index), (stop + 1).ToString());
        }

        DrawDepot(canvas, viewport, depot);
        DrawTitle(canvas, viewport, title);
        DrawLegend(canvas, viewport, routes);

        if (drewTiles && !string.IsNullOrWhiteSpace(attribution))
            DrawAttribution(canvas, viewport, attribution);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    private static SKColor ColorOf(int index) => SKColor.Parse(Colors[index % Colors.Length]);

    private static bool DrawTiles(
        SKCanvas canvas,
        MapViewport viewport,
        IReadOnlyDictionary<MapTile, byte[]>? tiles)
    {
        if (tiles is null)
            return false;

        var drew = false;

        foreach (var (tile, bytes) in tiles)
        {
                        using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);

            // A server can answer with something that is not an image (an error page, say).
            if (codec is null)
                continue;

            using var bitmap = SKBitmap.Decode(codec);

            if (bitmap is null)
                continue;

            var (x, y) = viewport.TileOrigin(tile);

            canvas.DrawBitmap(bitmap, SKRect.Create(
                (float)x, (float)y, MapViewport.TileSize, MapViewport.TileSize));
            drew = true;
        }

        return drew;
    }

    private static void DrawLine(SKCanvas canvas, MapViewport viewport, IReadOnlyList<RoutePoint> line, SKColor color)
    {
        if (line.Count < 2)
            return;

        using var path = new SKPath();

        for (var i = 0; i < line.Count; i++)
        {
            var (x, y) = viewport.ToPixel(line[i]);

            if (i == 0)
                path.MoveTo((float)x, (float)y);
            else
                path.LineTo((float)x, (float)y);
        }

        // A white edge keeps the line readable over busy streets.
        using var edge = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 10,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(255, 255, 255, 220)
        };

        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = color
        };

        canvas.DrawPath(path, edge);
        canvas.DrawPath(path, stroke);
    }

    private static void DrawStop(SKCanvas canvas, MapViewport viewport, RoutePoint stop, SKColor color, string number)
    {
        var (x, y) = viewport.ToPixel(stop);

        using var ring = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var fill = new SKPaint { IsAntialias = true, Color = color };

        canvas.DrawCircle((float)x, (float)y, 17, ring);
        canvas.DrawCircle((float)x, (float)y, 14, fill);

        DrawCentered(canvas, number, (float)x, (float)y, 17, SKColors.White);
    }

    private static void DrawDepot(SKCanvas canvas, MapViewport viewport, RoutePoint depot)
    {
        var (x, y) = viewport.ToPixel(depot);

        using var ring = new SKPaint { IsAntialias = true, Color = SKColors.White };
        using var fill = new SKPaint { IsAntialias = true, Color = SKColor.Parse("#111827") };

        canvas.DrawRoundRect(SKRect.Create((float)x - 20, (float)y - 20, 40, 40), 8, 8, ring);
        canvas.DrawRoundRect(SKRect.Create((float)x - 17, (float)y - 17, 34, 34), 6, 6, fill);

        DrawCentered(canvas, "D", (float)x, (float)y, 20, SKColors.White);
    }

    private static void DrawTitle(SKCanvas canvas, MapViewport viewport, string title)
    {
        using var bar = new SKPaint { Color = new SKColor(17, 24, 39, 200) };

        canvas.DrawRect(0, 0, viewport.Width, 48, bar);

        using var text = TextPaint(24, SKColors.White);

        canvas.DrawText(title, 16, 32, text);
    }

    private static void DrawLegend(SKCanvas canvas, MapViewport viewport, IReadOnlyList<RoadChartRoute> routes)
    {
        const float rowHeight = 30;
        const float margin = 14;

        var labels = routes.Select(route => route.Label).Append("Depot").ToList();

        using var text = TextPaint(18, SKColor.Parse("#111827"));

        var width = labels.Max(label => text.MeasureText(label)) + 56;
        var height = labels.Count * rowHeight + 16;
        var top = viewport.Height - height - margin;

        using var background = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 230) };

        canvas.DrawRoundRect(SKRect.Create(margin, top, width, height), 8, 8, background);

        for (var index = 0; index < labels.Count; index++)
        {
            var middle = top + 8 + index * rowHeight + rowHeight / 2;
            var isDepot = index == routes.Count;

            using var swatch = new SKPaint
            {
                IsAntialias = true,
                Color = isDepot ? SKColor.Parse("#111827") : ColorOf(index)
            };

            if (isDepot)
                canvas.DrawRect(margin + 10, middle - 8, 16, 16, swatch);
            else
                canvas.DrawRect(margin + 10, middle - 3, 16, 6, swatch);

            canvas.DrawText(labels[index], margin + 36, middle + 6, text);
        }
    }

    private static void DrawAttribution(SKCanvas canvas, MapViewport viewport, string attribution)
    {
        using var text = TextPaint(13, SKColor.Parse("#1f2937"), bold: false);
        using var background = new SKPaint { Color = new SKColor(255, 255, 255, 215) };

        var width = text.MeasureText(attribution) + 12;

        canvas.DrawRect(viewport.Width - width, viewport.Height - 22, width, 22, background);
        canvas.DrawText(attribution, viewport.Width - width + 6, viewport.Height - 7, text);
    }

    private static void DrawCentered(SKCanvas canvas, string text, float x, float y, float size, SKColor color)
    {
        using var paint = TextPaint(size, color);

        canvas.DrawText(text, x - paint.MeasureText(text) / 2, y + size * 0.35f, paint);
    }

    private static SKPaint TextPaint(float size, SKColor color, bool bold = true) =>
        new()
        {
            IsAntialias = true,
            Color = color,
            TextSize = size,
            Typeface = SKTypeface.FromFamilyName(
                "sans-serif",
                bold ? SKFontStyle.Bold : SKFontStyle.Normal)
        };
}
