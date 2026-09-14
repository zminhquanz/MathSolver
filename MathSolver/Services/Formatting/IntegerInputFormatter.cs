using System.Text;

namespace MathSolver.Services;

internal static class IntegerInputFormatter
{
    public static string FormatWhileTyping(
        string? text,
        bool allowDecimal = false)
    {
        string normalizedText =
            (text ?? string.Empty)
            .Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                '−',
                '-');

        if (normalizedText.Length == 0 ||
            normalizedText == "-")
        {
            return normalizedText;
        }

        bool isNegative =
            normalizedText[0] == '-';

        string unsignedText =
            isNegative
                ? normalizedText[1..]
                : normalizedText;

        int decimalPointIndex =
            allowDecimal
                ? unsignedText.IndexOf('.')
                : -1;

        bool hasDecimalPoint = decimalPointIndex >= 0;

        string digits =
            hasDecimalPoint
                ? unsignedText[..decimalPointIndex]
                : unsignedText;

        string decimalPart =
            hasDecimalPoint
                ? unsignedText[(decimalPointIndex + 1)..]
                : string.Empty;

        digits =
            digits.TrimStart('0');

        if (digits.Length == 0)
        {
            digits =
                "0";
        }

        string groupedDigits =
            AddThousandsSeparators(
                digits);

        string sign = isNegative ? "-" : string.Empty;

        return hasDecimalPoint
            ? $"{sign}{groupedDigits}.{decimalPart}"
            : $"{sign}{groupedDigits}";
    }

    public static int CountLogicalCharacters(
        string text,
        int cursorPosition)
    {
        int logicalCount =
            0;

        int characterCount =
            Math.Min(
                Math.Max(
                    cursorPosition,
                    0),
                text.Length);

        for (int index = 0;
             index < characterCount;
             index++)
        {
            if (text[index] != ',')
            {
                logicalCount++;
            }
        }

        return logicalCount;
    }

    public static int FindCursorPosition(
        string formattedText,
        int logicalPosition)
    {
        if (logicalPosition <= 0)
        {
            return 0;
        }

        int logicalCount =
            0;

        for (int index = 0;
             index < formattedText.Length;
             index++)
        {
            if (formattedText[index] == ',')
            {
                continue;
            }

            logicalCount++;

            if (logicalCount >=
                logicalPosition)
            {
                return index + 1;
            }
        }

        return formattedText.Length;
    }

    /// <summary>
    /// Adds invariant thousands separators to the integer part of a plain
    /// decimal number while preserving its fractional digits. This is the
    /// shared final-display path for solver results such as
    /// 1234567.8901234567 -> 1,234,567.8901234567. Scientific notation is
    /// intentionally handled by each solver before this method is called.
    /// </summary>
    public static string AddThousandsSeparatorsToPlainNumber(
        string text,
        bool useUnicodeMinus = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        bool isNegative =
            text[0] is '-' or '−';

        string unsignedText =
            isNegative
                ? text[1..]
                : text;

        // This helper is only for normal decimal notation. Leave a scientific
        // value untouched so the caller can render its exponent consistently.
        if (unsignedText.Contains('e') ||
            unsignedText.Contains('E'))
        {
            return useUnicodeMinus
                ? text.Replace(
                    "-",
                    "−",
                    StringComparison.Ordinal)
                : text.Replace(
                    '−',
                    '-');
        }

        int decimalPointIndex =
            unsignedText.IndexOf(
                '.',
                StringComparison.Ordinal);

        string integerPart =
            decimalPointIndex >= 0
                ? unsignedText[..decimalPointIndex]
                : unsignedText;

        string fractionPart =
            decimalPointIndex >= 0
                ? unsignedText[decimalPointIndex..]
                : string.Empty;

        if (integerPart.Length == 0)
        {
            integerPart = "0";
        }

        string sign =
            isNegative
                ? useUnicodeMinus
                    ? "−"
                    : "-"
                : string.Empty;

        return
            sign +
            AddThousandsSeparators(
                integerPart) +
            fractionPart;
    }

    public static string AddThousandsSeparators(
        string digits)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        var builder =
            new StringBuilder(
                digits.Length +
                digits.Length / 3);

        int firstGroupLength =
            digits.Length % 3;

        if (firstGroupLength == 0)
        {
            firstGroupLength =
                3;
        }

        builder.Append(
            digits,
            0,
            firstGroupLength);

        for (int index = firstGroupLength;
             index < digits.Length;
             index += 3)
        {
            builder.Append(',');
            builder.Append(
                digits,
                index,
                3);
        }

        return builder.ToString();
    }
}
