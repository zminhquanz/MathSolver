using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

public sealed class MotionAverageSpeedDrawable : IDrawable
{
    public double DistanceMeters { get; set; } = 40d;

    public int TimeSeconds { get; set; } = 1;

    public double SpeedMetersPerSecond { get; set; } = 40d;

    private static Color PrimaryColor =>
        ThemeResource.GetColor(
            "PrimaryColor",
            "#F97316");

    private static Color PrimaryBorderColor =>
        ThemeResource.GetColor(
            "PrimaryBorderColor",
            "#FDBA74");

    private static Color SurfaceAltColor =>
        ThemeResource.GetColor(
            "SurfaceAltColor",
            Application.Current?.RequestedTheme == AppTheme.Dark
                ? "#172033"
                : "#F8FAFC");

    private static Color DividerColor =>
        ThemeResource.GetColor(
            "DividerColor",
            "#E2E8F0");

    private static Color TextPrimaryColor =>
        ThemeResource.GetColor(
            "WallpaperTextPrimaryColor",
            "#0F172A");

    private static Color TextSecondaryColor =>
        ThemeResource.GetColor(
            "WallpaperTextSecondaryColor",
            "#64748B");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 140f || dirtyRect.Height < 140f)
        {
            return;
        }

        int timeSeconds = Math.Max(1, TimeSeconds);
        double distanceMeters = Math.Max(1d, DistanceMeters);
        double speed = Math.Max(0.1d, SpeedMetersPerSecond);

        float titleHeight = 56f;
        float contentTop = dirtyRect.Top + titleHeight;
        float left = dirtyRect.Left + 26f;
        float right = dirtyRect.Right - 22f;
        float centerY = contentTop + (dirtyRect.Height - titleHeight) * 0.48f;
        float totalWidth = MathF.Min(right - left, dirtyRect.Width * 0.78f);
        float trackHeight = 18f;
        float totalLeft = left + MathF.Max(0f, (right - left - totalWidth) / 2f);
        float totalRight = totalLeft + totalWidth;
        float totalTop = centerY - trackHeight / 2f;
        float segmentWidth = totalWidth / timeSeconds;

        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontColor = TextSecondaryColor;
        canvas.FontSize = dirtyRect.Width < 360f ? 16f : 18f;
        canvas.DrawString(
            LocalizationService.TranslateKey(
                "Formula.Motion.Graph.VisualTitle"),
            dirtyRect.Left,
            dirtyRect.Top,
            dirtyRect.Width,
            26f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
        canvas.DrawString(
            LocalizationService.TranslateKey(
                "Formula.Motion.Graph.VisualSubtitle"),
            dirtyRect.Left,
            dirtyRect.Top + 22f,
            dirtyRect.Width,
            24f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        // Track nền tổng quãng đường
        canvas.FillColor = SurfaceAltColor;
        canvas.FillRoundedRectangle(totalLeft, totalTop, totalWidth, trackHeight, 9f);

        // Nổi bật đoạn đi được trong 1 giây
        canvas.FillColor = PrimaryColor.WithAlpha(0.28f);
        canvas.FillRoundedRectangle(totalLeft, totalTop, segmentWidth, trackHeight, 9f);

        canvas.StrokeColor = DividerColor;
        canvas.StrokeSize = 1f;
        canvas.DrawRoundedRectangle(totalLeft, totalTop, totalWidth, trackHeight, 9f);

        // Vạch chia theo từng giây
        canvas.StrokeColor = PrimaryBorderColor;
        canvas.StrokeSize = 1.4f;
        for (int index = 1; index < timeSeconds; index++)
        {
            float dividerX = totalLeft + segmentWidth * index;
            canvas.DrawLine(dividerX, totalTop - 8f, dividerX, totalTop + trackHeight + 8f);
        }

        // Điểm kết thúc 1 giây đầu tiên
        float firstSecondX = totalLeft + segmentWidth;
        canvas.FillColor = PrimaryColor;
        canvas.FillCircle(firstSecondX, centerY, 7f);
        canvas.FillColor = Colors.White;
        canvas.FillCircle(firstSecondX, centerY, 4.2f);

        // Ngoặc trên: mỗi 1 giây
        DrawBracket(canvas, totalLeft, firstSecondX, totalTop - 32f, PrimaryBorderColor);
        canvas.FontColor = TextSecondaryColor;
        canvas.FontSize = 16f;
        canvas.DrawString(
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey(
                    "Formula.Motion.Graph.FirstSegment"),
                speed.ToString("0.##", CultureInfo.CurrentCulture)),
            totalLeft,
            totalTop - 60f,
            Math.Max(10f, segmentWidth),
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        // Ngoặc dưới: tổng d
        DrawBracket(canvas, totalLeft, totalRight, totalTop + trackHeight + 32f, TextSecondaryColor, dashed: true);
        canvas.DrawString(
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey(
                    "Formula.Motion.Graph.TotalDistance"),
                distanceMeters.ToString("0", CultureInfo.CurrentCulture)),
            totalLeft,
            totalTop + trackHeight + 42f,
            totalWidth,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Top);

        // Mô tả thời gian
        canvas.FontColor = TextPrimaryColor;
        canvas.FontSize = 15f;
        canvas.DrawString(
            string.Format(
                CultureInfo.CurrentCulture,
                LocalizationService.TranslateKey(
                    "Formula.Motion.Graph.TimeSegments"),
                timeSeconds,
                timeSeconds),
            dirtyRect.Left + 8f,
            totalTop + trackHeight + 74f,
            dirtyRect.Width - 16f,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Top);
    }

    private static void DrawBracket(
        ICanvas canvas,
        float left,
        float right,
        float y,
        Color color,
        bool dashed = false)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = 1.8f;
        canvas.StrokeDashPattern = dashed ? new float[] { 5f, 4f } : null;
        canvas.DrawLine(left, y, right, y);
        canvas.DrawLine(left, y - 10f, left, y + 10f);
        canvas.DrawLine(right, y - 10f, right, y + 10f);
        canvas.StrokeDashPattern = null;
    }
}
