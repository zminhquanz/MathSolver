using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using MathSolver.Numerics;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
string command = args.ElementAtOrDefault(0) ?? "validate";
ulong basis = ulong.Parse(args.ElementAtOrDefault(1) ?? "999999999999999999");
int exponent = int.Parse(args.ElementAtOrDefault(2) ?? "1000000");
int workers = int.Parse(args.ElementAtOrDefault(3) ?? Environment.ProcessorCount.ToString());
int rounds = int.Parse(args.ElementAtOrDefault(4) ?? "1");
Console.WriteLine(JsonSerializer.Serialize(new { Kind = "machine", Utc = DateTime.UtcNow,
    Runtime = Environment.Version.ToString(), LogicalProcessors = Environment.ProcessorCount,
    Avx2 = Avx2.IsSupported, Avx512F = Avx512F.IsSupported, Avx512DQ = Avx512DQ.IsSupported,
    Vector256 = System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated,
    Vector512 = System.Runtime.Intrinsics.Vector512.IsHardwareAccelerated,
    NttAcceleration = MathSolver.Services.CalculationAccelerationManager.UsePowerNttAvx2,
    Mode = Environment.GetEnvironmentVariable("SIMD_AUDIT_MODE") ?? "auto",
    Chain = Environment.GetEnvironmentVariable("SIMD_AUDIT_CHAIN") ?? "production",
    Disabled = Environment.GetEnvironmentVariable("SIMD_AUDIT_DISABLE") ?? "", Workers = workers }));

if (command == "micro") { KernelAudit.Run(); return; }

ParallelPowerResult Power(ulong b, int e, int w, CancellationToken token = default, Action<int, int>? progress = null) => e > 10_000_000
    ? ParallelBigUnsigned.PowMemoryBounded(b, e, w, progress, token)
    : ParallelBigUnsigned.Pow(b, e, w, progress, token);

string Hash(ParallelBigUnsigned value)
{
    var type = typeof(ParallelBigUnsigned);
    var limbs = (uint[])type.GetField("_limbs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    int count = (int)type.GetField("_limbCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(limbs.AsSpan(0, count))));
}

if (command == "validate")
{
    var referenceHashes = new Dictionary<(ulong Base, int Exponent), string>();
    foreach (int w in new[] { 1, 3, workers }.Distinct())
    foreach (var (b, e) in new (ulong, int)[] { (0, 0), (0, 7), (ulong.MaxValue, 1), (1, 100), (3, 511), (3, 512), (3, 513),
        (ulong.MaxValue, 10000), (3, 40000), (999999999999999999, 100000), (3, 10000001), (3, 20000000) })
    {
        int lastDone = -1, lastTotal = -1;
        var actual = Power(b, e, w, progress: (done, total) =>
        {
            if (done < 0 || done > total) throw new Exception("Progress outside bounds");
            Interlocked.Exchange(ref lastDone, done);
            Interlocked.Exchange(ref lastTotal, total);
        }).Magnitude;
        if (!referenceHashes.TryGetValue((b, e), out string? expectedHash))
        {
            var expected = ParallelBigUnsigned.FromBigInteger(BigInteger.Pow(new BigInteger(b), e), actual.DigitCount, w, null, default);
            expectedHash = Hash(expected);
            referenceHashes.Add((b, e), expectedHash);
        }
        if (Hash(actual) != expectedHash) throw new Exception($"Incorrect {b}^{e}, workers={w}");
        if (e > 0 && (lastDone < 0 || lastDone != lastTotal)) throw new Exception("Incomplete progress");
        Console.WriteLine($"PASS {b}^{e}, workers={w}");
    }
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    foreach (int e in new[] { 100000, 10000001 })
    {
        try { Power(3, e, workers, cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { Console.WriteLine($"PASS cancellation {e}"); }
    }
    foreach (int e in new[] { 10000000, 100000000 })
    {
        using var midFlight = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        try { Power(999999999999999999, e, workers, midFlight.Token); throw new Exception("Mid-flight cancellation ignored"); }
        catch (OperationCanceledException) { Console.WriteLine($"PASS mid-flight cancellation {e}"); }
    }
    return;
}

// Warm compilation independently of the measured power. No formatting or hashing in the timer.
_ = Power(basis, 100000, workers);
for (int round = 0; round < rounds; round++)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var watch = Stopwatch.StartNew();
    var result = Power(basis, exponent, workers);
    watch.Stop();
    Console.WriteLine(JsonSerializer.Serialize(new { Kind = "power", Basis = basis, Exponent = exponent,
        Workers = workers, Round = round, Seconds = watch.Elapsed.TotalSeconds, Digits = result.Magnitude.DigitCount,
        Hash = Hash(result.Magnitude), Diagnostics = result.Diagnostics }));
}
