using MathSolver.Services;
using MathSolver.Models;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public sealed class LongDivisionDrawable : IDrawable
{
    private const float DefaultDigitWidth = 30f;
    private const float DefaultRowHeight = 38f;
    private const float DefaultFontSize = 27f;

    // Giới hạn nhỏ nhất chỉ dùng khi phép chia có nhiều chữ số.
    // Kích thước chữ thực tế còn được điều chỉnh theo digitWidth.
    private const float MinimumContentScale = 0.34f;
    private const float MinimumFontSize = 8f;

    // Giới hạn chiều dọc để phép chia dài không tạo ra một vùng
    // GraphicsView quá cao và có nhiều khoảng trống ở phía dưới.
    private const float MinimumRowHeight = 13f;
    private const float MinimumPreferredHeight = 180f;

    public LongDivisionResult? Result { get; set; }

    /// <summary>
    /// Trả về chiều cao phù hợp với đúng scale mà Drawable sẽ sử dụng.
    /// Nhờ vậy GraphicsView không còn tính chiều cao theo rowHeight cố định
    /// trong khi phần vẽ đã thu nhỏ theo chiều ngang.
    /// </summary>
    public double GetPreferredHeight(
        double availableWidth)
    {
        if (Result is null)
        {
            return MinimumPreferredHeight;
        }

        float safeWidth =
            (float)Math.Max(
                280d,
                availableWidth);

        var metrics =
            CalculateLayoutMetrics(
                safeWidth,
                Result);

        return Math.Ceiling(
            metrics.PreferredHeight);
    }

    public void Draw(
        ICanvas canvas,
        RectF dirtyRect)
    {
        canvas.SaveState();

        try
        {
            canvas.FillColor =
                ThemeResource.GetColor(
                    "SurfaceColor",
                    "#FFFFFF");
            canvas.FillRectangle(dirtyRect);

            if (Result is null)
            {
                DrawEmptyMessage(
                    canvas,
                    dirtyRect);

                return;
            }

            DrawDivision(
                canvas,
                dirtyRect,
                Result);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private static void DrawDivision(
        ICanvas canvas,
        RectF bounds,
        LongDivisionResult result)
    {
        var metrics =
            CalculateLayoutMetrics(
                bounds.Width,
                result);

        float drawingWidth =
            metrics.TotalColumnCount *
            metrics.DigitWidth +
            metrics.DividerGap +
            metrics.RightTextInset;

        float originX =
            Math.Max(
                metrics.Padding,
                (bounds.Width -
                 drawingWidth) /
                2f);

        // Nếu bên ngoài vẫn đang giữ HeightRequest cũ quá lớn,
        // căn giữa nội dung theo chiều dọc thay vì dồn toàn bộ lên trên
        // và để một khoảng trắng rất lớn ở phía dưới.
        float originY =
            Math.Max(
                metrics.Padding,
                (bounds.Height -
                 metrics.PreferredHeight) /
                2f +
                metrics.Padding);

        float dividerX =
            originX +
            metrics.DividendColumnCount *
            metrics.DigitWidth +
            metrics.DividerGap;

        ConfigureCanvas(
            canvas,
            metrics.FontSize,
            metrics.Scale);

        DrawTopArea(
            canvas,
            result,
            originX,
            originY,
            dividerX,
            metrics.DigitWidth,
            metrics.RowHeight,
            metrics.RightTextInset,
            metrics.Scale);

        DrawSteps(
            canvas,
            result,
            originX,
            originY,
            metrics.DigitWidth,
            metrics.RowHeight,
            metrics.StepGap,
            metrics.Scale);
    }

    private static void DrawTopArea(
        ICanvas canvas,
        LongDivisionResult result,
        float originX,
        float originY,
        float dividerX,
        float digitWidth,
        float rowHeight,
        float rightTextInset,
        float scale)
    {
        DrawTextByColumns(
            canvas,
            result.NormalizedDividendText,
            originX,
            originY,
            digitWidth,
            rowHeight);

        float rightX =
            dividerX +
            rightTextInset;

        DrawTextByColumns(
            canvas,
            result.NormalizedDivisorText,
            rightX,
            originY,
            digitWidth,
            rowHeight);

        float horizontalLineY =
            originY + rowHeight;

        float rightWidth =
            Math.Max(
                CountVisualColumns(
                    result.NormalizedDivisorText),
                CountVisualColumns(
                    result.QuotientText)) *
            digitWidth;

        canvas.StrokeSize =
            Math.Max(
                1.2f,
                2.5f * scale);

        canvas.DrawLine(
            dividerX,
            horizontalLineY,
            rightX + rightWidth,
            horizontalLineY);

        canvas.DrawLine(
            dividerX,
            originY,
            dividerX,
            horizontalLineY +
            rowHeight * 1.2f);

        DrawTextByColumns(
            canvas,
            result.QuotientText,
            rightX,
            horizontalLineY + 3f * scale,
            digitWidth,
            rowHeight);
    }

    private static void DrawSteps(
        ICanvas canvas,
        LongDivisionResult result,
        float originX,
        float originY,
        float digitWidth,
        float rowHeight,
        float stepGap,
        float scale)
    {
        if (result.Steps.Count == 0)
        {
            return;
        }

        float currentY =
            originY +
            rowHeight +
            stepGap;

        for (int index = 0;
             index < result.Steps.Count;
             index++)
        {
            DivisionStep step =
                result.Steps[index];

            string partialText =
                step.PartialDividendText;

            string productText =
                step.ProductText;

            int partialStartColumn =
                step.EndColumn -
                CountVisualColumns(
                    partialText) + 1;

            int productStartColumn =
                step.EndColumn -
                CountVisualColumns(
                    productText) + 1;

            if (index > 0)
            {
                DrawDigitsAtColumn(
                    canvas,
                    partialText,
                    originX,
                    currentY,
                    partialStartColumn,
                    digitWidth,
                    rowHeight);

                currentY += rowHeight;
            }

            DrawDigitsAtColumn(
                canvas,
                productText,
                originX,
                currentY,
                productStartColumn,
                digitWidth,
                rowHeight);

            float lineStartX =
                originX +
                Math.Min(
                    partialStartColumn,
                    productStartColumn) *
                digitWidth;

            float lineEndX =
                originX +
                (step.EndColumn + 1) *
                digitWidth;

            float lineY =
                currentY +
                rowHeight -
                Math.Max(
                    2f,
                    rowHeight * 0.14f);

            canvas.DrawLine(
                lineStartX,
                lineY,
                lineEndX,
                lineY);

            currentY += rowHeight;

            bool isLastStep =
                index ==
                result.Steps.Count - 1;

            if (isLastStep)
            {
                string remainderText =
                    step.RemainderText;

                int remainderStartColumn =
                    step.EndColumn -
                    CountVisualColumns(
                        remainderText) + 1;

                DrawDigitsAtColumn(
                    canvas,
                    remainderText,
                    originX,
                    currentY,
                    remainderStartColumn,
                    digitWidth,
                    rowHeight);
            }
        }
    }

    private static void DrawTextByColumns(
        ICanvas canvas,
        string text,
        float x,
        float y,
        float digitWidth,
        float rowHeight)
    {
        int visualColumn = 0;

        foreach (char character in text)
        {
            if (character is '.' or ',')
            {
                DrawDecimalSeparator(
                    canvas,
                    x +
                    visualColumn * digitWidth -
                    digitWidth * 0.14f,
                    y,
                    digitWidth,
                    rowHeight);

                continue;
            }

            canvas.DrawString(
                character.ToString(),
                x + visualColumn * digitWidth,
                y,
                digitWidth,
                rowHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            visualColumn++;
        }
    }

    private static void DrawDecimalSeparator(
        ICanvas canvas,
        float x,
        float y,
        float digitWidth,
        float rowHeight)
    {
        canvas.DrawString(
            ",",
            x,
            y,
            digitWidth * 0.5f,
            rowHeight,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static void DrawDigitsAtColumn(
        ICanvas canvas,
        string text,
        float originX,
        float y,
        int startColumn,
        float digitWidth,
        float rowHeight)
    {
        int visualColumn =
            0;

        foreach (char character
                 in text)
        {
            if (character is '.' or ',')
            {
                DrawDecimalSeparator(
                    canvas,
                    originX +
                    (startColumn +
                     visualColumn) *
                    digitWidth -
                    digitWidth * 0.14f,
                    y,
                    digitWidth,
                    rowHeight);

                continue;
            }

            canvas.DrawString(
                character.ToString(),
                originX +
                (startColumn +
                 visualColumn) *
                digitWidth,
                y,
                digitWidth,
                rowHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            visualColumn++;
        }
    }

    private static int CountVisualColumns(
        string text)
    {
        return text.Count(
            character =>
                character != '.' &&
                character != ',');
    }

    private static (
        float Scale,
        float Padding,
        float DividerGap,
        float RightTextInset,
        float DigitWidth,
        float FontSize,
        float RowHeight,
        float StepGap,
        float PreferredHeight,
        int DividendColumnCount,
        int RightColumnCount,
        int TotalColumnCount)
        CalculateLayoutMetrics(
            float availableWidth,
            LongDivisionResult result)
    {
        string dividendText =
            result.NormalizedDividendText;

        string divisorText =
            result.NormalizedDivisorText;

        string quotientText =
            result.QuotientText;

        int dividendColumnCount =
            CountVisualColumns(
                dividendText);

        int rightColumnCount =
            Math.Max(
                CountVisualColumns(
                    divisorText),
                CountVisualColumns(
                    quotientText));

        int totalColumnCount =
            Math.Max(
                1,
                dividendColumnCount +
                rightColumnCount);

        float scale =
            CalculateScale(
                availableWidth,
                dividendColumnCount,
                rightColumnCount);

        float padding =
            Math.Max(
                6f,
                18f * scale);

        float dividerGap =
            Math.Max(
                5f,
                12f * scale);

        float rightTextInset =
            Math.Max(
                4f,
                8f * scale);

        float availableDigitArea =
            Math.Max(
                1f,
                availableWidth -
                padding * 2f -
                dividerGap -
                rightTextInset);

        float maximumDigitWidth =
            availableDigitArea /
            totalColumnCount;

        float digitWidth =
            Math.Clamp(
                Math.Min(
                    DefaultDigitWidth * scale,
                    maximumDigitWidth),
                6f,
                DefaultDigitWidth);

        float horizontalFontSize =
            Math.Clamp(
                Math.Min(
                    DefaultFontSize * scale,
                    digitWidth * 0.92f),
                MinimumFontSize,
                DefaultFontSize);

        int stepRowCount =
            CalculateVisibleStepRowCount(
                result);

        int totalRowUnits =
            Math.Max(
                1,
                1 + stepRowCount);

        float stepGap =
            Math.Max(
                4f,
                10f * scale);

        float maximumPreferredHeight =
            availableWidth switch
            {
                < 420f => 360f,
                < 700f => 430f,
                _ => 520f
            };

        float availableRowsHeight =
            Math.Max(
                MinimumRowHeight *
                totalRowUnits,
                maximumPreferredHeight -
                padding * 2f -
                stepGap);

        float heightLimitedRowHeight =
            availableRowsHeight /
            totalRowUnits;

        float naturalRowHeight =
            Math.Max(
                horizontalFontSize * 1.38f,
                DefaultRowHeight * scale);

        // Với nhiều bước chia, thu hẹp khoảng cách dọc nhưng vẫn giữ
        // chiều cao tối thiểu đủ để đọc số và đường gạch.
        float rowHeight =
            Math.Max(
                MinimumRowHeight,
                Math.Min(
                    naturalRowHeight,
                    heightLimitedRowHeight));

        float fontSize =
            Math.Clamp(
                Math.Min(
                    horizontalFontSize,
                    rowHeight * 0.72f),
                MinimumFontSize,
                DefaultFontSize);

        float preferredHeight =
            padding * 2f +
            stepGap +
            totalRowUnits *
            rowHeight;

        preferredHeight =
            Math.Max(
                MinimumPreferredHeight,
                preferredHeight);

        return (
            scale,
            padding,
            dividerGap,
            rightTextInset,
            digitWidth,
            fontSize,
            rowHeight,
            stepGap,
            preferredHeight,
            dividendColumnCount,
            rightColumnCount,
            totalColumnCount);
    }

    private static int CalculateVisibleStepRowCount(
        LongDivisionResult result)
    {
        if (result.Steps.Count == 0)
        {
            return 0;
        }

        // Mỗi bước có một dòng tích cần trừ.
        int rowCount =
            result.Steps.Count;

        // Từ bước thứ hai có thêm dòng số sau khi hạ xuống.
        rowCount +=
            Math.Max(
                0,
                result.Steps.Count - 1);

        // Dòng số dư cuối cùng.
        rowCount++;

        return rowCount;
    }

    private static void ConfigureCanvas(
        ICanvas canvas,
        float fontSize,
        float scale)
    {
        canvas.Font =
            Microsoft.Maui.Graphics.Font.Default;

        canvas.FontSize =
            fontSize;

        Color primaryText =
            ThemeResource.GetColor(
                "TextPrimaryColor",
                "#172033");

        canvas.FontColor =
            primaryText;

        canvas.StrokeColor =
            primaryText;

        canvas.StrokeSize =
            Math.Max(
                1.1f,
                2.2f * scale);
    }

    private static void DrawEmptyMessage(
        ICanvas canvas,
        RectF bounds)
    {
        canvas.FontColor =
            ThemeResource.GetColor(
                "TextSecondaryColor",
                "#64748B");

        canvas.FontSize = 14;

        canvas.DrawString(
            LocalizationService.Translate(
                "Chưa có phép chia để hiển thị."),
            bounds,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static float CalculateScale(
        float width,
        int dividendColumnCount,
        int rightColumnCount)
    {
        float viewportScale =
            width switch
            {
                < 420f => 0.72f,
                < 700f => 0.86f,
                _ => 1f
            };

        int totalColumnCount =
            Math.Max(
                1,
                dividendColumnCount +
                rightColumnCount);

        // Kích thước ước tính ở scale 1, gồm:
        // padding hai bên + khoảng tới vạch chia + khoảng từ vạch
        // chia tới số chia/thương.
        float estimatedWidthAtScaleOne =
            totalColumnCount *
            DefaultDigitWidth +
            18f * 2f +
            12f +
            8f;

        float fitScale =
            width /
            Math.Max(
                1f,
                estimatedWidthAtScaleOne);

        return Math.Clamp(
            Math.Min(
                viewportScale,
                fitScale),
            MinimumContentScale,
            1f);
    }
}