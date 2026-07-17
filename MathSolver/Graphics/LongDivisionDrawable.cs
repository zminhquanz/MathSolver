using MathSolver.Models;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public sealed class LongDivisionDrawable : IDrawable
{
    private const float DefaultDigitWidth = 30f;
    private const float DefaultRowHeight = 38f;
    private const float DefaultFontSize = 27f;

    public LongDivisionResult? Result { get; set; }

    public void Draw(
        ICanvas canvas,
        RectF dirtyRect)
    {
        canvas.SaveState();

        try
        {
            canvas.FillColor = Colors.White;
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
        float scale =
            CalculateScale(bounds.Width);

        float digitWidth =
            DefaultDigitWidth * scale;

        float rowHeight =
            DefaultRowHeight * scale;

        float fontSize =
            DefaultFontSize * scale;

        float padding =
            18f * scale;

        string dividendText =
            result.NormalizedDividendText;

        string divisorText =
            result.NormalizedDivisorText;

        string quotientText =
            result.QuotientText;

        int dividendColumnCount =
            CountVisualColumns(dividendText);

        int rightColumnCount =
            Math.Max(
                CountVisualColumns(divisorText),
                CountVisualColumns(quotientText));

        float dividerGap =
            12f * scale;

        float contentWidth =
            dividendColumnCount * digitWidth +
            dividerGap +
            rightColumnCount * digitWidth +
            padding * 2;

        float originX =
            Math.Max(
                padding,
                (bounds.Width - contentWidth) / 2f);

        float originY =
            padding;

        float dividerX =
            originX +
            dividendColumnCount * digitWidth +
            dividerGap;

        ConfigureCanvas(
            canvas,
            fontSize,
            scale);

        DrawTopArea(
            canvas,
            result,
            originX,
            originY,
            dividerX,
            digitWidth,
            rowHeight,
            scale);

        DrawSteps(
            canvas,
            result,
            originX,
            originY,
            digitWidth,
            rowHeight,
            scale);
    }

    private static void DrawTopArea(
        ICanvas canvas,
        LongDivisionResult result,
        float originX,
        float originY,
        float dividerX,
        float digitWidth,
        float rowHeight,
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
            dividerX + 12f * scale;

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
            2.5f * scale;

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
        float scale)
    {
        if (result.Steps.Count == 0)
        {
            return;
        }

        float currentY =
            originY +
            rowHeight +
            12f * scale;

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
                partialText.Length + 1;

            int productStartColumn =
                step.EndColumn -
                productText.Length + 1;

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
                5f * scale;

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
                    remainderText.Length + 1;

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
        for (int index = 0;
             index < text.Length;
             index++)
        {
            canvas.DrawString(
                text[index].ToString(),
                originX +
                (startColumn + index) *
                digitWidth,
                y,
                digitWidth,
                rowHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
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

    private static void ConfigureCanvas(
        ICanvas canvas,
        float fontSize,
        float scale)
    {
        canvas.Font =
            Microsoft.Maui.Graphics.Font.Default;

        canvas.FontSize =
            fontSize;

        canvas.FontColor =
            Color.FromArgb("#172033");

        canvas.StrokeColor =
            Color.FromArgb("#172033");

        canvas.StrokeSize =
            2.2f * scale;
    }

    private static void DrawEmptyMessage(
        ICanvas canvas,
        RectF bounds)
    {
        canvas.FontColor =
            Color.FromArgb("#64748B");

        canvas.FontSize = 14;

        canvas.DrawString(
            "Chưa có phép chia để hiển thị.",
            bounds,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static float CalculateScale(
        float width)
    {
        if (width < 420)
        {
            return 0.72f;
        }

        if (width < 700)
        {
            return 0.86f;
        }

        return 1f;
    }
}