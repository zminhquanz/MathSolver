using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using MathSolver.Numerics;
using MathSolver.Services;

internal static class Program
{
    private delegate void Kernel(uint[] values, int offset, int end);

    private static readonly (uint Modulus, uint PrimitiveRoot)[] Primes =
        [(2_013_265_921, 31), (469_762_049, 3)];

    private static int Main(string[] args)
    {
        try
        {
            bool avx512 = Avx512F.IsSupported && Vector512.IsHardwareAccelerated;
            Console.WriteLine($"Runtime {Environment.Version}; logical processors {Environment.ProcessorCount}; " +
                              $"AVX2={Avx2.IsSupported}; AVX512F={Avx512F.IsSupported}; Vector512={Vector512.IsHardwareAccelerated}");
            if (!avx512 && args.Contains("--require-avx512"))
                throw new InvalidOperationException("AVX-512 hardware/runtime support is required for this run.");

            if (args.Contains("--power-benchmark"))
            {
                BenchmarkPower(args);
                return 0;
            }

            if (!args.Contains("--powers-only"))
            {
                ValidateKernels(avx512);
                if (avx512 && args.Contains("--benchmark"))
                    BenchmarkKernels();
            }

            if (!args.Contains("--kernels-only"))
                ValidatePowers();

            Console.WriteLine("PASS: all requested checks completed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ValidateKernels(bool avx512)
    {
        int cases = 0;
        foreach ((uint modulus, uint primitiveRoot) in Primes)
        {
            uint forwardRoot = ModPow(primitiveRoot, (modulus - 1) / 4, modulus);
            uint inverseRoot = ModPow(forwardRoot, modulus - 2, modulus);
            Kernel scalarForward = BindKernel(false, false, modulus, forwardRoot);
            Kernel scalarInverse = BindKernel(true, false, modulus, inverseRoot);
            Kernel forward = avx512 ? BindKernel(false, true, modulus, forwardRoot) : scalarForward;
            Kernel inverse = avx512 ? BindKernel(true, true, modulus, inverseRoot) : scalarInverse;
            uint[] edges = [0, 1, 2, modulus / 2, modulus / 2 + 1, modulus - 2, modulus - 1];

            // Enumerate every four-value combination. Packing unlike groups
            // together also detects accidental cross-group lane shuffles.
            var combinations = new List<uint>();
            foreach (uint a in edges)
            foreach (uint b in edges)
            foreach (uint c in edges)
            foreach (uint d in edges)
                combinations.AddRange([a, b, c, d]);

            foreach (int offset in new[] { 0, 1, 3, 4, 7, 15, 16, 31 })
            {
                uint[] values = Guarded(combinations.Count, offset);
                combinations.CopyTo(values, offset);
                Check(values, offset, offset + combinations.Count);
            }

            var random = new Random(unchecked((int)modulus));
            foreach (int length in new[] { 0, 4, 8, 12, 16, 20, 28, 32, 36, 60, 64, 68, 124, 128, 132,
                                          252, 256, 260, 1024, 2048, 4092, 4096, 4100, 8192, 65536 })
            foreach (int offset in new[] { 0, 1, 3, 4, 7, 15, 16, 31 })
            {
                for (int pattern = 0; pattern < 8; pattern++)
                {
                    uint[] values = Guarded(length, offset);
                    for (int i = 0; i < length; i++)
                    {
                        values[offset + i] = pattern switch
                        {
                            0 => 0,
                            1 => modulus - 1,
                            2 => (i & 1) == 0 ? 0 : modulus - 1,
                            3 => edges[i % edges.Length],
                            4 => (uint)i % modulus,
                            _ => (uint)random.NextInt64(modulus)
                        };
                    }
                    Check(values, offset, offset + length);
                }
            }

            void Check(uint[] original, int offset, int end)
            {
                foreach (bool isInverse in new[] { false, true })
                {
                    uint[] expected = (uint[])original.Clone();
                    ReferenceDft(expected, offset, end, modulus,
                        isInverse ? inverseRoot : forwardRoot, isInverse);
                    uint[] scalar = (uint[])original.Clone();
                    (isInverse ? scalarInverse : scalarForward)(scalar, offset, end);
                    Equal(expected, scalar, $"scalar inverse={isInverse}, p={modulus}, offset={offset}, length={end - offset}");
                    if (avx512)
                    {
                        uint[] actual = (uint[])original.Clone();
                        (isInverse ? inverse : forward)(actual, offset, end);
                        Equal(expected, actual, $"AVX512 inverse={isInverse}, p={modulus}, offset={offset}, length={end - offset}");
                    }
                }

                uint[] roundTrip = (uint[])original.Clone();
                forward(roundTrip, offset, end);
                inverse(roundTrip, offset, end);
                uint[] scaled = (uint[])original.Clone();
                for (int i = offset; i < end; i++)
                    scaled[i] = (uint)((ulong)scaled[i] * 4 % modulus);
                Equal(scaled, roundTrip, $"forward/inverse round trip p={modulus}, offset={offset}, length={end - offset}");
                cases++;
            }
        }

        Console.WriteLine($"PASS: {cases:N0} kernel cases (independent DFT, scalar comparison, round trip, guards); " +
                          (avx512 ? "AVX-512 executed for both primes." : "AVX-512 checks SKIPPED: unsupported; scalar reference checked."));
    }

    private static uint[] Guarded(int length, int offset)
    {
        uint[] values = new uint[offset + length + 35];
        for (int i = 0; i < values.Length; i++)
            values[i] = 0xDAD00000u + (uint)i;
        return values;
    }

    // Evaluate a four-point DFT directly using modular exponentiation. The
    // production forward DIF output and inverse DIT input are bit-reversed.
    private static void ReferenceDft(uint[] values, int offset, int end, uint modulus, uint root, bool inverse)
    {
        ReadOnlySpan<int> bitReverse = [0, 2, 1, 3];
        Span<uint> source = stackalloc uint[4];
        Span<uint> output = stackalloc uint[4];
        for (int start = offset; start < end; start += 4)
        {
            for (int n = 0; n < 4; n++)
                source[n] = values[start + (inverse ? bitReverse[n] : n)];
            for (int k = 0; k < 4; k++)
            {
                ulong sum = 0;
                for (int n = 0; n < 4; n++)
                    sum = (sum + (ulong)source[n] * ModPow(root, (uint)(n * k), modulus)) % modulus;
                output[inverse ? k : bitReverse[k]] = (uint)sum;
            }
            output.CopyTo(values.AsSpan(start, 4));
        }
    }

    private static Kernel BindKernel(bool inverse, bool avx512, uint modulus, uint root)
    {
        string name = inverse ? "ExecuteInverseLengthTwoAndFourFusedBlock" : "ExecuteForwardLengthFourAndTwoFusedBlock";
        name += avx512 ? "Avx512" : inverse ? "" : "Shoup";
        MethodInfo method = typeof(ParallelBigUnsigned).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == (avx512 ? 7 : 6));
        ParameterExpression values = Expression.Parameter(typeof(uint[]), "values");
        ParameterExpression offset = Expression.Parameter(typeof(int), "offset");
        ParameterExpression end = Expression.Parameter(typeof(int), "end");
        var arguments = new List<Expression>
        {
            values, Expression.Constant(modulus), Expression.Constant(root),
            Expression.Constant((uint)(((ulong)root << 32) / modulus)), offset, end
        };
        if (avx512)
        {
            Type contextType = method.GetParameters()[6].ParameterType.GetElementType()!;
            object context = Activator.CreateInstance(contextType, modulus)!;
            arguments.Add(Expression.Constant(context, contextType));
        }
        return Expression.Lambda<Kernel>(Expression.Call(method, arguments), values, offset, end).Compile();
    }

    private static void ValidatePowers()
    {
        (ulong Base, int Exponent)[] examples =
        [
            (0, 0), (0, 17), (1, 42), (2, 4096), (123_456_789, 321),
            (3, 40_000), (9_999, 20_000), (123_456_789, 10_000), (ulong.MaxValue, 4096),
            (999_999_999_999_999_999, 100_000)
        ];
        int checkedPowers = 0;
        int nttPowers = 0;
        int acceleratedL2Powers = 0;
        foreach ((ulong baseValue, int exponent) in examples)
        {
            BigInteger expected = BigInteger.Pow(new BigInteger(baseValue), exponent);
            foreach (int workers in new[] { 1, 3, 4, Math.Min(24, Environment.ProcessorCount) }.Distinct())
            foreach (bool acceleration in new[] { false, true })
            {
                CalculationAccelerationManager.Enabled = acceleration;
                ParallelPowerResult result = ParallelBigUnsigned.Pow(baseValue, exponent, workers, null, CancellationToken.None);
                BigInteger actual = ToBigInteger(result.Magnitude);
                if (actual != expected)
                    throw new InvalidOperationException($"Power mismatch: {baseValue}^{exponent}, workers={workers}, acceleration={acceleration}.");
                if (result.Diagnostics.NttMultiplicationCount > 0)
                    nttPowers++;
                if (acceleration && result.Diagnostics.InverseL1Radix4Tail > TimeSpan.Zero)
                    acceleratedL2Powers++;
                checkedPowers++;
            }
        }
        if (nttPowers == 0)
            throw new InvalidOperationException("Power checks failed to exercise the NTT multiplication path.");
        if (Avx2.IsSupported && acceleratedL2Powers == 0)
            throw new InvalidOperationException("Power checks failed to exercise the profiled L2 path containing the radix-4 dispatch.");
        Console.WriteLine($"PASS: {checkedPowers} powers equal BigInteger; {nttPowers} use NTT; " +
                          $"{acceleratedL2Powers} accelerated powers exercise profiled L2 radix-4; acceleration on/off and varied worker counts.");
    }

    private static void BenchmarkKernels()
    {
        Console.WriteLine("Kernel benchmark: median of 7 alternating samples; buffers reused and setup excluded.");
        foreach ((uint modulus, uint primitiveRoot) in Primes)
        foreach (bool inverse in new[] { false, true })
        foreach (int length in new[] { 2048, 4096, 8192, 65536 })
        {
            uint root = ModPow(primitiveRoot, (modulus - 1) / 4, modulus);
            if (inverse)
                root = ModPow(root, modulus - 2, modulus);
            Kernel scalar = BindKernel(inverse, false, modulus, root);
            Kernel vector = BindKernel(inverse, true, modulus, root);
            var random = new Random(173);
            uint[] original = Enumerable.Range(0, length).Select(_ => (uint)random.NextInt64(modulus)).ToArray();
            uint[] scalarValues = (uint[])original.Clone();
            uint[] vectorValues = (uint[])original.Clone();
            int iterations = Math.Max(64, 4_194_304 / length);
            for (int i = 0; i < 256; i++)
            {
                scalar(scalarValues, 0, length);
                vector(vectorValues, 0, length);
            }
            var scalarTimes = new List<double>();
            var vectorTimes = new List<double>();
            for (int sample = 0; sample < 7; sample++)
            {
                Array.Copy(original, scalarValues, length);
                Array.Copy(original, vectorValues, length);
                foreach (bool runVector in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    Kernel kernel = runVector ? vector : scalar;
                    uint[] values = runVector ? vectorValues : scalarValues;
                    long started = Stopwatch.GetTimestamp();
                    for (int iteration = 0; iteration < iterations; iteration++)
                        kernel(values, 0, length);
                    double nanosecondsPerValue = Stopwatch.GetElapsedTime(started).TotalNanoseconds / (iterations * (double)length);
                    (runVector ? vectorTimes : scalarTimes).Add(nanosecondsPerValue);
                }
                Equal(scalarValues, vectorValues, "Repeated benchmark kernel output");
            }
            double scalarMedian = Median(scalarTimes);
            double vectorMedian = Median(vectorTimes);
            Console.WriteLine($"p={modulus}, {(inverse ? "inverse" : "forward")}, n={length}: scalar={scalarMedian:F3} ns/value, " +
                              $"AVX512={vectorMedian:F3} ns/value, speedup={scalarMedian / vectorMedian:F2}x");
        }
    }

    private static void BenchmarkPower(string[] args)
    {
        int index = Array.IndexOf(args, "--power-benchmark");
        ulong baseValue = args.Length > index + 1 ? ulong.Parse(args[index + 1], CultureInfo.InvariantCulture) : 3;
        int exponent = args.Length > index + 2 ? int.Parse(args[index + 2], CultureInfo.InvariantCulture) : 1_000_000;
        int workers = args.Length > index + 3 ? int.Parse(args[index + 3], CultureInfo.InvariantCulture) : Environment.ProcessorCount;
        int samples = args.Length > index + 4 ? int.Parse(args[index + 4], CultureInfo.InvariantCulture) : 5;
        CalculationAccelerationManager.Enabled = true;
        // Warm the actual workload so cold JIT/table paths are not part of the
        // timed samples. Each Pow still constructs its own normal workspace.
        _ = ParallelBigUnsigned.Pow(baseValue, exponent, workers, null, CancellationToken.None);
        var times = new List<double>();
        for (int sample = 0; sample < samples; sample++)
        {
            long started = Stopwatch.GetTimestamp();
            ParallelPowerResult result = ParallelBigUnsigned.Pow(baseValue, exponent, workers, null, CancellationToken.None);
            double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            times.Add(milliseconds);
            ParallelPowerDiagnostics d = result.Diagnostics;
            (uint[] limbs, int count) = GetLimbs(result.Magnitude);
            string hash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(limbs.AsSpan(0, count))));
            Console.WriteLine($"sample={sample + 1}, base={baseValue}, exponent={exponent}, workers={workers}, " +
                              $"elapsed={milliseconds:F3} ms, digits={result.Magnitude.DigitCount}, " +
                              $"forwardL1={d.ForwardLocalL1.TotalMilliseconds:F3} ms, inverseL1={d.InverseLocalL1.TotalMilliseconds:F3} ms, " +
                              $"inverseRadix4={d.InverseL1Radix4Tail.TotalMilliseconds:F3} ms, limbsSHA256={hash}");
            foreach (PropertyInfo property in typeof(ParallelPowerDiagnostics).GetProperties())
                if (property.PropertyType == typeof(TimeSpan))
                    Console.WriteLine($"  {property.Name}={((TimeSpan)property.GetValue(d)!).TotalMilliseconds:F3} ms");
        }
        Console.WriteLine($"Power median={Median(times):F3} ms; run A/B using the same runtime, worker budget and power inputs.");
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    private static (uint[] Limbs, int Count) GetLimbs(ParallelBigUnsigned magnitude) =>
        ((uint[])typeof(ParallelBigUnsigned).GetField("_limbs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(magnitude)!,
         (int)typeof(ParallelBigUnsigned).GetField("_limbCount", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(magnitude)!);

    // Exact conversion through a balanced tree keeps large BigInteger checks
    // practical without depending on the production decimal formatter/importer.
    private static BigInteger ToBigInteger(ParallelBigUnsigned magnitude)
    {
        (uint[] limbs, int count) = GetLimbs(magnitude);
        var powers = new Dictionary<int, BigInteger>();
        return ConvertRange(0, count);

        BigInteger ConvertRange(int offset, int length)
        {
            if (length <= 32)
            {
                BigInteger value = 0;
                for (int i = offset + length - 1; i >= offset; i--)
                    value = value * 10_000 + limbs[i];
                return value;
            }
            int lowLength = length / 2;
            if (!powers.TryGetValue(lowLength, out BigInteger power))
            {
                power = BigInteger.Pow(10_000, lowLength);
                powers.Add(lowLength, power);
            }
            return ConvertRange(offset, lowLength) +
                   ConvertRange(offset + lowLength, length - lowLength) * power;
        }
    }

    private static uint ModPow(uint value, uint exponent, uint modulus)
    {
        ulong result = 1;
        ulong factor = value;
        while (exponent != 0)
        {
            if ((exponent & 1) != 0)
                result = result * factor % modulus;
            factor = factor * factor % modulus;
            exponent >>= 1;
        }
        return (uint)result;
    }

    private static void Equal(uint[] expected, uint[] actual, string label)
    {
        for (int i = 0; i < expected.Length; i++)
            if (expected[i] != actual[i])
                throw new InvalidOperationException($"{label}: index {i}, expected {expected[i]}, actual {actual[i]}.");
    }
}
