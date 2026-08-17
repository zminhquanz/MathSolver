using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Services.Core;

public enum QuadraticSolutionKind
{
    NoRealRoots,
    DoubleRoot,
    TwoDistinctRoots,
    NotFinite
}

public sealed record QuadraticEquationResult(
    QuadraticSolutionKind Kind,
    BigInteger Delta,
    OctoDouble SquareRootDelta,
    OctoDouble FirstRoot,
    OctoDouble SecondRoot)
{
    public bool IsFinite =>
        Kind != QuadraticSolutionKind.NotFinite;
}

/// <summary>
/// Engine giải phương trình ax² + bx + c = 0 theo mô hình lai:
/// Δ được tính chính xác tuyệt đối bằng BigInteger, còn √Δ và nghiệm được
/// tính bằng OctoDouble. Lớp không phụ thuộc MAUI.
/// </summary>
public sealed class QuadraticEquationEngine
{
    public QuadraticEquationResult Solve(
        Int128 a,
        Int128 b,
        Int128 c)
    {
        if (a == Int128.Zero)
        {
            throw new ArgumentException(
                "Coefficient a must be non-zero.",
                nameof(a));
        }

        OctoDouble preciseA = OctoDouble.FromInt128(a);
        OctoDouble preciseB = OctoDouble.FromInt128(b);
        OctoDouble preciseC = OctoDouble.FromInt128(c);

        // a, b, c đều là Int128 nên Δ = b² − 4ac luôn là số nguyên.
        // Tính Δ bằng BigInteger để việc xét dấu / Δ = 0 hoàn toàn chính xác,
        // kể cả khi b² hoặc 4ac vượt xa miền Int128.
        BigInteger integerA = (BigInteger)a;
        BigInteger integerB = (BigInteger)b;
        BigInteger integerC = (BigInteger)c;
        BigInteger delta =
            integerB * integerB -
            4 * integerA * integerC;

        if (delta.Sign < 0)
        {
            return new(
                QuadraticSolutionKind.NoRealRoots,
                delta,
                OctoDouble.NaN,
                OctoDouble.NaN,
                OctoDouble.NaN);
        }

        if (delta.IsZero)
        {
            OctoDouble root =
                -preciseB /
                (2d * preciseA);

            return root.IsFinite
                ? new(
                    QuadraticSolutionKind.DoubleRoot,
                    delta,
                    OctoDouble.Zero,
                    root,
                    root)
                : NotFinite(delta);
        }

        // Chỉ chuyển Δ sang OctoDouble tại bước cần √Δ để tính nghiệm.
        // Với hệ số Int128, Δ có tối đa khoảng 77 chữ số thập phân, nhỏ hơn
        // đáng kể độ chính xác của OctoDouble nên phép chuyển này vẫn giữ
        // đủ thông tin cần thiết cho phần nghiệm.
        OctoDouble squareRootDelta =
            OctoDouble.Sqrt(
                OctoDouble.FromBigInteger(delta));

        if (!squareRootDelta.IsFinite)
        {
            return NotFinite(delta);
        }

        OctoDouble firstRoot;
        OctoDouble secondRoot;

        // Công thức q hạn chế triệt tiêu số khi b và √Δ gần bằng nhau.
        OctoDouble q =
            -0.5d *
            (preciseB +
             OctoDouble.CopySign(
                 squareRootDelta,
                 preciseB));

        if (!q.IsZero)
        {
            firstRoot = q / preciseA;
            secondRoot = preciseC / q;
        }
        else
        {
            OctoDouble denominator = 2d * preciseA;
            firstRoot = (-preciseB + squareRootDelta) / denominator;
            secondRoot = (-preciseB - squareRootDelta) / denominator;
        }

        return firstRoot.IsFinite && secondRoot.IsFinite
            ? new(
                QuadraticSolutionKind.TwoDistinctRoots,
                delta,
                squareRootDelta,
                firstRoot,
                secondRoot)
            : NotFinite(delta);
    }

    private static QuadraticEquationResult NotFinite(
        BigInteger delta) =>
        new(
            QuadraticSolutionKind.NotFinite,
            delta,
            OctoDouble.NaN,
            OctoDouble.NaN,
            OctoDouble.NaN);
}
