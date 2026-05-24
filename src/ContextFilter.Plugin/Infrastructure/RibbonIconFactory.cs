using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ContextFilter.Plugin.Infrastructure;

/// <summary>
/// Produces small in-memory ribbon icons so the sample builds without binary assets.
/// </summary>
public static class RibbonIconFactory
{
    /// <summary>Creates a simple context-filter bitmap icon.</summary>
    public static BitmapSource CreateIcon(int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var background = new SolidColorBrush(Color.FromRgb(30, 31, 34));
            var accent = new SolidColorBrush(Color.FromRgb(100, 210, 200));
            var muted = new SolidColorBrush(Color.FromRgb(169, 175, 184));
            context.DrawRectangle(background, null, new Rect(0, 0, size, size));
            context.DrawRoundedRectangle(null, new Pen(accent, Math.Max(1, size / 12.0)), new Rect(size * 0.18, size * 0.2, size * 0.64, size * 0.16), 2, 2);
            context.DrawRoundedRectangle(null, new Pen(muted, Math.Max(1, size / 14.0)), new Rect(size * 0.3, size * 0.46, size * 0.52, size * 0.14), 2, 2);
            context.DrawRoundedRectangle(null, new Pen(muted, Math.Max(1, size / 14.0)), new Rect(size * 0.42, size * 0.7, size * 0.4, size * 0.12), 2, 2);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Creates a simple bitmap icon for the snake game command.</summary>
    public static BitmapSource CreateSnakeIcon(int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var background = new SolidColorBrush(Color.FromRgb(30, 31, 34));
            var body = new SolidColorBrush(Color.FromRgb(124, 220, 90));
            var food = new SolidColorBrush(Color.FromRgb(255, 90, 90));
            context.DrawRectangle(background, null, new Rect(0, 0, size, size));

            var cell = size / 6.0;
            DrawSnakeCell(context, body, cell, 1, 3);
            DrawSnakeCell(context, body, cell, 2, 3);
            DrawSnakeCell(context, body, cell, 3, 3);
            DrawSnakeCell(context, body, cell, 3, 2);
            DrawSnakeCell(context, body, cell, 4, 2);
            context.DrawEllipse(food, null, new Point(size * 0.78, size * 0.72), cell * 0.34, cell * 0.34);
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawSnakeCell(DrawingContext context, Brush brush, double cell, int x, int y)
    {
        context.DrawRoundedRectangle(
            brush,
            null,
            new Rect(x * cell, y * cell, cell * 0.85, cell * 0.85),
            cell * 0.2,
            cell * 0.2);
    }
}
