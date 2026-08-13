using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Services.Core;

/// <summary>
/// Kết quả trung bình cộng giữ tổng dưới dạng phân số chính xác trước khi
/// chuyển sang OctoDouble cho kết quả cuối.
/// </summary>
public sealed record ArithmeticMeanResult(
    BigInteger SumNumerator,
    BigInteger SumDenominator,
    int Count,
    OctoDouble Average)
{
    public BigInteger AverageDenominator =>
        SumDenominator * Count;

    public bool IsFinite =>
        Average.IsFinite;
}

/// <summary>
/// Engine trung bình cộng không phụ thuộc UI. Đầu vào số nguyên vẫn là
/// Int128; đầu vào thập phân vẫn là Decimal; kết quả cuối là OctoDouble.
/// </summary>
public sealed class ArithmeticMeanEngine
{
    public ArithmeticMeanResult CalculateInteger(
        IReadOnlyList<Int128> values)
    {
        ValidateValues(values);

        BigInteger sum = BigInteger.Zero;

        foreach (Int128 value in values)
        {
            sum += (BigInteger)value;
        }

        return CreateResult(
            sum,
            BigInteger.One,
            values.Count);
    }

    public ArithmeticMeanResult CalculateDecimal(
        IReadOnlyList<decimal> values)
    {
        ValidateValues(values);

        int commonScale = values.Max(GetDecimalScale);
        BigInteger denominator = BigInteger.Pow(10, commonScale);
        BigInteger numerator = BigInteger.Zero;

        foreach (decimal value in values)
        {
            GetDecimalParts(
                value,
                out BigInteger unscaledValue,
                out int scale);

            numerator +=
                unscaledValue *
                BigInteger.Pow(
                    10,
                    commonScale - scale);
        }

        return CreateResult(
            numerator,
            denominator,
            values.Count);
    }

    public static void GetDecimalParts(
        decimal value,
        out BigInteger unscaledValue,
        out int scale)
    {
        int[] bits = decimal.GetBits(value);

        uint low = unchecked((uint)bits[0]);
        uint middle = unchecked((uint)bits[1]);
        uint high = unchecked((uint)bits[2]);

        scale = (bits[3] >> 16) & 0xFF;

        unscaledValue =
            ((BigInteger)high << 64) |
            ((BigInteger)middle << 32) |
            low;

        if ((bits[3] & int.MinValue) != 0)
        {
            unscaledValue = BigInteger.Negate(unscaledValue);
        }
    }

    private static ArithmeticMeanResult CreateResult(
        BigInteger numerator,
        BigInteger denominator,
        int count)
    {
        OctoDouble average =
            OctoDouble.FromRational(
                numerator,
                denominator * count);

        return new(
            numerator,
            denominator,
            count,
            average);
    }

    private static int GetDecimalScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0xFF;

    private static void ValidateValues<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one value is required.",
                nameof(values));
        }
    }
}
