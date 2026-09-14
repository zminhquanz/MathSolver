using MathSolver.Models;

using System.Globalization;
using System.Text;

namespace MathSolver.Services;

public static class LongDivisionCalculator
{
    public static LongDivisionResult Calculate(decimal dividend, decimal divisor, int maximumDecimalPlaces = 8)
    {
        if (maximumDecimalPlaces < 0)
        {
            maximumDecimalPlaces = 0;
        }

        if (dividend < 0 || divisor < 0)
        {
            decimal quotient = dividend / divisor;

            return new LongDivisionResult
            {
                OriginalDividend = dividend,
                OriginalDivisor = divisor,

                NormalizedDividendText = string.Empty,
                NormalizedDivisorText = string.Empty,

                Quotient = quotient,
                QuotientText = quotient.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture),

                Remainder = 0,
                IsDecimalDivision = true,
                DecimalShiftCount = 0,
                QuotientDecimalIndex = -1,

                IsLongDivisionSupported = false,

                Steps = Array.Empty<DivisionStep>()
            };
        }

        string dividendText = ToPlainDecimalString(dividend);
        string divisorText = ToPlainDecimalString(divisor);

        int dividendDecimalPlaces =
            CountDecimalPlaces(dividendText);

        int divisorDecimalPlaces =
            CountDecimalPlaces(divisorText);

        int decimalShiftCount =
            divisorDecimalPlaces;

        // Dịch dấu phẩy của cả hai số sang phải cùng số vị trí,
        // để số chia trở thành số nguyên.
        string normalizedDividendText =
            ShiftDecimalRight(
                dividendText,
                decimalShiftCount);

        string normalizedDivisorText =
            ShiftDecimalRight(
                divisorText,
                decimalShiftCount);

        normalizedDividendText =
            RemoveDecimalSeparator(
                normalizedDividendText);

        normalizedDivisorText =
            RemoveDecimalSeparator(
                normalizedDivisorText);

        normalizedDividendText =
            TrimLeadingZeros(
                normalizedDividendText);

        normalizedDivisorText =
            TrimLeadingZeros(
                normalizedDivisorText);

        long normalizedDivisor =
            long.Parse(
                normalizedDivisorText,
                CultureInfo.InvariantCulture);

        // Số chữ số thập phân vẫn còn trong số bị chia
        // sau khi dịch dấu phẩy.
        int normalizedDividendDecimalPlaces =
            Math.Max(
                0,
                dividendDecimalPlaces -
                decimalShiftCount);

        return CalculateNormalized(
            dividend,
            divisor,
            normalizedDividendText,
            normalizedDivisorText,
            normalizedDivisor,
            normalizedDividendDecimalPlaces,
            decimalShiftCount,
            maximumDecimalPlaces);
    }

    private static LongDivisionResult CalculateNormalized(
        decimal originalDividend,
        decimal originalDivisor,
        string normalizedDividendDigits,
        string normalizedDivisorText,
        long normalizedDivisor,
        int dividendDecimalPlaces,
        int decimalShiftCount,
        int maximumDecimalPlaces)
    {
        var steps = new List<DivisionStep>();
        var quotientBuilder = new StringBuilder();

        int integerDigitCount =
            normalizedDividendDigits.Length -
            dividendDecimalPlaces;

        if (integerDigitCount <= 0)
        {
            integerDigitCount = 1;
        }

        long partialDividend = 0;
        bool quotientStarted = false;
        bool decimalPointAdded = false;

        int column = 0;
        int sourceLength =
            normalizedDividendDigits.Length;

        int totalColumns =
            sourceLength + maximumDecimalPlaces;

        for (column = 0;
             column < totalColumns;
             column++)
        {
            bool isOriginalDigit =
                column < sourceLength;

            int digit = isOriginalDigit
                ? normalizedDividendDigits[column] - '0'
                : 0;

            partialDividend =
                checked(partialDividend * 10 + digit);

            bool isAfterDecimalPoint =
                column >= integerDigitCount;

            if (isAfterDecimalPoint &&
                !decimalPointAdded)
            {
                if (!quotientStarted)
                {
                    quotientBuilder.Append('0');
                }

                quotientBuilder.Append('.');
                decimalPointAdded = true;
            }

            if (!quotientStarted)
            {
                if (partialDividend < normalizedDivisor)
                {
                    if (isAfterDecimalPoint)
                    {
                        quotientBuilder.Append('0');
                    }

                    continue;
                }

                quotientStarted = true;
            }

            int quotientDigit =
                (int)(partialDividend / normalizedDivisor);

            long product =
                quotientDigit * normalizedDivisor;

            long remainder =
                partialDividend - product;

            quotientBuilder.Append(quotientDigit);

            steps.Add(new DivisionStep
            {
                PartialDividendText =
                    partialDividend.ToString(
                        CultureInfo.InvariantCulture),

                QuotientDigit = quotientDigit,

                ProductText =
                    product.ToString(
                        CultureInfo.InvariantCulture),

                RemainderText =
                    remainder.ToString(
                        CultureInfo.InvariantCulture),

                EndColumn = column,

                IsAfterDecimalPoint =
                    isAfterDecimalPoint
            });

            partialDividend = remainder;

            if (partialDividend == 0 &&
                column >= sourceLength - 1)
            {
                break;
            }
        }

        if (quotientBuilder.Length == 0)
        {
            quotientBuilder.Append('0');
        }

        string quotientText =
            NormalizeQuotientText(
                quotientBuilder.ToString());

        decimal quotient =
            decimal.Parse(
                quotientText,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture);

        int quotientDecimalIndex =
            quotientText.IndexOf('.');

        return new LongDivisionResult
        {
            OriginalDividend =
                originalDividend,

            OriginalDivisor =
                originalDivisor,

            NormalizedDividendText =
                InsertDecimalPoint(
                    normalizedDividendDigits,
                    dividendDecimalPlaces),

            NormalizedDivisorText =
                normalizedDivisorText,

            QuotientText =
                quotientText,

            Quotient =
                quotient,

            Remainder =
                partialDividend,

            IsDecimalDivision =
                originalDividend !=
                decimal.Truncate(originalDividend) ||
                originalDivisor !=
                decimal.Truncate(originalDivisor),

            DecimalShiftCount =
                decimalShiftCount,

            QuotientDecimalIndex =
                quotientDecimalIndex,

            Steps =
                steps
        };
    }

    private static string ToPlainDecimalString(
        decimal value)
    {
        return value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
    }

    private static int CountDecimalPlaces(
        string value)
    {
        int separatorIndex =
            value.IndexOf('.');

        if (separatorIndex < 0)
        {
            return 0;
        }

        return value.Length -
               separatorIndex - 1;
    }

    private static string ShiftDecimalRight(
        string value,
        int positions)
    {
        if (positions <= 0)
        {
            return value;
        }

        string digits =
            RemoveDecimalSeparator(value);

        int originalDecimalPlaces =
            CountDecimalPlaces(value);

        int remainingDecimalPlaces =
            originalDecimalPlaces - positions;

        if (remainingDecimalPlaces <= 0)
        {
            return digits +
                   new string(
                       '0',
                       Math.Abs(
                           remainingDecimalPlaces));
        }

        int separatorPosition =
            digits.Length -
            remainingDecimalPlaces;

        return digits.Insert(
            separatorPosition,
            ".");
    }

    private static string RemoveDecimalSeparator(
        string value)
    {
        return value.Replace(".", string.Empty);
    }

    private static string InsertDecimalPoint(
        string digits,
        int decimalPlaces)
    {
        if (decimalPlaces <= 0)
        {
            return digits;
        }

        if (digits.Length <= decimalPlaces)
        {
            digits =
                digits.PadLeft(
                    decimalPlaces + 1,
                    '0');
        }

        int position =
            digits.Length -
            decimalPlaces;

        return digits.Insert(
            position,
            ".");
    }

    private static string TrimLeadingZeros(
        string value)
    {
        string trimmed =
            value.TrimStart('0');

        return trimmed.Length == 0
            ? "0"
            : trimmed;
    }

    private static string NormalizeQuotientText(
        string value)
    {
        if (value.StartsWith(
                ".",
                StringComparison.Ordinal))
        {
            value = "0" + value;
        }

        if (value.EndsWith(
                ".",
                StringComparison.Ordinal))
        {
            value += "0";
        }

        return value;
    }
}