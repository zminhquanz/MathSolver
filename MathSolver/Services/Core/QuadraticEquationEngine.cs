using MathSolver.Numerics;

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
    OctoDouble Delta,
    OctoDouble SquareRootDelta,
    OctoDouble FirstRoot,
    OctoDouble SecondRoot)
{
    public bool IsFinite =>
        Kind != QuadraticSolutionKind.NotFinite;
}

/// <summary>
/// Engine giải phương trình ax² + bx + c = 0 bằng OctoDouble. Lớp không
/// phụ thuộc MAUI; View chỉ định dạng lời giải và đồ thị từ kết quả trả về.
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

        // Δ = b² − 4ac. FMA preserves the current high-precision behavior.
        OctoDouble delta =
            OctoDouble.FusedMultiplyAdd(
                -4d * preciseA,
                preciseC,
                preciseB * preciseB);

        if (!delta.IsFinite)
        {
            return NotFinite(delta);
        }

        if (delta < OctoDouble.Zero)
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

        OctoDouble squareRootDelta =
            OctoDouble.Sqrt(delta);

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
        OctoDouble delta) =>
        new(
            QuadraticSolutionKind.NotFinite,
            delta,
            OctoDouble.NaN,
            OctoDouble.NaN,
            OctoDouble.NaN);
}
