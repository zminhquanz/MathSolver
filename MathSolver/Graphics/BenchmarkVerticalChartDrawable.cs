using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

/// <summary>
/// Reusable vertical bar chart for raw hardware benchmark comparisons.
/// Supports one or more bars per category and nullable values so an
/// unsupported SIMD width can remain visible as N/A instead of disappearing.
/// </summary>
public sealed class BenchmarkVerticalChartDrawable : IDrawable
{
    private const float LeftAxisWidth = 58f;
    private const float RightPadding = 10f;
    private const float TopPadding = 10f;
    private const float LegendHeight = 28f;
    private const float BottomLabelHeight = 42f;
    private const float BarGap = 6f;

    public IReadOnlyList<string> SeriesLabels { get; set; } =
        Array.Empty<string>();

    public IReadOnlyList<BenchmarkVerticalChartGroup> Groups { get; set; } =
        Array.Empty<BenchmarkVerticalChartGroup>();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Groups.Count == 0 ||
            dirtyRect.Width < 240f ||
            dirtyRect.Height < 160f)
        {
            return;
        }

        int seriesCount = Math.Max(
            1,
            Math.Max(
                SeriesLabels.Count,
                Groups.Max(group => group.Values.Count)));

        bool showLegend =
            seriesCount > 1 &&
            SeriesLabels.Count > 1;

        float chartTop =
            dirtyRect.Top +
            TopPadding +
            (showLegend ? LegendHeight : 6f);

        float chartBottom =
            dirtyRect.Bottom - BottomLabelHeight;

        float chartLeft =
            dirtyRect.Left + LeftAxisWidth;

        float chartRight =
            dirtyRect.Right - RightPadding;

        float chartWidth =
            MathF.Max(1f, chartRight - chartLeft);

        float chartHeight =
            MathF.Max(1f, chartBottom - chartTop);

        double maximum =
            Groups
                .SelectMany(group => group.Values)
                .Where(value => value.HasValue)
                .Select(value => Math.Max(0d, value!.Value))
                .DefaultIfEmpty(1d)
                .Max();

        if (maximum <= 0d)
        {
            maximum = 1d;
        }

        // Leave a little headroom for the value labels above the tallest bar.
        maximum *= 1.12d;

        canvas.SaveState();

        try
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;

            if (showLegend)
            {
                DrawLegend(
                    canvas,
                    dirtyRect,
                    seriesCount);
            }

            DrawYAxis(
                canvas,
                chartLeft,
                chartRight,
                chartTop,
                chartBottom,
                maximum);

            DrawGroups(
                canvas,
                chartLeft,
                chartTop,
                chartWidth,
                chartHeight,
                chartBottom,
                maximum,
                seriesCount);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawLegend(
        ICanvas canvas,
        RectF dirtyRect,
        int seriesCount)
    {
        float itemWidth =
            MathF.Min(
                150f,
                MathF.Max(
                    92f,
                    (dirtyRect.Width - LeftAxisWidth) /
                    Math.Max(1, seriesCount)));

        float totalWidth =
            itemWidth * seriesCount;

        float startX =
            MathF.Max(
                dirtyRect.Left + LeftAxisWidth,
                dirtyRect.Right - totalWidth - RightPadding);

        for (int index = 0;
             index < seriesCount;
             index++)
        {
            string label =
                index < SeriesLabels.Count
                    ? SeriesLabels[index]
                    : $"Series {index + 1}";

            float x =
                startX + index * itemWidth;

            canvas.FillColor =
                GetSeriesColor(index);

            canvas.FillRoundedRectangle(
                x,
                dirtyRect.Top + TopPadding + 8f,
                12f,
                12f,
                3f);

            canvas.FontColor = TextSecondaryColor;
            canvas.FontSize = 11f;
            canvas.DrawString(
                label,
                x + 18f,
                dirtyRect.Top + TopPadding,
                itemWidth - 20f,
                28f,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }
    }

    private static void DrawYAxis(
        ICanvas canvas,
        float chartLeft,
        float chartRight,
        float chartTop,
        float chartBottom,
        double maximum)
    {
        canvas.StrokeColor = DividerColor;
        canvas.StrokeSize = 1f;
        canvas.FontColor = TextSecondaryColor;
        canvas.FontSize = 10f;

        const int tickCount = 4;

        for (int tick = 0;
             tick <= tickCount;
             tick++)
        {
            double fraction =
                tick / (double)tickCount;

            float y =
                chartBottom -
                (chartBottom - chartTop) *
                (float)fraction;

            canvas.DrawLine(
                chartLeft,
                y,
                chartRight,
                y);

            string valueText =
                FormatCompactValue(
                    maximum * fraction);

            canvas.DrawString(
                valueText,
                chartLeft - LeftAxisWidth + 2f,
                y - 10f,
                LeftAxisWidth - 8f,
                20f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }
    }

    private void DrawGroups(
        ICanvas canvas,
        float chartLeft,
        float chartTop,
        float chartWidth,
        float chartHeight,
        float chartBottom,
        double maximum,
        int seriesCount)
    {
        float groupWidth =
            chartWidth /
            Math.Max(1, Groups.Count);

        float usableGroupWidth =
            groupWidth * 0.72f;

        float barWidth =
            MathF.Min(
                64f,
                MathF.Max(
                    12f,
                    (usableGroupWidth -
                     BarGap * (seriesCount - 1)) /
                    seriesCount));

        float barsWidth =
            barWidth * seriesCount +
            BarGap * (seriesCount - 1);

        for (int groupIndex = 0;
             groupIndex < Groups.Count;
             groupIndex++)
        {
            BenchmarkVerticalChartGroup group =
                Groups[groupIndex];

            float groupCenter =
                chartLeft +
                groupWidth * (groupIndex + 0.5f);

            float startX =
                groupCenter - barsWidth / 2f;

            for (int seriesIndex = 0;
                 seriesIndex < seriesCount;
                 seriesIndex++)
            {
                double? value =
                    seriesIndex < group.Values.Count
                        ? group.Values[seriesIndex]
                        : null;

                float x =
                    startX +
                    seriesIndex * (barWidth + BarGap);

                if (!value.HasValue)
                {
                    DrawUnavailableBar(
                        canvas,
                        x,
                        chartTop,
                        chartBottom,
                        barWidth);

                    continue;
                }

                double normalized =
                    Math.Clamp(
                        value.Value / maximum,
                        0d,
                        1d);

                float barHeight =
                    chartHeight *
                    (float)normalized;

                float y =
                    chartBottom - barHeight;

                canvas.FillColor =
                    GetSeriesColor(seriesIndex);

                canvas.FillRoundedRectangle(
                    x,
                    y,
                    barWidth,
                    MathF.Max(2f, barHeight),
                    MathF.Min(8f, barWidth / 3f));

                canvas.FontColor = TextPrimaryColor;
                canvas.FontSize = 10f;
                canvas.DrawString(
                    FormatCompactValue(value.Value),
                    x - 12f,
                    MathF.Max(
                        chartTop - 2f,
                        y - 20f),
                    barWidth + 24f,
                    18f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }

            canvas.FontColor = TextPrimaryColor;
            canvas.FontSize = 11f;
            canvas.DrawString(
                group.Label,
                chartLeft + groupIndex * groupWidth,
                chartBottom + 7f,
                groupWidth,
                BottomLabelHeight - 7f,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }
    }

    private static void DrawUnavailableBar(
        ICanvas canvas,
        float x,
        float chartTop,
        float chartBottom,
        float barWidth)
    {
        float markerHeight = 8f;
        float markerY =
            chartBottom - markerHeight;

        canvas.FillColor = SurfaceAltColor;
        canvas.FillRoundedRectangle(
            x,
            markerY,
            barWidth,
            markerHeight,
            3f);

        canvas.StrokeColor = DividerColor;
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(
            x,
            markerY,
            barWidth,
            markerHeight,
            3f);

        canvas.FontColor = TextSecondaryColor;
        canvas.FontSize = 10f;
        canvas.DrawString(
            "N/A",
            x - 8f,
            MathF.Max(
                chartTop,
                markerY - 20f),
            barWidth + 16f,
            18f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static string FormatCompactValue(
        double value)
    {
        double absolute =
            Math.Abs(value);

        if (absolute >= 1_000_000_000d)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.#}B",
                value / 1_000_000_000d);
        }

        if (absolute >= 1_000_000d)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.#}M",
                value / 1_000_000d);
        }

        if (absolute >= 1_000d)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.#}K",
                value / 1_000d);
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0:0.#}",
            value);
    }

    private static Color GetSeriesColor(
        int index) =>
        index switch
        {
            0 => PrimaryColor,
            1 => InfoColor,
            2 => SuccessColor,
            _ => WarningColor
        };

    private static Color PrimaryColor =>
        ThemeResource.GetColor(
            "PrimaryColor",
            "#6D28D9");

    private static Color InfoColor =>
        ThemeResource.GetColor(
            "InfoColor",
            "#2563EB");

    private static Color SuccessColor =>
        ThemeResource.GetColor(
            "SuccessColor",
            "#15803D");

    private static Color WarningColor =>
        ThemeResource.GetColor(
            "WarningColor",
            "#C2410C");

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

    private static Color SurfaceAltColor =>
        ThemeResource.GetColor(
            "SurfaceAltColor",
            "#F8FAFC");
}

public sealed record BenchmarkVerticalChartGroup(
    string Label,
    IReadOnlyList<double?> Values);
