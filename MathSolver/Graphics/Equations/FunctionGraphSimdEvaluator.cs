using MathSolver.Services;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Graphics;

public static class FunctionGraphSimdEvaluator
{
    /// <summary>
    /// Evaluates the two endpoints of y = ax + b without allocating sample arrays.
    /// AVX uses two active lanes and two padding lanes; SSE/ARM use both lanes.
    /// Shares the parabola's runtime and user acceleration policy.
    /// </summary>
    public static void EvaluateLinearPair(double x1, double x2, double a, double b, out double y1, out double y2)
    {
        var path = CalculationAccelerationManager.UseSimd ? SelectedPath : SimdPath.Scalar;
        switch (path)
        {
            case SimdPath.AvxFma:
            {
                var x = Vector256.Create(x1, x2, 0d, 0d);
                var y = Fma.MultiplyAdd(Vector256.Create(a), x, Vector256.Create(b));
                y1 = y.GetElement(0); y2 = y.GetElement(1);
                return;
            }
            case SimdPath.Avx:
            {
                var x = Vector256.Create(x1, x2, 0d, 0d);
                var y = Avx.Add(Avx.Multiply(Vector256.Create(a), x), Vector256.Create(b));
                y1 = y.GetElement(0); y2 = y.GetElement(1);
                return;
            }
            case SimdPath.Sse2:
            {
                var x = Vector128.Create(x1, x2);
                var y = Sse2.Add(Sse2.Multiply(Vector128.Create(a), x), Vector128.Create(b));
                y1 = y.GetElement(0); y2 = y.GetElement(1);
                return;
            }
            case SimdPath.NeonFma:
            {
                var x = Vector128.Create(x1, x2);
                var y = AdvSimd.Arm64.FusedMultiplyAdd(Vector128.Create(b), Vector128.Create(a), x);
                y1 = y.GetElement(0); y2 = y.GetElement(1);
                return;
            }
            case SimdPath.ArmVector128:
            {
                var x = Vector128.Create(x1, x2);
                var y = Vector128.Add(Vector128.Multiply(Vector128.Create(a), x), Vector128.Create(b));
                y1 = y.GetElement(0); y2 = y.GetElement(1);
                return;
            }
            default:
                y1 = Math.FusedMultiplyAdd(a, x1, b);
                y2 = Math.FusedMultiplyAdd(a, x2, b);
                return;
        }
    }

    private enum SimdPath
    {
        AvxFma,
        Avx,
        Sse2,
        NeonFma,
        ArmVector128,
        Scalar
    }

    private static SimdPath SelectedPath =>
        DetectBestPath();

    public static string BackendName => !CalculationAccelerationManager.UseSimd ? "Scalar" : SelectedPath switch
    {
        SimdPath.AvxFma => "AVX/FMA",
        SimdPath.Avx => "AVX",
        SimdPath.Sse2 => "SSE2",
        SimdPath.NeonFma => "NEON/AdvSIMD (FMA)",
        SimdPath.ArmVector128 => "NEON/AdvSIMD (Vector128)",
        _ => "Scalar"
    };

    public static bool IsAccelerationAvailable =>
        SelectedPath != SimdPath.Scalar;

    /// <summary>
    /// Tính hàng loạt y = ax² + bx + c cho dãy sample đã ở dạng double.
    /// Đây là đường duy nhất dùng SIMD của renderer parabol. Toán học chính
    /// (Δ, nghiệm, đỉnh, viewport) vẫn nằm trong ParabolaGraphDrawable và
    /// dùng OctoDouble.
    /// </summary>
    public static void EvaluateQuadratic(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        if (count < 0 ||
            xValues.Length < count ||
            yValues.Length < count)
        {
            throw new ArgumentException(
                "Mảng mẫu không đủ kích thước.");
        }

        if (count == 0)
        {
            return;
        }

        if (!CalculationAccelerationManager.UseSimd)
        {
            EvaluateScalar(
                xValues,
                yValues,
                0,
                count,
                a,
                b,
                c);

            return;
        }

        switch (SelectedPath)
        {
            case SimdPath.AvxFma:
                EvaluateAvxFma(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.Avx:
                EvaluateAvx(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.Sse2:
                EvaluateSse2(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.NeonFma:
                EvaluateNeonFma(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            case SimdPath.ArmVector128:
                EvaluateArmVector128(
                    xValues,
                    yValues,
                    count,
                    a,
                    b,
                    c);
                return;

            default:
                EvaluateScalar(
                    xValues,
                    yValues,
                    0,
                    count,
                    a,
                    b,
                    c);
                return;
        }
    }

    private static SimdPath DetectBestPath()
    {
#if ANDROID
        if (!CalculationAccelerationManager.IsAndroidNeonExecutionAllowed)
            return SimdPath.Scalar;
#endif
        // x86: FMA + AVX là đường Horner nhanh và chính xác nhất hiện có.
        if (CalculationAccelerationManager.AllowAvx && Avx.IsSupported &&
            Fma.IsSupported)
        {
            return SimdPath.AvxFma;
        }

        if (CalculationAccelerationManager.AllowAvx && Avx.IsSupported)
        {
            return SimdPath.Avx;
        }

        if (Sse2.IsSupported)
        {
            return SimdPath.Sse2;
        }

        // ARM64: AdvSimd.Arm64 expose FMLA cho Vector128<double>.
        if (AdvSimd.Arm64.IsSupported)
        {
            return SimdPath.NeonFma;
        }

        // Một số Mono build expose generic Vector128 hardware acceleration
        // nhưng không expose AdvSimd.Arm64 trực tiếp. Trên ARM64 đây vẫn là
        // managed SIMD 128-bit (NEON), chỉ không ép FMA intrinsic.
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
            Vector128.IsHardwareAccelerated)
        {
            return SimdPath.ArmVector128;
        }

        return SimdPath.Scalar;
    }

    private static void EvaluateAvxFma(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector256<double> aVector = Vector256.Create(a);
        Vector256<double> bVector = Vector256.Create(b);
        Vector256<double> cVector = Vector256.Create(c);

        int index = 0;
        int width = Vector256<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector256<double> xVector =
                Vector256.LoadUnsafe(
                    ref xValues[index]);

            // Horner + FMA3: y = fma(fma(a, x, b), x, c)
            Vector256<double> axPlusB =
                Fma.MultiplyAdd(
                    aVector,
                    xVector,
                    bVector);

            Vector256<double> yVector =
                Fma.MultiplyAdd(
                    axPlusB,
                    xVector,
                    cVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateAvx(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector256<double> aVector = Vector256.Create(a);
        Vector256<double> bVector = Vector256.Create(b);
        Vector256<double> cVector = Vector256.Create(c);

        int index = 0;
        int width = Vector256<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector256<double> xVector =
                Vector256.LoadUnsafe(
                    ref xValues[index]);

            Vector256<double> yVector =
                Avx.Add(
                    Avx.Multiply(
                        Avx.Add(
                            Avx.Multiply(
                                aVector,
                                xVector),
                            bVector),
                        xVector),
                    cVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateSse2(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector128<double> aVector = Vector128.Create(a);
        Vector128<double> bVector = Vector128.Create(b);
        Vector128<double> cVector = Vector128.Create(c);

        int index = 0;
        int width = Vector128<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector128<double> xVector =
                Vector128.LoadUnsafe(
                    ref xValues[index]);

            Vector128<double> yVector =
                Sse2.Add(
                    Sse2.Multiply(
                        Sse2.Add(
                            Sse2.Multiply(
                                aVector,
                                xVector),
                            bVector),
                        xVector),
                    cVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateNeonFma(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector128<double> aVector = Vector128.Create(a);
        Vector128<double> bVector = Vector128.Create(b);
        Vector128<double> cVector = Vector128.Create(c);

        int index = 0;
        int width = Vector128<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector128<double> xVector =
                Vector128.LoadUnsafe(
                    ref xValues[index]);

            // AArch64 FMLA, API order: addend + left * right.
            Vector128<double> axPlusB =
                AdvSimd.Arm64.FusedMultiplyAdd(
                    bVector,
                    aVector,
                    xVector);

            Vector128<double> yVector =
                AdvSimd.Arm64.FusedMultiplyAdd(
                    cVector,
                    axPlusB,
                    xVector);

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateArmVector128(
        double[] xValues,
        double[] yValues,
        int count,
        double a,
        double b,
        double c)
    {
        Vector128<double> aVector = Vector128.Create(a);
        Vector128<double> bVector = Vector128.Create(b);
        Vector128<double> cVector = Vector128.Create(c);

        int index = 0;
        int width = Vector128<double>.Count;
        int vectorizedCount = count - count % width;

        for (;
             index < vectorizedCount;
             index += width)
        {
            Vector128<double> xVector =
                Vector128.LoadUnsafe(
                    ref xValues[index]);

            // Generic Vector128 vẫn là managed SIMD 128-bit trên ARM64.
            // Không giả FMA nếu Mono chưa expose AdvSimd.Arm64.
            Vector128<double> yVector =
                ((aVector * xVector) + bVector) * xVector + cVector;

            yVector.StoreUnsafe(
                ref yValues[index]);
        }

        EvaluateScalar(
            xValues,
            yValues,
            index,
            count,
            a,
            b,
            c);
    }

    private static void EvaluateScalar(
        double[] xValues,
        double[] yValues,
        int startIndex,
        int count,
        double a,
        double b,
        double c)
    {
        for (int index = startIndex;
             index < count;
             index++)
        {
            double x = xValues[index];

            yValues[index] =
                Math.FusedMultiplyAdd(
                    Math.FusedMultiplyAdd(
                        a,
                        x,
                        b),
                    x,
                    c);
        }
    }
}
