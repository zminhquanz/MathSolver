using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

/// <summary>
/// Minh họa trực quan ý nghĩa "chia đều" của trung bình cộng.
/// Các cột bên trái giữ lượng ban đầu; các cột bên phải có cùng chiều cao = TBC.
/// </summary>
public sealed class AverageDistributionDrawable : IDrawable
{
    public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();
    public double Average { get; set; }
    public bool Vietnamese { get; set; } = true;

    public Color AccentColor { get; set; } = Color.FromArgb("#A78BFA");
    public Color PrimaryTextColor { get; set; } = Colors.White;
    public Color SecondaryTextColor { get; set; } = Color.FromArgb("#CBD5E1");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        try
        {
            if (dirtyRect.Width <= 1f || dirtyRect.Height <= 1f)
            {
                return;
            }

            float padding = Math.Max(10f, dirtyRect.Width * 0.018f);
            float titleHeight = 34f;
            float footerHeight = 34f;
            float gap = Math.Max(18f, dirtyRect.Width * 0.035f);
            float halfWidth = (dirtyRect.Width - padding * 2f - gap) / 2f;
            float leftX = dirtyRect.Left + padding;
            float rightX = leftX + halfWidth + gap;
            float chartTop = dirtyRect.Top + titleHeight + 10f;
            float chartBottom = dirtyRect.Bottom - footerHeight;
            float chartHeight = Math.Max(70f, chartBottom - chartTop);

            DrawTitle(
                canvas,
                Vietnamese ? "Lượng ban đầu" : "Original amounts",
                leftX,
                dirtyRect.Top,
                halfWidth,
                titleHeight);

            DrawTitle(
                canvas,
                Vietnamese ? "Phần bằng nhau" : "Equal shares",
                rightX,
                dirtyRect.Top,
                halfWidth,
                titleHeight);

            double maxValue = Math.Max(
                1d,
                Math.Max(
                    Values.Count == 0 ? 1d : Values.Max(),
                    Average));

            DrawStacks(
                canvas,
                Values,
                leftX,
                chartTop,
                halfWidth,
                chartHeight,
                maxValue,
                showValues: true);

            int equalCount = Math.Max(2, Values.Count);
            DrawStacks(
                canvas,
                Enumerable.Repeat(Average, equalCount).ToArray(),
                rightX,
                chartTop,
                halfWidth,
                chartHeight,
                maxValue,
                showValues: false);

            DrawArrow(
                canvas,
                leftX + halfWidth + 4f,
                chartTop + chartHeight * 0.48f,
                gap - 8f);

            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = 13f;
            canvas.FontColor = SecondaryTextColor;
            canvas.DrawString(
                string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Vietnamese ? "Mỗi phần = {0:0.##}" : "Each share = {0:0.##}",
                    Average),
                rightX,
                chartBottom + 5f,
                halfWidth,
                footerHeight - 4f,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawTitle(
        ICanvas canvas,
        string text,
        float x,
        float y,
        float width,
        float height)
    {
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = 16f;
        canvas.FontColor = PrimaryTextColor;
        canvas.DrawString(
            text,
            x,
            y,
            width,
            height,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void DrawArrow(
        ICanvas canvas,
        float x,
        float centerY,
        float width)
    {
        if (width < 14f)
        {
            return;
        }

        float startX = x + 2f;
        float endX = x + width - 2f;

        canvas.StrokeColor = AccentColor;
        canvas.StrokeSize = 3f;
        canvas.DrawLine(startX, centerY, endX, centerY);
        canvas.DrawLine(endX - 10f, centerY - 9f, endX, centerY);
        canvas.DrawLine(endX - 10f, centerY + 9f, endX, centerY);
    }

    private void DrawStacks(
        ICanvas canvas,
        IReadOnlyList<double> values,
        float x,
        float top,
        float width,
        float height,
        double maxValue,
        bool showValues)
    {
        if (values.Count == 0)
        {
            return;
        }

        float spacing = Math.Max(5f, Math.Min(12f, width * 0.035f));
        float itemWidth = Math.Min(
            38f,
            Math.Max(
                14f,
                (width - spacing * (values.Count - 1)) / values.Count));

        float totalWidth = itemWidth * values.Count + spacing * (values.Count - 1);
        float startX = x + (width - totalWidth) / 2f;
        float footer = showValues ? 24f : 0f;
        float usableHeight = Math.Max(38f, height - footer);
        float safeMaxValue = (float)Math.Max(1d, maxValue);
        float unitHeight = MathF.Min(18f, usableHeight / safeMaxValue);
        float blockGap = Math.Min(4f, unitHeight * 0.23f);
        float blockHeight = Math.Max(3f, unitHeight - blockGap);
        float baseY = top + usableHeight;

        for (int index = 0; index < values.Count; index++)
        {
            double value = Math.Max(0d, values[index]);
            int whole = (int)Math.Floor(value);
            double fraction = value - whole;
            float itemX = startX + index * (itemWidth + spacing);

            canvas.FillColor = AccentColor;

            for (int unit = 0; unit < whole; unit++)
            {
                float y = baseY - (unit + 1) * unitHeight;
                canvas.FillRoundedRectangle(
                    itemX,
                    y,
                    itemWidth,
                    blockHeight,
                    3f);
            }

            if (fraction > 0.001d)
            {
                float y = baseY - (whole + 1) * unitHeight;
                float partialHeight = Math.Max(2f, (float)(blockHeight * fraction));
                canvas.Alpha = 0.52f;
                canvas.FillRoundedRectangle(
                    itemX,
                    y + blockHeight - partialHeight,
                    itemWidth,
                    partialHeight,
                    3f);
                canvas.Alpha = 1f;
            }

            if (showValues)
            {
                canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                canvas.FontSize = 13f;
                canvas.FontColor = SecondaryTextColor;
                canvas.DrawString(
                    value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture),
                    itemX - 3f,
                    baseY + 4f,
                    itemWidth + 6f,
                    20f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Top);
            }
        }
    }
}
