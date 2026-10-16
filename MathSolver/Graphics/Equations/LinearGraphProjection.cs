using MathSolver.Numerics;

namespace MathSolver.Graphics;

/// <summary>Pixel projection only; roots and viewport geometry retain QuadDouble precision.</summary>
internal readonly struct LinearGraphProjection
{
    private readonly QuadDouble _a;
    private readonly QuadDouble _b;
    private readonly double _drawA;
    private readonly double _drawB;
    private readonly double _aProjectionError;
    private readonly double _bProjectionError;

    internal LinearGraphProjection(QuadDouble a, QuadDouble b)
    {
        _a = a;
        _b = b;
        _drawA = a.ToDouble();
        _drawB = b.ToDouble();
        _aProjectionError = ProjectionError(a, _drawA);
        _bProjectionError = ProjectionError(b, _drawB);
    }

    internal void EvaluatePair(double x1, double x2, double absoluteTolerance, out double y1, out double y2)
    {
        FunctionGraphSimdEvaluator.EvaluateLinearPair(x1, x2, _drawA, _drawB, out y1, out y2);
        if (!WithinPixelBudget(x1, y1, absoluteTolerance)) y1 = EvaluatePrecise(x1);
        if (!WithinPixelBudget(x2, y2, absoluteTolerance)) y2 = EvaluatePrecise(x2);
    }

    private double EvaluatePrecise(double x) => QuadDouble.FusedMultiplyAdd(_a, new QuadDouble(x), _b).ToDouble();

    private bool WithinPixelBudget(double x, double y, double tolerance)
    {
        if (!double.IsFinite(y) || !double.IsFinite(tolerance) || tolerance < 0d) return false;
        double absX = Math.Abs(x);
        // Conservative estimate covers coefficient projection and either fused
        // or unfused raster arithmetic. Cancellation at high zoom falls back.
        double error = absX * _aProjectionError + _bProjectionError
            + (Math.Abs(_drawA) * absX + Math.Abs(_drawB)) * (4d * 2.2204460492503131e-16)
            + double.Epsilon;
        return double.IsFinite(error) && error <= tolerance;
    }

    private static double ProjectionError(QuadDouble precise, double projected)
    {
        var residual = QuadDouble.Abs(precise - new QuadDouble(projected));
        return residual.IsZero ? 0d : Math.BitIncrement(residual.ToDouble());
    }
}
