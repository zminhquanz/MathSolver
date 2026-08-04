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
