using System.Globalization;
using System.Numerics;

namespace MathSolver.Models;

/// <summary>
/// Phân số chuẩn hóa dùng làm dữ liệu đáp án, lựa chọn và tham số lệnh UI.
/// Mẫu luôn dương và tử/mẫu luôn tối giản.
/// </summary>
public readonly record struct ReducedFraction
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public ReducedFraction(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException(
                "A fraction denominator cannot be zero.");
        }

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        BigInteger divisor =
            BigInteger.GreatestCommonDivisor(
                BigInteger.Abs(numerator),
                denominator);

        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    public override string ToString() =>
        Denominator.IsOne
            ? Numerator.ToString(CultureInfo.InvariantCulture)
            : string.Concat(
                Numerator.ToString(CultureInfo.InvariantCulture),
                "/",
                Denominator.ToString(CultureInfo.InvariantCulture));

    public static bool TryParse(
        string? value,
        out ReducedFraction fraction)
    {
        fraction = default;
        string compact = (value ?? string.Empty)
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        string[] parts = compact.Split('/');
        if (parts.Length is < 1 or > 2 ||
            !BigInteger.TryParse(
                parts[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger numerator))
        {
            return false;
        }

        BigInteger denominator = BigInteger.One;
        if (parts.Length == 2 &&
            (!BigInteger.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out denominator) ||
             denominator.IsZero))
        {
            return false;
        }

        fraction = new ReducedFraction(
            numerator,
            denominator);
        return true;
    }
}

public sealed record FractionQuizContract(
    ReducedFraction LeftOperand,
    FractionOperation Operation,
    ReducedFraction RightOperand,
    ReducedFraction CorrectAnswer,
    ReducedFraction? PresentedAnswer,
    IReadOnlyList<ReducedFraction> Choices)
{
    public string OperationSymbol =>
        Operation switch
        {
            FractionOperation.Add => "+",
            FractionOperation.Subtract => "−",
            FractionOperation.Multiply => "×",
            FractionOperation.Divide => "÷",
            _ => throw new ArgumentOutOfRangeException(
                nameof(Operation))
        };

    public string ExpressionText =>
        $"{LeftOperand} {OperationSymbol} {RightOperand}";
}
