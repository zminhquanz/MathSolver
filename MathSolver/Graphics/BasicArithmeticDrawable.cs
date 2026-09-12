using MathSolver.Models;
using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Numerics;

namespace MathSolver.Graphics;

/// <summary>
/// Vẽ phép cộng, trừ và nhân theo dạng đặt tính dọc, tách biệt hoàn toàn
/// với LongDivisionDrawable. Các chữ số được đặt trên lưới cột cố định để
/// dễ quan sát giá trị hàng; phép nhân nhiều chữ số hiển thị các tích riêng.
/// </summary>
public sealed class BasicArithmeticDrawable : IDrawable
{
    private const float DefaultDigitWidth = 30f;
    private const float DefaultRowHeight = 38f;
    private const float DefaultFontSize = 27f;
    private const float DefaultPadding = 18f;
    private const float OperatorReserveWidth = 32f;
    private const float MinimumPreferredHeight = 150f;

    private string _firstText = string.Empty;
    private string _secondText = string.Empty;
    private string _resultText = string.Empty;
    private readonly List<PartialProductRow> _partialProducts = new();

    public ArithmeticOperation Operation { get; private set; } = ArithmeticOperation.Add;

    public bool HasContent =>
        !string.IsNullOrEmpty(_firstText) &&
        !string.IsNullOrEmpty(_secondText) &&
        !string.IsNullOrEmpty(_resultText);

    public void Clear()
    {
        _firstText = string.Empty;
        _secondText = string.Empty;
        _resultText = string.Empty;
        _partialProducts.Clear();
    }

    public void SetInteger(
        BigInteger first,
        BigInteger second,
        BigInteger result,
        ArithmeticOperation operation)
    {
        EnsureSupportedOperation(operation);

        Operation = operation;
        _firstText = FormatInteger(first);
        _secondText = FormatInteger(second);
        _resultText = FormatInteger(result);

        _partialProducts.Clear();

        if (operation == ArithmeticOperation.Multiply)
        {
            BuildPartialProducts(
                BigInteger.Abs(first),
                BigInteger.Abs(second));
        }
    }

    public void SetDecimal(
        decimal first,
        decimal second,
        ArithmeticOperation operation)
    {
        EnsureSupportedOperation(operation);

        Operation = operation;
        _partialProducts.Clear();

        DecimalParts firstParts = GetDecimalParts(first);
        DecimalParts secondParts = GetDecimalParts(second);

        if (operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract)
        {
            int commonScale =
                Math.Max(
                    firstParts.Scale,
                    secondParts.Scale);

            BigInteger firstAligned =
                ScaleTo(
                    firstParts.UnscaledValue,
                    firstParts.Scale,
                    commonScale);

            BigInteger secondAligned =
                ScaleTo(
                    secondParts.UnscaledValue,
                    secondParts.Scale,
                    commonScale);

            BigInteger resultAligned =
                operation == ArithmeticOperation.Add
                    ? firstAligned + secondAligned
                    : firstAligned - secondAligned;

            _firstText =
                FormatScaledInteger(
                    firstAligned,
                    commonScale);

            _secondText =
                FormatScaledInteger(
                    secondAligned,
                    commonScale);

            _resultText =
                FormatScaledInteger(
                    resultAligned,
                    commonScale);

            return;
        }

        _firstText =
            FormatScaledInteger(
                firstParts.UnscaledValue,
                firstParts.Scale);

        _secondText =
            FormatScaledInteger(
                secondParts.UnscaledValue,
                secondParts.Scale);

        BigInteger unscaledProduct =
            firstParts.UnscaledValue *
            secondParts.UnscaledValue;

        int productScale =
            firstParts.Scale +
            secondParts.Scale;

        _resultText =
            FormatScaledInteger(
                unscaledProduct,
                productScale);

        BuildPartialProducts(
            BigInteger.Abs(firstParts.UnscaledValue),
            BigInteger.Abs(secondParts.UnscaledValue));
    }

    public double GetPreferredWidth()
    {
        if (!HasContent)
        {
            return 320d;
        }

        int numberColumns =
            CalculateNumberColumnCount();

        return Math.Ceiling(
            DefaultPadding * 2f +
            OperatorReserveWidth +
            numberColumns *
            DefaultDigitWidth);
    }

    public double GetPreferredHeight(double availableWidth)
    {
        if (!HasContent)
        {
            return MinimumPreferredHeight;
        }

        float width =
            (float)Math.Max(
                280d,
                availableWidth);

        LayoutMetrics metrics =
            CalculateLayoutMetrics(width);

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
                    "SurfaceAltColor",
                    "#F7F8FA");

            canvas.FillRectangle(dirtyRect);

            if (!HasContent)
            {
                DrawEmptyMessage(
                    canvas,
                    dirtyRect);
                return;
            }

            DrawArithmetic(
                canvas,
                dirtyRect);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawArithmetic(
        ICanvas canvas,
        RectF bounds)
    {
        LayoutMetrics metrics =
            CalculateLayoutMetrics(
                bounds.Width);

        float contentWidth =
            metrics.OperatorReserveWidth +
            metrics.NumberColumnCount *
            metrics.DigitWidth;

        float originX =
            Math.Max(
                metrics.Padding,
                (bounds.Width - contentWidth) /
                2f);

        float originY =
            Math.Max(
                metrics.Padding,
                (bounds.Height -
                 metrics.PreferredHeight) /
                2f +
                metrics.Padding);

        float numbersX =
            originX +
            metrics.OperatorReserveWidth;

        ConfigureCanvas(
            canvas,
            metrics.FontSize,
            metrics.Scale);

        float currentY = originY;

        DrawRightAlignedText(
            canvas,
            _firstText,
            numbersX,
            currentY,
            metrics.NumberColumnCount,
            metrics.DigitWidth,
            metrics.RowHeight);

        currentY +=
            metrics.RowHeight;

        DrawOperatorRow(
            canvas,
            GetOperationSymbol(Operation),
            _secondText,
            originX,
            numbersX,
            currentY,
            metrics);

        currentY +=
            metrics.RowHeight;

        DrawResultWidthLine(
            canvas,
            numbersX,
            currentY,
            metrics);

        if (Operation != ArithmeticOperation.Multiply ||
            _partialProducts.Count <= 1)
        {
            DrawRightAlignedText(
                canvas,
                _resultText,
                numbersX,
                currentY,
                metrics.NumberColumnCount,
                metrics.DigitWidth,
                metrics.RowHeight);

            return;
        }

        for (int index = 0;
             index < _partialProducts.Count;
             index++)
        {
            PartialProductRow row =
                _partialProducts[index];

            DrawShiftedRightAlignedText(
                canvas,
                row.Text,
                row.ShiftColumns,
                numbersX,
                currentY,
                metrics.NumberColumnCount,
                metrics.DigitWidth,
                metrics.RowHeight);

            currentY +=
                metrics.RowHeight;
        }

        DrawResultWidthLine(
            canvas,
            numbersX,
            currentY,
            metrics);

        DrawRightAlignedText(
            canvas,
            _resultText,
            numbersX,
            currentY,
            metrics.NumberColumnCount,
            metrics.DigitWidth,
            metrics.RowHeight);
    }

    private static void DrawOperatorRow(
        ICanvas canvas,
        string operationSymbol,
        string operandText,
        float originX,
        float numbersX,
        float y,
        LayoutMetrics metrics)
    {
        // Dấu + / − / × dùng một cột riêng ở bên trái toàn bộ khối số.
        // Không bám theo độ dài của toán hạng thứ hai: với 100 và 10,
        // dấu vẫn nằm ngoài mép trái của cả hai số như cách đặt tính
        // truyền thống và đúng vị trí được đánh dấu trong ảnh tham chiếu.
        float operatorX =
            originX;

        float operatorWidth =
            metrics.OperatorReserveWidth;

        // Dấu phép toán nằm đúng giữa hai hàng cơ số a và b về chiều dọc.
        // y là mép trên của hàng b, đồng thời cũng là đường giữa giữa
        // tâm hàng a và tâm hàng b; vì DrawString nhận một ô cao RowHeight,
        // ta dịch mép trên của ô dấu lên nửa RowHeight.
        float operatorY =
            y -
            metrics.RowHeight *
            0.5f;

        canvas.DrawString(
            operationSymbol,
            operatorX,
            operatorY,
            operatorWidth,
            metrics.RowHeight,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);

        DrawRightAlignedText(
            canvas,
            operandText,
            numbersX,
            y,
            metrics.NumberColumnCount,
            metrics.DigitWidth,
            metrics.RowHeight);
    }

    private void DrawResultWidthLine(
        ICanvas canvas,
        float numbersX,
        float currentY,
        LayoutMetrics metrics)
    {
        int resultColumns =
            Math.Clamp(
                CountVisualColumns(
                    _resultText),
                1,
                metrics.NumberColumnCount);

        int startColumn =
            metrics.NumberColumnCount -
            resultColumns;

        float lineY =
            currentY +
            metrics.LineInset;

        float lineStartX =
            numbersX +
            startColumn *
            metrics.DigitWidth;

        float lineEndX =
            numbersX +
            metrics.NumberColumnCount *
            metrics.DigitWidth;

        canvas.DrawLine(
            lineStartX,
            lineY,
            lineEndX,
            lineY);
    }

    private static void DrawRightAlignedText(
        ICanvas canvas,
        string text,
        float numbersX,
        float y,
        int numberColumnCount,
        float digitWidth,
        float rowHeight)
    {
        int textColumns =
            CountVisualColumns(
                text);

        int startColumn =
            Math.Max(
                0,
                numberColumnCount -
                textColumns);

        DrawTextAtColumn(
            canvas,
            text,
            numbersX,
            y,
            startColumn,
            digitWidth,
            rowHeight);
    }

    private static void DrawShiftedRightAlignedText(
        ICanvas canvas,
        string text,
        int shiftColumns,
        float numbersX,
        float y,
        int numberColumnCount,
        float digitWidth,
        float rowHeight)
    {
        int textColumns =
            CountVisualColumns(
                text);

        int endColumnExclusive =
            Math.Max(
                textColumns,
                numberColumnCount -
                shiftColumns);

        int startColumn =
            Math.Max(
                0,
                endColumnExclusive -
                textColumns);

        DrawTextAtColumn(
            canvas,
            text,
            numbersX,
            y,
            startColumn,
            digitWidth,
            rowHeight);
    }

    private static void DrawTextAtColumn(
        ICanvas canvas,
        string text,
        float originX,
        float y,
        int startColumn,
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
                    character,
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

    private static void DrawDecimalSeparator(
        ICanvas canvas,
        char separator,
        float x,
        float y,
        float digitWidth,
        float rowHeight)
    {
        canvas.DrawString(
            separator.ToString(),
            x,
            y,
            digitWidth * 0.5f,
            rowHeight,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private LayoutMetrics CalculateLayoutMetrics(
        float availableWidth)
    {
        int numberColumnCount =
            CalculateNumberColumnCount();

        float idealWidth =
            OperatorReserveWidth +
            numberColumnCount *
            DefaultDigitWidth +
            DefaultPadding * 2f;

        float scale =
            idealWidth <= availableWidth
                ? 1f
                : Math.Max(
                    0.32f,
                    availableWidth /
                    idealWidth);

        float padding =
            Math.Max(
                7f,
                DefaultPadding *
                scale);

        float operatorReserveWidth =
            Math.Max(
                12f,
                OperatorReserveWidth *
                scale);

        float digitWidth =
            Math.Max(
                9f,
                DefaultDigitWidth *
                scale);

        float fontSize =
            Math.Max(
                8f,
                DefaultFontSize *
                scale);

        float rowHeight =
            Math.Max(
                18f,
                DefaultRowHeight *
                scale);

        float lineInset =
            Math.Max(
                1.5f,
                2.5f *
                scale);

        int contentRowCount =
            Operation == ArithmeticOperation.Multiply &&
            _partialProducts.Count > 1
                ? 3 + _partialProducts.Count
                : 3;

        float preferredHeight =
            padding * 2f +
            contentRowCount *
            rowHeight;

        return new LayoutMetrics(
            Scale: scale,
            Padding: padding,
            OperatorReserveWidth: operatorReserveWidth,
            DigitWidth: digitWidth,
            FontSize: fontSize,
            RowHeight: rowHeight,
            LineInset: lineInset,
            PreferredHeight: Math.Max(
                MinimumPreferredHeight,
                preferredHeight),
            NumberColumnCount: numberColumnCount);
    }

    private int CalculateNumberColumnCount()
    {
        int maxColumns =
            Math.Max(
                CountVisualColumns(
                    _firstText),
                Math.Max(
                    CountVisualColumns(
                        _secondText),
                    CountVisualColumns(
                        _resultText)));

        foreach (PartialProductRow row
                 in _partialProducts)
        {
            maxColumns =
                Math.Max(
                    maxColumns,
                    CountVisualColumns(
                        row.Text) +
                    row.ShiftColumns);
        }

        return Math.Max(
            1,
            maxColumns);
    }

    private static void ConfigureCanvas(
        ICanvas canvas,
        float fontSize,
        float scale)
    {
        canvas.FontColor =
            ThemeResource.GetColor(
                "WallpaperTextPrimaryColor",
                "#1F2937");

        canvas.FontSize =
            fontSize;

        canvas.StrokeColor =
            ThemeResource.GetColor(
                "WallpaperTextPrimaryColor",
                "#1F2937");

        canvas.StrokeSize =
            Math.Max(
                1.2f,
                2.2f *
                scale);
    }

    private static void DrawEmptyMessage(
        ICanvas canvas,
        RectF bounds)
    {
        canvas.FontColor =
            ThemeResource.GetColor(
                "WallpaperTextSecondaryColor",
                "#64748B");

        canvas.FontSize = 16f;

        canvas.DrawString(
            "Thực hiện phép tính để xem cách đặt tính.",
            bounds,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private void BuildPartialProducts(
        BigInteger multiplicand,
        BigInteger multiplier)
    {
        _partialProducts.Clear();

        string multiplierDigits =
            multiplier.ToString();

        for (int digitIndex = 0;
             digitIndex < multiplierDigits.Length;
             digitIndex++)
        {
            int sourceIndex =
                multiplierDigits.Length -
                1 -
                digitIndex;

            int digit =
                multiplierDigits[sourceIndex] -
                '0';

            BigInteger partial =
                multiplicand *
                digit;

            _partialProducts.Add(
                new PartialProductRow(
                    partial.ToString(),
                    digitIndex));
        }
    }

    private static DecimalParts GetDecimalParts(
        decimal value)
    {
        int[] bits =
            decimal.GetBits(
                value);

        BigInteger magnitude =
            (BigInteger)(uint)bits[0] |
            ((BigInteger)(uint)bits[1] << 32) |
            ((BigInteger)(uint)bits[2] << 64);

        bool isNegative =
            (bits[3] & unchecked((int)0x80000000)) != 0 &&
            !magnitude.IsZero;

        int scale =
            (bits[3] >> 16) &
            0x7F;

        return new DecimalParts(
            isNegative
                ? -magnitude
                : magnitude,
            scale);
    }

    private static BigInteger ScaleTo(
        BigInteger value,
        int currentScale,
        int targetScale)
    {
        int scaleDelta =
            targetScale -
            currentScale;

        return scaleDelta <= 0
            ? value
            : value *
              BigInteger.Pow(
                  10,
                  scaleDelta);
    }

    private static string FormatScaledInteger(
        BigInteger value,
        int scale)
    {
        bool isNegative =
            value.Sign < 0;

        string digits =
            BigInteger.Abs(
                value)
            .ToString();

        if (scale > 0)
        {
            digits =
                digits.PadLeft(
                    scale + 1,
                    '0');

            int decimalIndex =
                digits.Length -
                scale;

            digits =
                digits.Insert(
                    decimalIndex,
                    ".");
        }

        return isNegative
            ? "−" + digits
            : digits;
    }

    private static string FormatInteger(
        BigInteger value)
    {
        return value.Sign < 0
            ? "−" +
              BigInteger.Abs(value)
                  .ToString()
            : value.ToString();
    }

    private static int CountVisualColumns(
        string text)
    {
        return text.Count(
            character =>
                character != '.' &&
                character != ',');
    }

    private static string GetOperationSymbol(
        ArithmeticOperation operation)
    {
        return operation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "−",
            ArithmeticOperation.Multiply => "×",
            _ => string.Empty
        };
    }

    private static void EnsureSupportedOperation(
        ArithmeticOperation operation)
    {
        if (operation is not (
                ArithmeticOperation.Add or
                ArithmeticOperation.Subtract or
                ArithmeticOperation.Multiply))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Chỉ hỗ trợ đặt tính cộng, trừ và nhân.");
        }
    }

    private readonly record struct PartialProductRow(
        string Text,
        int ShiftColumns);

    private readonly record struct DecimalParts(
        BigInteger UnscaledValue,
        int Scale);

    private readonly record struct LayoutMetrics(
        float Scale,
        float Padding,
        float OperatorReserveWidth,
        float DigitWidth,
        float FontSize,
        float RowHeight,
        float LineInset,
        float PreferredHeight,
        int NumberColumnCount);
}
