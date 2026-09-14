using System.Diagnostics;
using System.Numerics;
using MathSolver.Numerics;

internal static class AuditMeasure
{
    internal record Sample(string Operation, long InputBits, int Power, double Milliseconds, long AllocatedBytes);
    internal static readonly List<Sample> Samples = [];
    private static T Measure<T>(string operation, long bits, int power, Func<T> action)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        T value = action();
        double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Samples.Add(new(operation, bits, power, ms, allocated));
        return value;
    }
    internal static BigInteger Pow(BigInteger value, int exponent) =>
        Measure("runtime-pow", value.GetBitLength(), exponent, () => BigInteger.Pow(value, exponent));
    internal static BigInteger Multiply(BigInteger left, BigInteger right) =>
        Measure(left == right ? "runtime-square" : "runtime-multiply", left.GetBitLength(), 0, () => left * right);
    internal static ushort[] SquareMagnitude(ushort[] value, ulong[] workspace, CancellationToken token) =>
        Measure("custom-square", value.Length * 16, 2, () => Avx2BigIntegerPower.SquareMagnitude(value, workspace, token));
    internal static ushort[] MultiplyMagnitude(ushort[] left, ushort[] right, ulong[] workspace, CancellationToken token) =>
        Measure("custom-multiply", left.Length * 16, 0, () => Avx2BigIntegerPower.MultiplyMagnitude(left, right, workspace, token));
    internal static BigInteger ToBigInteger(ushort[] value) =>
        Measure("handoff", value.Length * 16, 0, () => Avx2BigIntegerPower.ToBigInteger(value));
}
