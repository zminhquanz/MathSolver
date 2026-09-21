using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using MathSolver.Numerics;
using MathSolver.Services;

Console.WriteLine($"CPU SSE2={Sse2.IsSupported} SSE3={Sse3.IsSupported} SSSE3={Ssse3.IsSupported} SSE41={Sse41.IsSupported} SSE42={Sse42.IsSupported} AVX={Avx.IsSupported} AVX2={Avx2.IsSupported}");
if (!Sse2.IsSupported) throw new Exception("SSE2 unavailable");
if (Environment.GetEnvironmentVariable("DOTNET_EnableSSE42") == "0" &&
    (Sse3.IsSupported || Ssse3.IsSupported || Sse41.IsSupported || Sse42.IsSupported || Avx.IsSupported))
    throw new Exception("SSE2 mask was not applied");
if (args.FirstOrDefault() != "bench") Console.WriteLine($"PASS {ParallelBigUnsigned.ValidateSseKernels()} modular/block checks");
int exponent = args.Length > 1 ? int.Parse(args[1]) : 20000;
int workers = args.Length > 2 ? int.Parse(args[2]) : 2;
ulong baseValue = args.Length > 3 ? ulong.Parse(args[3]) : 999999999999999999UL;
int rounds = args.FirstOrDefault() == "bench" ? (args.Length > 4 ? int.Parse(args[4]) : 3) : 1;
string? expected = null;
for (int round = 0; round < rounds; round++)
foreach (bool sse in round % 2 == 0 ? new[] { false, true } : new[] { true, false })
{
    CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.Sse);
    CalculationAccelerationManager.SetUseSimd(sse);
    if (CalculationAccelerationManager.UsePowerNttSse != sse || CalculationAccelerationManager.UsePowerNttAvx2)
        throw new Exception("Dispatch policy");
    var timer = Stopwatch.StartNew();
    var result = ParallelBigUnsigned.Pow(baseValue, exponent, workers, null, default);
    double seconds = timer.Elapsed.TotalSeconds;
    Console.WriteLine($"COMPUTE round={round} sse={sse} seconds={seconds:F4} forward={result.Diagnostics.ForwardTransform.TotalSeconds:F4} inverse={result.Diagnostics.InverseTransform.TotalSeconds:F4}");
    string text = result.Magnitude.ToDecimalString();
    expected ??= text;
    if (text != expected || result.Diagnostics.UsedSseNttButterflies != sse || result.Diagnostics.UsedAvx2NttButterflies)
        throw new Exception("Whole-power mismatch or incorrect backend");
    if (args.FirstOrDefault() != "bench" && text.Length <= 400000 &&
        text != BigInteger.Pow(baseValue, exponent).ToString())
        throw new Exception("BigInteger mismatch");
    foreach (uint prime in new uint[] { 1000000007, 1000000009, 4294967291 })
    {
        ulong remainder = 0;
        foreach (char digit in text) remainder = (remainder * 10 + (uint)(digit - '0')) % prime;
        if (remainder != (ulong)BigInteger.ModPow(baseValue, exponent, prime))
            throw new Exception("Independent modular check");
    }
    Console.WriteLine($"round={round} sse={sse} base={baseValue} exponent={exponent} workers={workers} seconds={seconds:F4} forward={result.Diagnostics.ForwardTransform.TotalSeconds:F4} inverse={result.Diagnostics.InverseTransform.TotalSeconds:F4} ntt={result.Diagnostics.NttMultiplicationCount} sha256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}");
}
if (args.FirstOrDefault() != "bench")
{
    CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.Auto);
    CalculationAccelerationManager.SetUseSimd(true);
    var auto = ParallelBigUnsigned.Pow(12345, 4000, 2, null, default);
    if (auto.Magnitude.ToDecimalString() != BigInteger.Pow(12345, 4000).ToString() ||
        auto.Diagnostics.UsedSseNttButterflies != CalculationAccelerationManager.UsePowerNttSse)
        throw new Exception("Auto backend result/diagnostics");
    CalculationAccelerationManager.SetSelectedSimdMode(CalculationSimdMode.Sse);
    var outsideScope = ParallelBigUnsigned.Pow(1, 10_000_001, 1, null, default);
    if (outsideScope.Diagnostics.UsedSseNttButterflies) throw new Exception("SSE enabled above 10M");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        ParallelBigUnsigned.Pow(12345, 4000, 2, null, cancellation.Token);
        throw new Exception("Cancellation ignored");
    }
    catch (OperationCanceledException) { }
    Console.WriteLine("PASS Auto, >10M scope and cancellation");
}
