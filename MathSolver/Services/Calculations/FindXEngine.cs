using MathSolver.Models;
using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Services.Core;

public enum FindXCoreSolutionKind
{
    Unique,
    NoSolution,
    InfiniteSolutions,
    Undefined
}

public sealed record FindXIntegerResult(
    FindXCoreSolutionKind Kind,
    BigInteger Numerator,
    BigInteger Denominator);

public sealed record FindXDecimalResult(
    FindXCoreSolutionKind Kind,
    QuadDouble Value);

/// <summary>
/// Engine tìm thành phần chưa biết. Integer giữ nghiệm dưới dạng cặp tử/mẫu
/// rút gọn; Decimal chuyển sang QuadDouble trước khi giải.
/// </summary>
public sealed class FindXEngine
{
    public FindXIntegerResult SolveInteger(
        BigInteger knownValue,
        BigInteger resultValue,
        ArithmeticOperation operation,
        bool unknownIsLeftOperand)
    {
        switch (operation)
        {
            case ArithmeticOperation.Add:
                return Unique(
                    resultValue - knownValue,
                    BigInteger.One);

            case ArithmeticOperation.Subtract:
                return unknownIsLeftOperand
                    ? Unique(
                        resultValue + knownValue,
                        BigInteger.One)
                    : Unique(
                        knownValue - resultValue,
                        BigInteger.One);

            case ArithmeticOperation.Multiply:
                if (knownValue.IsZero)
                {
                    return resultValue.IsZero
                        ? InfiniteInteger()
                        : NoInteger();
                }

                return Unique(
                    resultValue,
                    knownValue);

            case ArithmeticOperation.Divide
                when unknownIsLeftOperand:
                return knownValue.IsZero
                    ? UndefinedInteger()
                    : Unique(
                        resultValue * knownValue,
                        BigInteger.One);

            case ArithmeticOperation.Divide:
                if (resultValue.IsZero)
                {
                    return knownValue.IsZero
                        ? InfiniteInteger()
                        : NoInteger();
                }

                if (knownValue.IsZero)
                {
                    return NoInteger();
                }

                return Unique(
                    knownValue,
                    resultValue);

            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    public FindXDecimalResult SolveDecimal(
        decimal knownValue,
        decimal resultValue,
        ArithmeticOperation operation,
        bool unknownIsLeftOperand)
    {
        QuadDouble known = QuadDouble.FromDecimal(knownValue);
        QuadDouble result = QuadDouble.FromDecimal(resultValue);

        switch (operation)
        {
            case ArithmeticOperation.Add:
                return Unique(result - known);

            case ArithmeticOperation.Subtract:
                return Unique(
                    unknownIsLeftOperand
                        ? result + known
                        : known - result);

            case ArithmeticOperation.Multiply:
                if (knownValue == 0m)
                {
                    return resultValue == 0m
                        ? InfiniteDecimal()
                        : NoDecimal();
                }

                return Unique(result / known);

            case ArithmeticOperation.Divide
                when unknownIsLeftOperand:
                return knownValue == 0m
                    ? UndefinedDecimal()
                    : Unique(result * known);

            case ArithmeticOperation.Divide:
                if (resultValue == 0m)
                {
                    return knownValue == 0m
                        ? InfiniteDecimal()
                        : NoDecimal();
                }

                if (knownValue == 0m)
                {
                    return NoDecimal();
                }

                return Unique(known / result);

            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    public (BigInteger Numerator, BigInteger Denominator)
        EvaluateIntegerLeftSide(
            BigInteger numerator,
            BigInteger denominator,
            BigInteger knownValue,
            ArithmeticOperation operation,
            bool unknownIsLeftOperand)
    {
        (BigInteger leftNumerator, BigInteger leftDenominator) =
            (operation, unknownIsLeftOperand) switch
            {
                (ArithmeticOperation.Add, _) =>
                    (numerator + knownValue * denominator, denominator),
                (ArithmeticOperation.Subtract, true) =>
                    (numerator - knownValue * denominator, denominator),
                (ArithmeticOperation.Subtract, false) =>
                    (knownValue * denominator - numerator, denominator),
                (ArithmeticOperation.Multiply, _) =>
                    (numerator * knownValue, denominator),
                (ArithmeticOperation.Divide, true) =>
                    (numerator, denominator * knownValue),
                (ArithmeticOperation.Divide, false) =>
                    (knownValue * denominator, numerator),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };

        return NormalizeFraction(
            leftNumerator,
            leftDenominator);
    }

    public QuadDouble EvaluateDecimalLeftSide(
        QuadDouble x,
        decimal knownValue,
        ArithmeticOperation operation,
        bool unknownIsLeftOperand)
    {
        QuadDouble known = QuadDouble.FromDecimal(knownValue);

        return (operation, unknownIsLeftOperand) switch
        {
            (ArithmeticOperation.Add, true) => x + known,
            (ArithmeticOperation.Add, false) => known + x,
            (ArithmeticOperation.Subtract, true) => x - known,
            (ArithmeticOperation.Subtract, false) => known - x,
            (ArithmeticOperation.Multiply, true) => x * known,
            (ArithmeticOperation.Multiply, false) => known * x,
            (ArithmeticOperation.Divide, true) => x / known,
            (ArithmeticOperation.Divide, false) => known / x,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    public static (BigInteger Numerator, BigInteger Denominator)
        NormalizeFraction(
            BigInteger numerator,
            BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException(
                "Denominator cannot be zero.");
        }

        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Negate(denominator);
        }

        if (numerator.IsZero)
        {
            return (BigInteger.Zero, BigInteger.One);
        }

        BigInteger divisor =
            BigInteger.GreatestCommonDivisor(
                BigInteger.Abs(numerator),
                denominator);

        return (
            numerator / divisor,
            denominator / divisor);
    }

    private static FindXIntegerResult Unique(
        BigInteger numerator,
        BigInteger denominator)
    {
        (numerator, denominator) =
            NormalizeFraction(numerator, denominator);

        return new(
            FindXCoreSolutionKind.Unique,
            numerator,
            denominator);
    }

    private static FindXDecimalResult Unique(QuadDouble value) =>
        new(FindXCoreSolutionKind.Unique, value);

    private static FindXIntegerResult NoInteger() =>
        new(FindXCoreSolutionKind.NoSolution, BigInteger.Zero, BigInteger.One);

    private static FindXIntegerResult InfiniteInteger() =>
        new(FindXCoreSolutionKind.InfiniteSolutions, BigInteger.Zero, BigInteger.One);

    private static FindXIntegerResult UndefinedInteger() =>
        new(FindXCoreSolutionKind.Undefined, BigInteger.Zero, BigInteger.One);

    private static FindXDecimalResult NoDecimal() =>
        new(FindXCoreSolutionKind.NoSolution, QuadDouble.Zero);

    private static FindXDecimalResult InfiniteDecimal() =>
        new(FindXCoreSolutionKind.InfiniteSolutions, QuadDouble.Zero);

    private static FindXDecimalResult UndefinedDecimal() =>
        new(FindXCoreSolutionKind.Undefined, QuadDouble.Zero);
}
