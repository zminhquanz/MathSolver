using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

/// <summary>
/// Minh họa đồng thời hai quan hệ có cùng hệ số k = 2:
/// y = 2x (tỉ lệ thuận) và y = 2/x (tỉ lệ nghịch).
/// Điểm đánh dấu được điều khiển bởi Slider ở ProportionFormulaView.
/// </summary>
public sealed class ProportionComparisonDrawable : IDrawable
{
    private const double XMinimum = 0d;
    private const double XMaximum = 3d;
    private const double YMinimum = 0d;
    private const double YMaximum = 5.5d;

    public double SelectedX { get; set; } =
        0.5d;

    public string DirectLegend { get; set; } =
        "Tỉ lệ thuận: y = 2x";

    public string InverseLegend { get; set; } =
        "Tỉ lệ nghịch: y = 2/x";

    private static Color DirectColor =>
        ThemeResource.GetColor(
            "PrimaryColor",
            "#7C3AED");

    private static Color InverseColor =>
        Color.FromArgb(
            Application.Current?.RequestedTheme == AppTheme.Dark
                ? "#F9A8D4"
                : "#DB2777");

    private static Color AxisColor =>
        ThemeResource.GetColor(
            "TextSecondaryColor",
            "#64748B");

    private static Color GridColor =>
        ThemeResource.GetColor(
            "DividerColor",
            "#E2E8F0");

    private static Color TextColor =>
        ThemeResource.GetColor(
            "TextPrimaryColor",
            "#1E293B");

    private static Color SurfaceColor =>
        ThemeResource.GetColor(
            "SurfaceColor",
            "#FFFFFF");

    public void Draw(
        ICanvas canvas,
        RectF dirtyRect)
    {
        if (dirtyRect.Width < 120f ||
            dirtyRect.Height < 120f)
        {
            return;
        }

        float leftPadding =
            dirtyRect.Width < 360f
                ? 40f
                : 48f;

        var plotRect =
            new RectF(
                dirtyRect.Left + leftPadding,
                dirtyRect.Top + 18f,
                MathF.Max(
                    1f,
                    dirtyRect.Width - leftPadding - 18f),
                MathF.Max(
                    1f,
                    dirtyRect.Height - 58f));

        canvas.SaveState();

        try
        {
            DrawGridAndAxes(
                canvas,
                plotRect);

            DrawCurves(
                canvas,
                plotRect);

            DrawSelectedPoints(
                canvas,
                plotRect);

            DrawLegend(
                canvas,
                plotRect);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private static void DrawGridAndAxes(
        ICanvas canvas,
        RectF plotRect)
    {
        canvas.Font =
            Microsoft.Maui.Graphics.Font.Default;

        canvas.FontSize =
            11f;

        canvas.FontColor =
            AxisColor;

        canvas.StrokeColor =
            GridColor;

        canvas.StrokeSize =
            1f;

        for (double x = 0.5d;
             x <= 2.5d + 0.001d;
             x += 0.5d)
        {
            float screenX =
                MapX(
                    x,
                    plotRect);

            canvas.DrawLine(
                screenX,
                plotRect.Top,
                screenX,
                plotRect.Bottom);

            canvas.DrawString(
                FormatTick(x),
                screenX - 24f,
                plotRect.Bottom + 5f,
                48f,
                22f,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }

        for (double y = 1d;
             y <= 5d;
             y += 1d)
        {
            float screenY =
                MapY(
                    y,
                    plotRect);

            canvas.DrawLine(
                plotRect.Left,
                screenY,
                plotRect.Right,
                screenY);

            canvas.DrawString(
                FormatTick(y),
                plotRect.Left - 38f,
                screenY - 10f,
                32f,
                20f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        canvas.StrokeColor =
            AxisColor;

        canvas.StrokeSize =
            1.6f;

        canvas.DrawLine(
            plotRect.Left,
            plotRect.Top,
            plotRect.Left,
            plotRect.Bottom);

        canvas.DrawLine(
            plotRect.Left,
            plotRect.Bottom,
            plotRect.Right,
            plotRect.Bottom);

        canvas.FontColor =
            TextColor;

        canvas.FontSize =
            13f;

        canvas.DrawString(
            "y",
            plotRect.Left - 35f,
            plotRect.Top - 5f,
            26f,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        canvas.DrawString(
            "x",
            plotRect.Right - 8f,
            plotRect.Bottom + 25f,
            22f,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawCurves(
        ICanvas canvas,
        RectF plotRect)
    {
        canvas.SaveState();

        try
        {
            canvas.ClipRectangle(
                plotRect);

            var directPath =
                new PathF();

            const int sampleCount = 180;

            for (int index = 0;
                 index <= sampleCount;
                 index++)
            {
                double x =
                    XMaximum *
                    index /
                    sampleCount;

                double y =
                    2d * x;

                float screenX =
                    MapX(
                        x,
                        plotRect);

                float screenY =
                    MapY(
                        y,
                        plotRect);

                if (index == 0)
                {
                    directPath.MoveTo(
                        screenX,
                        screenY);
                }
                else
                {
                    directPath.LineTo(
                        screenX,
                        screenY);
                }
            }

            canvas.StrokeColor =
                DirectColor;

            canvas.StrokeSize =
                3f;

            canvas.StrokeLineCap =
                LineCap.Round;

            canvas.StrokeLineJoin =
                LineJoin.Round;

            canvas.DrawPath(
                directPath);

            var inversePath =
                new PathF();

            for (int index = 0;
                 index <= sampleCount;
                 index++)
            {
                double x =
                    0.36d +
                    (XMaximum - 0.36d) *
                    index /
                    sampleCount;

                double y =
                    2d / x;

                float screenX =
                    MapX(
                        x,
                        plotRect);

                float screenY =
                    MapY(
                        y,
                        plotRect);

                if (index == 0)
                {
                    inversePath.MoveTo(
                        screenX,
                        screenY);
                }
                else
                {
                    inversePath.LineTo(
                        screenX,
                        screenY);
                }
            }

            canvas.StrokeColor =
                InverseColor;

            canvas.StrokeSize =
                3f;

            canvas.DrawPath(
                inversePath);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawSelectedPoints(
        ICanvas canvas,
        RectF plotRect)
    {
        double x =
            Math.Clamp(
                SelectedX,
                0.5d,
                2.5d);

        double directY =
            2d * x;

        double inverseY =
            2d / x;

        float screenX =
            MapX(
                x,
                plotRect);

        float directScreenY =
            MapY(
                directY,
                plotRect);

        float inverseScreenY =
            MapY(
                inverseY,
                plotRect);

        canvas.SaveState();

        try
        {
            canvas.ClipRectangle(
                plotRect);

            canvas.StrokeColor =
                AxisColor;

            canvas.StrokeSize =
                1.2f;

            canvas.StrokeDashPattern =
                [5f, 4f];

            canvas.DrawLine(
                screenX,
                plotRect.Bottom,
                screenX,
                MathF.Min(
                    directScreenY,
                    inverseScreenY));

            canvas.StrokeDashPattern =
                null;

            DrawPoint(
                canvas,
                screenX,
                directScreenY,
                DirectColor);

            DrawPoint(
                canvas,
                screenX,
                inverseScreenY,
                InverseColor);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawLegend(
        ICanvas canvas,
        RectF plotRect)
    {
        float legendWidth =
            MathF.Min(
                220f,
                plotRect.Width * 0.66f);

        float legendHeight =
            58f;

        float legendLeft =
            plotRect.Right -
            legendWidth -
            8f;

        float legendTop =
            plotRect.Top +
            8f;

        canvas.FillColor =
            SurfaceColor;

        canvas.FillRectangle(
            legendLeft,
            legendTop,
            legendWidth,
            legendHeight);

        canvas.StrokeColor =
            GridColor;

        canvas.StrokeSize =
            1f;

        canvas.DrawRectangle(
            legendLeft,
            legendTop,
            legendWidth,
            legendHeight);

        DrawLegendRow(
            canvas,
            legendLeft,
            legendTop + 7f,
            legendWidth,
            DirectColor,
            DirectLegend);

        DrawLegendRow(
            canvas,
            legendLeft,
            legendTop + 30f,
            legendWidth,
            InverseColor,
            InverseLegend);
    }

    private static void DrawLegendRow(
        ICanvas canvas,
        float left,
        float top,
        float width,
        Color color,
        string text)
    {
        canvas.StrokeColor =
            color;

        canvas.StrokeSize =
            3f;

        canvas.StrokeLineCap =
            LineCap.Round;

        canvas.DrawLine(
            left + 10f,
            top + 8f,
            left + 34f,
            top + 8f);

        canvas.Font =
            Microsoft.Maui.Graphics.Font.Default;

        canvas.FontColor =
            TextColor;

        canvas.FontSize =
            width < 180f
                ? 10f
                : 12f;

        canvas.DrawString(
            text,
            left + 42f,
            top,
            MathF.Max(
                1f,
                width - 48f),
            18f,
            HorizontalAlignment.Left,
            VerticalAlignment.Center);
    }

    private static void DrawPoint(
        ICanvas canvas,
        float x,
        float y,
        Color color)
    {
        canvas.FillColor =
            SurfaceColor;

        canvas.FillCircle(
            x,
            y,
            8f);

        canvas.FillColor =
            color;

        canvas.FillCircle(
            x,
            y,
            5.5f);
    }

    private static float MapX(
        double x,
        RectF plotRect)
    {
        return plotRect.Left +
               (float)((x - XMinimum) /
                       (XMaximum - XMinimum)) *
               plotRect.Width;
    }

    private static float MapY(
        double y,
        RectF plotRect)
    {
        return plotRect.Bottom -
               (float)((y - YMinimum) /
                       (YMaximum - YMinimum)) *
               plotRect.Height;
    }

    private static string FormatTick(
        double value)
    {
        return value.ToString(
            "0.#",
            CultureInfo.CurrentCulture);
    }
}
