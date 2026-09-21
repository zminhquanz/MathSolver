using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using MathSolver.Numerics;

internal static class KernelAudit
{
    const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly Type Engine = typeof(ParallelBigUnsigned);

    // Compile a direct call once. Reflection, context construction and table preparation
    // are outside timing. This also works against an unmodified production source file.
    static Action Bind(string name, Dictionary<string, object> arguments)
    {
        var method = Engine.GetMethods(PrivateStatic).Single(m => m.Name == name &&
            m.GetParameters().All(p => p.Name == "context" || arguments.ContainsKey(p.Name!)));
        var variables = new List<ParameterExpression>();
        var setup = new List<Expression>();
        var parameters = method.GetParameters().Select(p =>
        {
            if (p.Name != "context") return (Expression)Expression.Constant(arguments[p.Name!], p.ParameterType);
            var type = p.ParameterType.GetElementType()!;
            var variable = Expression.Variable(type);
            variables.Add(variable);
            setup.Add(Expression.Assign(variable, Expression.New(type.GetConstructor(new[] { typeof(uint) })!,
                Expression.Constant((uint)arguments["modulus"]))));
            return (Expression)variable;
        }).ToArray();
        setup.Add(Expression.Call(method, parameters));
        // Context construction is a few vector broadcasts; keep it outside each timed call.
        var action = Expression.Lambda<Action>(Expression.Call(method, parameters));
        var factory = Expression.Lambda<Func<Action>>(Expression.Block(variables, setup.Take(setup.Count - 1).Append(action)));
        return factory.Compile()();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Add(uint a, uint b, uint p) { uint s = a + b; return s >= p ? s - p : s; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Sub(uint a, uint b, uint p) => a >= b ? a - b : a + p - b;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Shoup(uint a, uint b, uint s, uint p)
    {
        uint q = (uint)(((ulong)a * s) >> 32);
        uint r = unchecked(a * b - q * p);
        return r >= p ? r - p : r;
    }
    static uint Pow(uint a, uint n, uint p)
    {
        ulong r = 1, x = a;
        for (; n != 0; n >>= 1, x = x * x % p) if ((n & 1) != 0) r = r * x % p;
        return (uint)r;
    }

    // Fused scalar comparator: same two-stage data traffic and Shoup companions.
    // The separate modular reference below checks it independently.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    static void ScalarPair(uint[] v, uint p, uint[] w, uint[] sh, int offset, int length, int s, bool inverse)
    {
        int q = inverse ? s / 2 : s / 4;
        int second = s / 2;
        for (int group = offset; group < offset + length; group += 4 * q)
        for (int i = 0; i < q; i++)
        {
            int j = group + i;
            uint a = v[j], b = v[j + q], c = v[j + 2 * q], d = v[j + 3 * q];
            if (!inverse)
            {
                uint x = Add(a, c, p), y = Add(b, d, p);
                uint z = Shoup(Sub(a, c, p), w[i], sh[i], p);
                uint t = Shoup(Sub(b, d, p), w[q + i], sh[q + i], p);
                v[j] = Add(x, y, p);
                v[j + q] = Shoup(Sub(x, y, p), w[second + i], sh[second + i], p);
                v[j + 2 * q] = Add(z, t, p);
                v[j + 3 * q] = Shoup(Sub(z, t, p), w[second + i], sh[second + i], p);
            }
            else
            {
                b = Shoup(b, w[i], sh[i], p); d = Shoup(d, w[i], sh[i], p);
                uint x = Add(a, b, p), y = Sub(a, b, p);
                uint z = Shoup(Add(c, d, p), w[second + i], sh[second + i], p);
                uint t = Shoup(Sub(c, d, p), w[second + q + i], sh[second + q + i], p);
                v[j] = Add(x, z, p); v[j + 2 * q] = Sub(x, z, p);
                v[j + q] = Add(y, t, p); v[j + 3 * q] = Sub(y, t, p);
            }
        }
    }

    static void Reference(uint[] v, uint p, uint[] w, int offset, int length, int s, bool inverse)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            int stage = pass == 0 ? s : inverse ? s * 2 : s / 2;
            int twiddle = pass == 0 ? 0 : s / 2;
            int half = stage / 2;
            for (int g = offset; g < offset + length; g += stage)
            for (int i = 0; i < half; i++)
            {
                uint a = v[g + i], b = v[g + i + half];
                if (inverse) b = (uint)((ulong)b * w[twiddle + i] % p);
                v[g + i] = Add(a, b, p);
                uint d = Sub(a, b, p);
                v[g + i + half] = inverse ? d : (uint)((ulong)d * w[twiddle + i] % p);
            }
        }
    }

    public static void Run()
    {
        PointwiseAndCrt();
        foreach (var (p, root) in new (uint, uint)[] { (2013265921, 31), (469762049, 3) })
        foreach (bool inverse in new[] { false, true })
        foreach (var (label, length, parent) in new[] {
            ("packed-L1",4096,16), ("generic-L1",4096,256), ("top-L1",4096,4096),
            ("lower-L2",65536,16384), ("top-L2",65536,65536), ("L3",262144,262144) })
        {
            int stage = inverse ? parent / 2 : parent;
            uint[] w = new uint[parent * 2], sh = new uint[parent * 2];
            for (int pass = 0; pass < 2; pass++)
            {
                int s = pass == 0 ? stage : inverse ? stage * 2 : stage / 2;
                int start = pass == 0 ? 0 : stage / 2;
                uint step = Pow(root, (p - 1) / (uint)s, p);
                if (inverse) step = Pow(step, p - 2, p);
                uint current = 1;
                for (int i = 0; i < s / 2; i++)
                {
                    w[start + i] = current; sh[start + i] = (uint)(((ulong)current << 32) / p);
                    current = (uint)((ulong)current * step % p);
                }
            }
            var names = new List<(string Label, string? Method)> { ("scalar-fused-Shoup", null) };
            string prefix = "Execute" + (inverse ? "Inverse" : "Forward") + "CachedStagePairRegion";
            string suffix = inverse ? "TwiddleMajor" : "TwiddleMajorBounded";
            if (Avx2.IsSupported)
            {
                names.Add(("AVX2", prefix + suffix + "Avx2"));
                names.Add(("AVX2-Low32", prefix + suffix + "Low32Avx2"));
            }
            if (Avx512F.IsSupported)
            {
                names.Add(("AVX512", prefix + (parent == 16 ? inverse ? "Half4" : "Quarter4" : suffix) + "Avx512"));
                if (parent != 16) names.Add(("AVX512-Low32", prefix + suffix + "Low32Avx512"));
            }
            // Odd offsets and untouched guards are verified before throughput measurement.
            foreach (int offset in new[] { 0, 1, 15 })
            {
                var random = new Random(177 + offset);
                uint[] input = Enumerable.Range(0, length + offset + 17).Select(i => i % 8 == 0 ? p - 1 : (uint)random.NextInt64(p)).ToArray();
                uint[] expected = (uint[])input.Clone();
                Reference(expected, p, w, offset, length, stage, inverse);
                var variants = names.Select(n =>
                {
                    uint[] v = (uint[])input.Clone();
                    var parameters = new Dictionary<string, object> { ["values"] = v, ["modulus"] = p,
                        ["twiddles"] = w, ["shoupTwiddles"] = sh, ["firstTwiddleOffset"] = 0,
                        ["secondTwiddleOffset"] = stage / 2, ["regionOffset"] = offset,
                        ["regionLength"] = length, ["stageLength"] = stage };
                    Action call = n.Method == null ? () => ScalarPair(v, p, w, sh, offset, length, stage, inverse) : Bind(n.Method, parameters);
                    call();
                    if (!v.AsSpan().SequenceEqual(expected)) throw new Exception($"Kernel mismatch {label}/{inverse}/{p}/{offset}/{n.Label}");
                    return (n.Label, Values: v, Call: call, Samples: new List<double>());
                }).ToArray();
                if (offset != 0) continue;
                int iterations = Math.Max(4, 1048576 / length);
                for (int round = 0; round < 7; round++)
                {
                    uint[]? repeated = null;
                    foreach (var variant in round % 2 == 0 ? variants : variants.Reverse())
                    {
                        input.CopyTo(variant.Values, 0);
                        var watch = Stopwatch.StartNew();
                        for (int i = 0; i < iterations; i++) variant.Call();
                        watch.Stop();
                        variant.Samples.Add(watch.Elapsed.TotalMicroseconds / iterations);
                        if (repeated != null && !variant.Values.AsSpan().SequenceEqual(repeated)) throw new Exception("Repeated kernel mismatch");
                        repeated = variant.Values;
                    }
                }
                foreach (var variant in variants)
                    Console.WriteLine(JsonSerializer.Serialize(new { Kind = "kernel", Case = label, Inverse = inverse,
                        Modulus = p, Length = length, Stage = stage, Variant = variant.Label,
                        MedianUs = variant.Samples.Order().ElementAt(3), SamplesUs = variant.Samples }));
            }
        }
    }

    delegate void CrtKernel(ReadOnlySpan<uint> first, ReadOnlySpan<uint> second, Span<ulong> output, bool vector);

    static void PointwiseAndCrt()
    {
        foreach (uint p in new uint[] { 2013265921, 469762049 })
        foreach (bool square in new[] { false, true })
        foreach (int count in new[] { 8, 16, 64, 4096, 1048576 })
        {
            var random = new Random(122);
            uint[] left = Enumerable.Range(0, count + 3).Select(i => (i % 8) switch {
                0 => 0u, 1 => p - 1, 2 => 1u, _ => (uint)random.NextInt64(p) }).ToArray();
            uint[] right = square ? left : Enumerable.Range(0, count + 3).Select(i => (i % 8) switch {
                0 => p - 1, 1 => p - 1, 2 => 0u, _ => (uint)random.NextInt64(p) }).ToArray();
            uint[] scalar = new uint[count + 3], vector = new uint[count + 3];
            Action scalarCall = () => { for (int i = 1; i <= count; i++) scalar[i] = (uint)((ulong)left[i] * right[i] % p); };
            var names = new Dictionary<string, object> { ["destination"] = vector, ["left"] = left,
                ["right"] = right, ["source"] = left, ["start"] = 1, ["end"] = count + 1 };
            if (!Avx512F.IsSupported) continue;
            Action vectorCall = Bind("ProcessPointwise" + (square ? "Square" : "Product") + "Range" +
                (p == 2013265921 ? "First" : "Second") + "ModulusDualVectorIlp", names);
            scalarCall(); vectorCall();
            if (!scalar.AsSpan().SequenceEqual(vector)) throw new Exception("Pointwise mismatch");
            MeasureActions(square ? "pointwise-square" : "pointwise-product", p, count,
                ("scalar", scalarCall), (Avx512DQ.IsSupported ? "AVX512F-DQ" : "AVX512F", vectorCall));
            // Exercise aliased destination, including an odd start and scalar remainder.
            uint[] aliased = (uint[])left.Clone();
            names["destination"] = aliased; names["source"] = aliased; names["left"] = aliased;
            Bind("ProcessPointwise" + (square ? "Square" : "Product") + "Range" +
                (p == 2013265921 ? "First" : "Second") + "ModulusDualVectorIlp", names)();
            if (!aliased.AsSpan(1, count).SequenceEqual(scalar.AsSpan(1, count))) throw new Exception("Aliased pointwise mismatch");
        }

        var crt = Engine.GetMethod("ReconstructCrtRange", PrivateStatic)!.CreateDelegate<CrtKernel>();
        foreach (int count in new[] { 8, 16, 64, 4096, 1048576 })
        {
            var random = new Random(999);
            uint[] first = Enumerable.Range(0, count + 3).Select(_ => (uint)random.NextInt64(2013265921)).ToArray();
            uint[] second = Enumerable.Range(0, count + 3).Select(_ => (uint)random.NextInt64(469762049)).ToArray();
            ulong[] scalar = new ulong[count + 3], vector = new ulong[count + 3];
            Action scalarCall = () => crt(first.AsSpan(1, count), second.AsSpan(1, count), scalar.AsSpan(1, count), false);
            Action vectorCall = () => crt(first.AsSpan(1, count), second.AsSpan(1, count), vector.AsSpan(1, count), true);
            if (!Avx512DQ.IsSupported) continue;
            scalarCall(); vectorCall();
            if (!scalar.AsSpan().SequenceEqual(vector)) throw new Exception("CRT mismatch");
            for (int i = 1; i <= count; i++)
                if (vector[i] >= (ulong)2013265921 * 469762049 || vector[i] % 2013265921 != first[i] || vector[i] % 469762049 != second[i])
                    throw new Exception("CRT residue or canonical range mismatch");
            MeasureActions("CRT", 0, count, ("scalar", scalarCall), ("AVX512DQ", vectorCall));
        }
    }

    static void MeasureActions(string label, uint modulus, int length, params (string Name, Action Call)[] variants)
    {
        var samples = variants.Select(_ => new List<double>()).ToArray();
        int iterations = Math.Max(4, 1048576 / length);
        for (int round = 0; round < 7; round++)
        foreach (int i in round % 2 == 0 ? Enumerable.Range(0, variants.Length) : Enumerable.Range(0, variants.Length).Reverse())
        {
            var watch = Stopwatch.StartNew();
            for (int j = 0; j < iterations; j++) variants[i].Call();
            watch.Stop();
            samples[i].Add(watch.Elapsed.TotalMicroseconds / iterations);
        }
        for (int i = 0; i < variants.Length; i++)
            Console.WriteLine(JsonSerializer.Serialize(new { Kind = "kernel", Case = label, Modulus = modulus,
                Length = length, Variant = variants[i].Name, MedianUs = samples[i].Order().ElementAt(3), SamplesUs = samples[i] }));
    }
}
