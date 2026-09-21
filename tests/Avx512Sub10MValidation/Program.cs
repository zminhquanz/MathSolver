using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using MathSolver.Numerics;
using MathSolver.Services;

Console.WriteLine($"Runtime={Environment.Version} AVX512F={Avx512F.IsSupported} AVX512DQ={Avx512DQ.IsSupported} CPU={Environment.ProcessorCount}");
if (!Avx512F.IsSupported) throw new PlatformNotSupportedException("AVX-512 required for this audit.");
CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.Avx512);
CalculationAccelerationManager.SetUseSimd(true);
if (args.FirstOrDefault() == "bench")
{
    int exponent = int.Parse(args[1]);
    int workers = int.Parse(args[2]);
    const ulong basis = 999999999999999999;
    _ = ParallelBigUnsigned.Pow(basis, 100000, workers, null, default);
    var watch = Stopwatch.StartNew();
    var result = ParallelBigUnsigned.Pow(basis, exponent, workers, null, default);
    watch.Stop();
    Console.WriteLine($"POWER exponent={exponent} workers={workers} seconds={watch.Elapsed.TotalSeconds:F6} hash={result.Magnitude.CheckAndHash(basis, exponent)}");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result.Diagnostics));
}
else
{
    ParallelBigUnsigned.ValidateUncachedStage();
    foreach (var (basis, exponent) in new (ulong,int)[] { (2,0), (3,12345), (999999999999999999,4000) })
    {
        var result = ParallelBigUnsigned.Pow(basis, exponent, 3, null, default);
        if (result.Magnitude.ToDecimalString() != BigInteger.Pow(basis, exponent).ToString())
            throw new Exception("BigInteger oracle mismatch");
    }
    Console.WriteLine("PASS whole-power BigInteger oracles");
}
