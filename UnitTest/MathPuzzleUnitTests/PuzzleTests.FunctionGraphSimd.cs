using MathSolver.Graphics;
using MathSolver.Numerics;
using MathSolver.Services;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    private delegate void LinearPair(double x1, double x2, double a, double b, out double y1, out double y2);

    internal static void CheckFunctionGraphSimd()
    {
        LinearPair pair = FunctionGraphSimdEvaluator.EvaluateLinearPair;
        Action<double[], double[], int, double, double, double> quadratic = FunctionGraphSimdEvaluator.EvaluateQuadratic;
        bool previousSimd = CalculationAccelerationManager.UseSimd;
        bool previousAvx = CalculationAccelerationManager.AllowAvx;
        try
        {
            foreach (var mode in new[] { (Simd: false, Avx: false), (Simd: true, Avx: false), (Simd: true, Avx: true) })
            {
                CalculationAccelerationManager.UseSimd = mode.Simd;
                CalculationAccelerationManager.AllowAvx = mode.Avx;
                string backend = FunctionGraphSimdEvaluator.BackendName;
                Require(mode.Simd || backend == "Scalar", "The master SIMD switch must also disable linear graph SIMD.");
                Require(mode.Avx || !backend.StartsWith("AVX", StringComparison.Ordinal), "Graph dispatcher bypassed the AVX mode gate.");
                if (mode.Simd && mode.Avx && Avx.IsSupported)
                    Require(backend == (Fma.IsSupported ? "AVX/FMA" : "AVX"), "Linear and quadratic graphs must select the available AVX/FMA path.");
                foreach (var (a, b) in new[] { (3d, -7d), (-5d, 9d), (0d, 2d), (1e-250, 1e-300), (1e150, -1e150), (134217729d, -18014398509481984d) })
                foreach (var (x1, x2) in new[] { (-1e6, 1e6), (-3.5, 0d), (1e-15, 1d), (134217727d, 134217728d) })
                {
                    pair(x1, x2, a, b, out double y1, out double y2);
                    Check(x1, y1); Check(x2, y2);
                    void Check(double x, double actual)
                    {
                        double expected = Math.FusedMultiplyAdd(a, x, b);
                        double tolerance = 8d * 2.2204460492503131e-16 * (Math.Abs(a * x) + Math.Abs(b));
                        Require(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance,
                            $"Linear endpoint mismatch in {backend}: {a} * {x} + {b}.");
                        if (backend is "AVX/FMA" or "Scalar" or "NEON/AdvSIMD (FMA)")
                            Require(actual == expected, $"Fused linear endpoint lost FMA rounding in {backend}.");
                    }
                }
                // Existing parabola arithmetic, including vector tails and untouched suffixes.
                foreach (int count in new[] { 0, 1, 2, 3, 4, 5, 1025, 2048 })
                {
                    var xs = Enumerable.Range(0, count + 2).Select(i => (i - count / 2d) / 17d).ToArray();
                    var ys = Enumerable.Repeat(double.NaN, xs.Length).ToArray();
                    quadratic(xs, ys, count, .375, -.625, .25);
                    for (int i = 0; i < count; i++)
                    {
                        double expected = Math.FusedMultiplyAdd(Math.FusedMultiplyAdd(.375, xs[i], -.625), xs[i], .25);
                        double scale = Math.Abs(.375 * xs[i] * xs[i]) + Math.Abs(.625 * xs[i]) + .25;
                        Require(Math.Abs(ys[i] - expected) <= 8d * 2.2204460492503131e-16 * scale,
                            $"Parabola changed after sharing its dispatcher: {backend}, count={count}, i={i}.");
                    }
                    Require(ys.Skip(count).All(double.IsNaN), "Graph evaluator wrote beyond the requested sample count.");
                }
                CheckPreciseProjection();
                Console.WriteLine($"  Graph backend: {backend}; linear pairs and quadratic tails verified.");
            }
        }
        finally
        {
            CalculationAccelerationManager.UseSimd = previousSimd;
            CalculationAccelerationManager.AllowAvx = previousAvx;
        }

        static void CheckPreciseProjection()
        {
            // Int128 projection loses low coefficient bits; zoomed cancellation
            // must still recover the endpoint rather than draw a shifted line.
            Int128 coefficient = ((Int128)1 << 100) + 1;
            QuadDouble a = QuadDouble.FromInt128(coefficient), b = QuadDouble.FromInt128(-coefficient + 1);
            var projection = new LinearGraphProjection(a, b);
            projection.EvaluatePair(1d, Math.BitIncrement(1d), 1e-6, out double first, out double second);
            Require(first == 1d, "Int128 cancellation was lost during SIMD graph projection.");
            double preciseSecond = QuadDouble.FusedMultiplyAdd(a, new QuadDouble(Math.BitIncrement(1d)), b).ToDouble();
            Require(second == preciseSecond, "Zoomed endpoint exceeded its pixel error budget.");

            // Without FMA, the raw double product rounds away this residual.
            var fused = new LinearGraphProjection(new QuadDouble(134217729d), new QuadDouble(-18014398509481984d));
            fused.EvaluatePair(134217727d, 134217728d, 1e-6, out first, out second);
            Require(first == -1d && second == 134217728d, "Unfused raster rounding needs a precise fallback at high zoom.");

            a = QuadDouble.FromInt128(Int128.MaxValue); b = QuadDouble.FromInt128(Int128.MinValue);
            projection = new LinearGraphProjection(a, b);
            foreach (double budget in new[] { 1e30, 1e-6, 0d, double.NaN, -1d })
            {
                projection.EvaluatePair(-.25, 1.25, budget, out first, out second);
                double expected1 = QuadDouble.FusedMultiplyAdd(a, new QuadDouble(-.25), b).ToDouble();
                double expected2 = QuadDouble.FusedMultiplyAdd(a, new QuadDouble(1.25), b).ToDouble();
                double tolerance = double.IsFinite(budget) ? Math.Max(0d, budget) : 0d;
                Require(Math.Abs(first - expected1) <= tolerance && Math.Abs(second - expected2) <= tolerance,
                    "Large Int128 coefficients exceeded the graph projection error budget.");
            }
        }
    }
}
