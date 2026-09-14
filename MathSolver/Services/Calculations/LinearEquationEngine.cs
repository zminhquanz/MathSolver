using MathSolver.Numerics;

namespace MathSolver.Services.Core;

public sealed record LinearEquationResult(
    QuadDouble Root)
{
    public bool IsFinite => Root.IsFinite;
}

/// <summary>
/// Giải phương trình bậc nhất ax + b = 0 với hệ số Int128. Phép chia cuối
/// cùng dùng QuadDouble (~64 chữ số có nghĩa) để giữ độ chính xác cao khi
/// nghiệm là số thập phân.
/// </summary>
public sealed class LinearEquationEngine
{
    public LinearEquationResult Solve(
        Int128 a,
        Int128 b)
    {
        if (a == Int128.Zero)
        {
            throw new ArgumentException(
                "Coefficient a must be non-zero.",
                nameof(a));
        }

        QuadDouble preciseA =
            QuadDouble.FromInt128(a);

        QuadDouble preciseB =
            QuadDouble.FromInt128(b);

        QuadDouble root =
            -preciseB /
            preciseA;

        return new(root);
    }
}
