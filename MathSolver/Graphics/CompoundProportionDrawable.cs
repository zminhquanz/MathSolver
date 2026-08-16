using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

public sealed class CompoundProportionDrawable : IDrawable
{
    private const double BaseProducts = 120d;
    private const double BaseWorkers = 4d;
    private const double BaseHoursPerDay = 6d;

    public int ProductCount { get; set; } = 240;
    public int WorkerCount { get; set; } = 6;
    public int HoursPerDay { get; set; } = 8;
    public double DaysNeeded { get; set; } = 5d;

    private static Color DirectColor =>
        ThemeResource.GetColor("PrimaryColor", "#F97316");

    private static Color InverseColor =>
        Color.FromArgb(
            Application.Current?.RequestedTheme == AppTheme.Dark
                ? "#F9A8D4"
                : "#DB2777");

    private static Color AxisColor =>
        ThemeResource.GetColor("TextSecondaryColor", "#64748B");

    private static Color GridColor =>
        ThemeResource.GetColor("DividerColor", "#E2E8F0");

    private static Color TextColor =>
        ThemeResource.GetColor("TextPrimaryColor", "#1E293B");

    private static Color SurfaceColor =>
        ThemeResource.GetColor("SurfaceColor", "#FFFFFF");

    private static Color SurfaceAltColor =>
        ThemeResource.GetColor("SurfaceAltColor", "#F8FAFC");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 180f || dirtyRect.Height < 180f)
        {
            return;
        }

        canvas.SaveState();

        try
        {
            DrawTitle(canvas, dirtyRect);
            DrawRelationshipDiagram(canvas, dirtyRect);
            DrawRangeBars(canvas, dirtyRect);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawTitle(ICanvas canvas, RectF rect)
    {
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = rect.Width < 540f ? 17f : 20f;
        canvas.FontColor = TextColor;

        canvas.DrawString(
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Title"),
            rect.Left,
            rect.Top + 4f,
            rect.Width,
            28f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 12f;
        canvas.FontColor = AxisColor;

        canvas.DrawString(
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Subtitle"),
            rect.Left + 18f,
            rect.Top + 28f,
            rect.Width - 36f,
            26f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawRelationshipDiagram(ICanvas canvas, RectF rect)
    {
        float diagramTop = rect.Top + 68f;
        float diagramBottom = MathF.Min(rect.Bottom - 130f, rect.Top + rect.Height * 0.68f);
        float centerX = rect.Left + rect.Width * 0.5f;
        float centerY = diagramTop + (diagramBottom - diagramTop) * 0.50f;

        var dayBox = new RectF(centerX - 82f, centerY - 36f, 164f, 72f);
        DrawNode(
            canvas,
            new RectF(centerX - 90f, diagramTop, 180f, 58f),
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Products"),
            $"120 → {ProductCount.ToString("0", CultureInfo.CurrentCulture)}",
            DirectColor,
            true);

        DrawNode(
            canvas,
            dayBox,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Days"),
            DaysNeeded.ToString("0.##", CultureInfo.CurrentCulture),
            DirectColor,
            false);

        DrawNode(
            canvas,
            new RectF(rect.Left + rect.Width * 0.12f, diagramBottom - 52f, 172f, 58f),
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Workers"),
            $"4 → {WorkerCount.ToString("0", CultureInfo.CurrentCulture)}",
            InverseColor,
            true);

        DrawNode(
            canvas,
            new RectF(rect.Right - rect.Width * 0.12f - 172f, diagramBottom - 52f, 172f, 58f),
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Hours"),
            $"6 → {HoursPerDay.ToString("0", CultureInfo.CurrentCulture)}",
            InverseColor,
            true);

        // Product -> days (direct)
        DrawArrow(
            canvas,
            centerX,
            diagramTop + 58f,
            centerX,
            dayBox.Top,
            DirectColor,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Direct"));

        // Workers -> days (inverse)
        DrawArrow(
            canvas,
            rect.Left + rect.Width * 0.12f + 86f,
            diagramBottom - 54f,
            dayBox.Left + 24f,
            dayBox.Bottom,
            InverseColor,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Inverse"));

        // Hours -> days (inverse)
        DrawArrow(
            canvas,
            rect.Right - rect.Width * 0.12f - 86f,
            diagramBottom - 54f,
            dayBox.Right - 24f,
            dayBox.Bottom,
            InverseColor,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Inverse"));
    }

    private void DrawRangeBars(ICanvas canvas, RectF rect)
    {
        float startY = rect.Bottom - 114f;
        float left = rect.Left + 20f;
        float right = rect.Right - 20f;
        float barLeft = left + 120f;
        float barWidth = right - barLeft - 12f;
        if (barWidth < 80f)
        {
            return;
        }

        DrawRangeBar(
            canvas,
            startY,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Products"),
            ProductCount,
            60d,
            300d,
            barLeft,
            barWidth,
            DirectColor);

        DrawRangeBar(
            canvas,
            startY + 28f,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Workers"),
            WorkerCount,
            2d,
            10d,
            barLeft,
            barWidth,
            InverseColor);

        DrawRangeBar(
            canvas,
            startY + 56f,
            LocalizationService.TranslateKey("Formula.Proportion.Compound.Graph.Hours"),
            HoursPerDay,
            4d,
            10d,
            barLeft,
            barWidth,
            InverseColor);
    }

    private void DrawRangeBar(
        ICanvas canvas,
        float y,
        string label,
        double value,
        double min,
        double max,
        float barLeft,
        float barWidth,
        Color color)
    {
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 13f;
        canvas.FontColor = TextColor;

        canvas.DrawString(
            label,
            barLeft - 118f,
            y - 10f,
            88f,
            20f,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);

        canvas.DrawString(
            value.ToString("0.##", CultureInfo.CurrentCulture),
            barLeft - 32f,
            y - 10f,
            28f,
            20f,
            HorizontalAlignment.Right,
            VerticalAlignment.Center);

        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 6f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(barLeft, y, barLeft + barWidth, y);

        float x = barLeft + (float)((value - min) / (max - min)) * barWidth;
        canvas.StrokeColor = color;
        canvas.StrokeSize = 4f;
        canvas.DrawLine(barLeft, y, x, y);

        canvas.FillColor = SurfaceColor;
        canvas.FillCircle(x, y, 7.5f);
        canvas.FillColor = color;
        canvas.FillCircle(x, y, 4.5f);
    }

    private void DrawNode(ICanvas canvas, RectF rect, string title, string value, Color accent, bool subtle)
    {
        canvas.FillColor = subtle ? SurfaceAltColor : SurfaceColor;
        canvas.FillRoundedRectangle(rect.Left, rect.Top, rect.Width, rect.Height, 14f);
        canvas.StrokeColor = subtle ? GridColor : accent;
        canvas.StrokeSize = 1.2f;
        canvas.DrawRoundedRectangle(rect.Left, rect.Top, rect.Width, rect.Height, 14f);

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = rect.Width < 150f ? 12f : 13f;
        canvas.FontColor = accent;
        canvas.DrawString(title, rect.Left + 8f, rect.Top + 7f, rect.Width - 16f, 18f, HorizontalAlignment.Center, VerticalAlignment.Center);

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = rect.Width < 150f ? 15f : 18f;
        canvas.FontColor = TextColor;
        canvas.DrawString(value, rect.Left + 8f, rect.Top + 26f, rect.Width - 16f, rect.Height - 30f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private void DrawArrow(ICanvas canvas, float x1, float y1, float x2, float y2, Color color, string label)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2.2f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(x1, y1, x2, y2);

        float angle = MathF.Atan2(y2 - y1, x2 - x1);
        float arrowLength = 10f;
        float arrowAngle = 0.55f;
        canvas.DrawLine(x2, y2, x2 - arrowLength * MathF.Cos(angle - arrowAngle), y2 - arrowLength * MathF.Sin(angle - arrowAngle));
        canvas.DrawLine(x2, y2, x2 - arrowLength * MathF.Cos(angle + arrowAngle), y2 - arrowLength * MathF.Sin(angle + arrowAngle));

        float midX = (x1 + x2) * 0.5f;
        float midY = (y1 + y2) * 0.5f;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = 11f;
        canvas.FontColor = color;
        canvas.DrawString(label, midX - 24f, midY - 22f, 48f, 16f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
