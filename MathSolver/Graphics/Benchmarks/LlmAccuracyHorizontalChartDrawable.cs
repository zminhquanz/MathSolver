using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

/// <summary>
/// Horizontal bar chart used by the AI/LLM hardware benchmark.
/// The chart is intentionally data-driven: adding another benchmark category
/// only requires adding another item to <see cref="Items"/>.
/// </summary>
public sealed class LlmAccuracyHorizontalChartDrawable : IDrawable
{
    private const float TopAxisHeight = 28f;
    private const float RowHeight = 44f;
    private const float BarHeight = 18f;
    private const float HorizontalPadding = 8f;

    public IReadOnlyList<LlmAccuracyChartItem> Items { get; set; } =
        Array.Empty<LlmAccuracyChartItem>();

    public void Draw(
        ICanvas canvas,
        RectF dirtyRect)
    {
        if (Items.Count == 0 ||
            dirtyRect.Width < 220f ||
            dirtyRect.Height < 80f)
        {
            return;
        }

        float labelWidth =
            dirtyRect.Width >= 760f
                ? 190f
                : dirtyRect.Width >= 520f
                    ? 165f
                    : 132f;

        float valueWidth = 54f;
        float plotLeft = dirtyRect.Left + HorizontalPadding + labelWidth;
        float plotRight = dirtyRect.Right - HorizontalPadding - valueWidth;
        float plotWidth = MathF.Max(1f, plotRight - plotLeft);
        float rowsTop = dirtyRect.Top + TopAxisHeight;
        float rowsBottom = MathF.Min(
            dirtyRect.Bottom,
            rowsTop + Items.Count * RowHeight);

        canvas.SaveState();

        try
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            DrawScale(
                canvas,
                plotLeft,
                plotWidth,
                rowsTop,
                rowsBottom);

            for (int index = 0;
                 index < Items.Count;
                 index++)
            {
                DrawItem(
                    canvas,
                    dirtyRect,
                    Items[index],
                    index,
                    labelWidth,
                    valueWidth,
                    plotLeft,
                    plotWidth,
                    rowsTop);
            }
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private static void DrawScale(
        ICanvas canvas,
        float plotLeft,
        float plotWidth,
        float rowsTop,
        float rowsBottom)
    {
        canvas.StrokeColor = DividerColor;
        canvas.StrokeSize = 1f;
        canvas.FontColor = TextSecondaryColor;
        canvas.FontSize = 11f;

        int[] ticks = [0, 25, 50, 75, 100];

        foreach (int tick in ticks)
        {
            float x =
                plotLeft +
                plotWidth * tick / 100f;

            canvas.DrawLine(
                x,
                rowsTop - 3f,
                x,
                rowsBottom);

            canvas.DrawString(
                $"{tick}%",
                x - 24f,
                rowsTop - TopAxisHeight,
                48f,
                22f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private static void DrawItem(
        ICanvas canvas,
        RectF dirtyRect,
        LlmAccuracyChartItem item,
        int index,
        float labelWidth,
        float valueWidth,
        float plotLeft,
        float plotWidth,
        float rowsTop)
    {
        double accuracy =
            Math.Clamp(
                item.AccuracyPercent,
                0d,
                100d);

        float rowTop =
            rowsTop + index * RowHeight;
        float centerY =
            rowTop + RowHeight / 2f;
        float barTop =
            centerY - BarHeight / 2f;

        canvas.FontColor = TextPrimaryColor;
        canvas.FontSize = dirtyRect.Width < 520f
            ? 12f
            : 13f;
        canvas.DrawString(
            item.Label,
            dirtyRect.Left + HorizontalPadding,
            rowTop,
            MathF.Max(1f, labelWidth - 12f),
            RowHeight,
            HorizontalAlignment.Right,
            VerticalAlignment.Center);

        canvas.FillColor = TrackColor;
        canvas.FillRoundedRectangle(
            plotLeft,
            barTop,
            plotWidth,
            BarHeight,
            BarHeight / 2f);

        canvas.StrokeColor = DividerColor;
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(
            plotLeft,
            barTop,
            plotWidth,
            BarHeight,
            BarHeight / 2f);

        float filledWidth =
            plotWidth * (float)(accuracy / 100d);

        if (filledWidth > 0.5f)
        {
            canvas.FillColor = PrimaryColor;
            canvas.FillRoundedRectangle(
                plotLeft,
                barTop,
                MathF.Max(1f, filledWidth),
                BarHeight,
                MathF.Min(
                    BarHeight / 2f,
                    MathF.Max(1f, filledWidth / 2f)));
        }

        canvas.FontColor = PrimaryColor;
        canvas.FontSize = 13f;
        canvas.DrawString(
            string.Format(
                CultureInfo.CurrentCulture,
                "{0:F0}%",
                accuracy),
            plotLeft + plotWidth + 8f,
            rowTop,
            MathF.Max(1f, valueWidth - 8f),
            RowHeight,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    private static Color PrimaryColor =>
        ThemeResource.GetColor(
            "PrimaryColor",
            "#7C3AED");

    private static Color TextPrimaryColor =>
        ThemeResource.GetColor(
            "TextPrimaryColor",
            "#1E293B");

    private static Color TextSecondaryColor =>
        ThemeResource.GetColor(
            "TextSecondaryColor",
            "#64748B");

    private static Color DividerColor =>
        ThemeResource.GetColor(
            "DividerColor",
            "#E2E8F0");

    private static Color TrackColor =>
        ThemeResource.GetColor(
            "SurfaceAltColor",
            "#F8FAFC");
}

public sealed record LlmAccuracyChartItem(
    string Label,
    double AccuracyPercent);
