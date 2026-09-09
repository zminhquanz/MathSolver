using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using MathSolver.Numerics;

internal static class FinalInverseValidation
{
    private delegate void PrefixKernel(
        uint[] values, uint[] output, int halfLength, int first, int last,
        bool writeRight, uint modulus, uint root, uint inverseLength,
        uint inverseLengthShoup, CancellationToken cancellationToken);

    private static PrefixKernel Bind() => typeof(ParallelBigUnsigned)
        .GetMethod("ProcessFinalInversePrefixAvx512", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<PrefixKernel>();

    public static void Validate()
    {
        PrefixKernel kernel = Bind();
        int cases = 0;
        foreach ((uint modulus, uint generator) in new[] { (2_013_265_921u, 31u), (469_762_049u, 3u) })
        foreach (int half in new[] { 64, 256, 4096 })
        {
            uint root = (uint)BigInteger.ModPow(generator, modulus - 1 - (modulus - 1) / (2 * half), modulus);
            uint scale = (uint)BigInteger.ModPow(2 * half, modulus - 2, modulus);
            uint shoup = (uint)(((ulong)scale << 32) / modulus);
            var random = new Random(half);
            foreach (int first in new[] { 0, 1, 3, 15, 31 })
            foreach (int count in new[] { 0, 1, 15, 16, 17, 31, 32, half - first }.Distinct())
            foreach (bool writeRight in new[] { false, true })
            foreach (bool inPlace in new[] { false, true })
            foreach (bool edges in new[] { false, true })
            {
                int last = first + count;
                if (last > half) continue;
                uint[] values = Enumerable.Range(0, 2 * half + 7).Select(i =>
                    edges ? ((i % 4) switch { 0 => 0u, 1 => 1u, 2 => modulus - 1, _ => modulus - 2 })
                          : (uint)random.NextInt64(modulus)).ToArray();
                uint[] original = (uint[])values.Clone();
                // An out-of-place buffer ends exactly at the valid prefix.
                uint[] output = inPlace ? values : Enumerable.Repeat(0xDEADBEEFu, writeRight ? half + last : half).ToArray();
                uint[] expected = (uint[])output.Clone();
                ulong twiddle = (uint)BigInteger.ModPow(root, first, modulus);
                for (int i = first; i < last; i++)
                {
                    ulong left = original[i];
                    ulong right = original[i + half] * twiddle % modulus;
                    expected[i] = (uint)((left + right) % modulus * scale % modulus);
                    if (writeRight)
                        expected[i + half] = (uint)((left + modulus - right) % modulus * scale % modulus);
                    twiddle = twiddle * root % modulus;
                }
                kernel(values, output, half, first, last, writeRight, modulus, root, scale, shoup, CancellationToken.None);
                if (!expected.AsSpan().SequenceEqual(output))
                    throw new InvalidOperationException($"Final prefix mismatch: p={modulus}, half={half}, first={first}, count={count}, right={writeRight}, inPlace={inPlace}, edges={edges}.");
                if (!inPlace && !values.AsSpan().SequenceEqual(original))
                    throw new InvalidOperationException("Final prefix modified its separate input.");
                cases++;
            }
        }
        Console.WriteLine($"PASS: {cases:N0} final inverse-prefix cases (independent modular reference, compact/in-place buffers, vector tails and both primes).");
    }

    public static void Benchmark()
    {
        PrefixKernel kernel = Bind();
        foreach ((uint modulus, uint generator) in new[] { (2_013_265_921u, 31u), (469_762_049u, 3u) })
        foreach (int half in new[] { 4096, 1 << 18 })
        foreach (bool writeRight in new[] { false, true })
        {
            uint root = (uint)BigInteger.ModPow(generator, modulus - 1 - (modulus - 1) / (2 * half), modulus);
            uint scale = (uint)BigInteger.ModPow(2 * half, modulus - 2, modulus);
            uint shoup = (uint)(((ulong)scale << 32) / modulus);
            var random = new Random(173);
            uint[] values = Enumerable.Range(0, 2 * half).Select(_ => (uint)random.NextInt64(modulus)).ToArray();
            uint[] output = new uint[writeRight ? 2 * half : half];
            int iterations = Math.Max(16, (1 << 22) / half);
            for (int i = 0; i < 32; i++)
                kernel(values, output, half, 0, half, writeRight, modulus, root, scale, shoup, CancellationToken.None);
            var times = new List<double>();
            for (int sample = 0; sample < 7; sample++)
            {
                long started = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++)
                    kernel(values, output, half, 0, half, writeRight, modulus, root, scale, shoup, CancellationToken.None);
                times.Add(Stopwatch.GetElapsedTime(started).TotalNanoseconds / (iterations * (double)half));
            }
            times.Sort();
            Console.WriteLine($"Prefix p={modulus}, half={half}, both={writeRight}: median={times[3]:F3} ns/butterfly");
        }
    }
}
