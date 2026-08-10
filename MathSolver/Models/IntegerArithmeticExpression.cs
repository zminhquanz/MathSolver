using System.Numerics;

namespace MathSolver.Models;

/// <summary>
/// Một biểu thức số nguyên độc lập với giao diện.
/// </summary>
public readonly record struct IntegerArithmeticExpression(
    BigInteger LeftOperand,
    ArithmeticOperation Operation,
    BigInteger RightOperand);

/// <summary>
/// Kết quả chính xác của một biểu thức số nguyên. Với phép chia,
/// Result là thương và Remainder là số dư có dấu theo BigInteger.DivRem.
/// </summary>
public sealed record IntegerArithmeticResult(
    IntegerArithmeticExpression Expression,
    BigInteger Result,
    BigInteger Remainder)
{
    public bool IsDivision =>
        Expression.Operation == ArithmeticOperation.Divide;

    public bool IsExactDivision =>
        IsDivision &&
        Remainder.IsZero;
}
