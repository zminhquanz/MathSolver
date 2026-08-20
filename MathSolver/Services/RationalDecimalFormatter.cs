using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Formats an exact BigInteger numerator/denominator pair as decimal text.
/// Terminating decimals are kept exact. Repeating decimals are rounded to a
/// caller-specified maximum number of fractional digits without going through
/// double/decimal, so no binary floating-point noise is introduced.
/// </summary>
internal static class RationalDecimalFormatter
{
    public static string Format(
        BigInteger numerator,
        BigInteger denominator,
        int maxRepeatingDecimalPlaces)
    {
        if (denominator.IsZero)
        {
            return numerator.Sign < 0
                ? "-Infinity"
                : numerator.IsZero
                    ? "NaN"
                    : "Infinity";
        }

        if (numerator.IsZero)
        {
            return "0";
        }

        bool negative =
            numerator.Sign != denominator.Sign;

        BigInteger absoluteNumerator =
            BigInteger.Abs(numerator);

        BigInteger absoluteDenominator =
            BigInteger.Abs(denominator);

        BigInteger gcd =
            BigInteger.GreatestCommonDivisor(
                absoluteNumerator,
                absoluteDenominator);

        absoluteNumerator /= gcd;
        absoluteDenominator /= gcd;

        if (absoluteDenominator.IsOne)
        {
            string integerText =
                absoluteNumerator.ToString(
                    CultureInfo.InvariantCulture);

            return negative
                ? "-" + integerText
                : integerText;
        }

        BigInteger remainingDenominator =
            absoluteDenominator;

        int factorTwoCount = 0;
        int factorFiveCount = 0;

        while ((remainingDenominator & 1) == 0)
        {
            remainingDenominator >>= 1;
            factorTwoCount++;
        }

        while (remainingDenominator % 5 == 0)
        {
            remainingDenominator /= 5;
            factorFiveCount++;
        }

        if (remainingDenominator.IsOne)
        {
            return FormatTerminatingDecimal(
                absoluteNumerator,
                absoluteDenominator,
                factorTwoCount,
                factorFiveCount,
                negative);
        }

        return FormatRepeatingDecimal(
            absoluteNumerator,
            absoluteDenominator,
            Math.Max(0, maxRepeatingDecimalPlaces),
            negative);
    }

    private static string FormatTerminatingDecimal(
        BigInteger numerator,
        BigInteger denominator,
        int factorTwoCount,
        int factorFiveCount,
        bool negative)
    {
        int decimalPlaces =
            Math.Max(
                factorTwoCount,
                factorFiveCount);

        BigInteger scaledNumerator =
            numerator;

        if (factorTwoCount < decimalPlaces)
        {
            scaledNumerator *=
                BigInteger.Pow(
                    2,
                    decimalPlaces - factorTwoCount);
        }

        if (factorFiveCount < decimalPlaces)
        {
            scaledNumerator *=
                BigInteger.Pow(
                    5,
                    decimalPlaces - factorFiveCount);
        }

        BigInteger scale =
            BigInteger.Pow(
                10,
                decimalPlaces);

        BigInteger integerPart =
            BigInteger.DivRem(
                scaledNumerator,
                scale,
                out BigInteger fractionalPart);

        string sign =
            negative
                ? "-"
                : string.Empty;

        string integerText =
            integerPart.ToString(
                CultureInfo.InvariantCulture);

        if (fractionalPart.IsZero)
        {
            return sign + integerText;
        }

        string fractionalText =
            fractionalPart
                .ToString(
                    CultureInfo.InvariantCulture)
                .PadLeft(
                    decimalPlaces,
                    '0')
                .TrimEnd('0');

        return
            $"{sign}{integerText}.{fractionalText}";
    }

    private static string FormatRepeatingDecimal(
        BigInteger numerator,
        BigInteger denominator,
        int maxDecimalPlaces,
        bool negative)
    {
        if (maxDecimalPlaces <= 0)
        {
            BigInteger integerValue =
                BigInteger.DivRem(
                    numerator,
                    denominator,
                    out BigInteger remainder);

            if ((remainder << 1) >= denominator)
            {
                integerValue++;
            }

            string roundedInteger =
                integerValue.ToString(
                    CultureInfo.InvariantCulture);

            return negative && !integerValue.IsZero
                ? "-" + roundedInteger
                : roundedInteger;
        }

        BigInteger scale =
            BigInteger.Pow(
                10,
                maxDecimalPlaces);

        BigInteger scaledValue =
            BigInteger.DivRem(
                numerator * scale,
                denominator,
                out BigInteger remainderAfterScale);

        // Round half-up using exact integer arithmetic. The sign is attached
        // only after rounding the magnitude, so negative values round away
        // from zero on an exact half just like positive values.
        if ((remainderAfterScale << 1) >= denominator)
        {
            scaledValue++;
        }

        BigInteger integerPart =
            BigInteger.DivRem(
                scaledValue,
                scale,
                out BigInteger fractionalPart);

        string sign =
            negative && !scaledValue.IsZero
                ? "-"
                : string.Empty;

        string integerText =
            integerPart.ToString(
                CultureInfo.InvariantCulture);

        if (fractionalPart.IsZero)
        {
            return sign + integerText;
        }

        string fractionalText =
            fractionalPart
                .ToString(
                    CultureInfo.InvariantCulture)
                .PadLeft(
                    maxDecimalPlaces,
                    '0')
                .TrimEnd('0');

        return
            $"{sign}{integerText}.{fractionalText}";
    }
}
