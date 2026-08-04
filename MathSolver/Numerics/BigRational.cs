using System.Numerics;

namespace MathSolver.Numerics;

/// <summary>
/// Exact arbitrary-precision rational number with normalized sign and fraction.
/// Multiplication cross-cancels first to keep intermediate BigInteger values small.
/// </summary>
public readonly struct BigRational : IEquatable<BigRational>
{
    public static BigRational Zero => new(BigInteger.Zero, BigInteger.One);
    public static BigRational One => new(BigInteger.One, BigInteger.One);

    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }
    public bool IsZero => Numerator.IsZero;
    public bool IsInteger => Denominator.IsOne;

    public BigRational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException("Denominator cannot be zero.");
        }

        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Negate(denominator);
        }

        if (numerator.IsZero)
        {
            Numerator = BigInteger.Zero;
            Denominator = BigInteger.One;
            return;
        }

        BigInteger divisor = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(numerator),
            denominator);

        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public static BigRational operator +(BigRational left, BigRational right)
    {
        BigInteger divisor = BigInteger.GreatestCommonDivisor(
            left.Denominator,
            right.Denominator);

        BigInteger leftScale = right.Denominator / divisor;
        BigInteger rightScale = left.Denominator / divisor;

        return new BigRational(
            left.Numerator * leftScale + right.Numerator * rightScale,
            left.Denominator * leftScale);
    }

    public static BigRational operator -(BigRational left, BigRational right) =>
        left + new BigRational(BigInteger.Negate(right.Numerator), right.Denominator);

    public static BigRational operator *(BigRational left, BigRational right)
    {
        BigInteger firstCancellation = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(left.Numerator),
            right.Denominator);

        BigInteger secondCancellation = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(right.Numerator),
            left.Denominator);

        return new BigRational(
            left.Numerator / firstCancellation *
            (right.Numerator / secondCancellation),
            left.Denominator / secondCancellation *
            (right.Denominator / firstCancellation));
    }

    public static BigRational operator /(BigRational left, BigRational right)
    {
        if (right.Numerator.IsZero)
        {
            throw new DivideByZeroException("Cannot divide by zero.");
        }

        return left * new BigRational(right.Denominator, right.Numerator);
    }

    public bool Equals(BigRational other) =>
        Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) =>
        obj is BigRational other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public override string ToString() =>
        Denominator.IsOne ? Numerator.ToString() : $"{Numerator}/{Denominator}";
}
