using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using MathSolver.Numerics;

string command = args.ElementAtOrDefault(0) ?? "bench";
string? cpu = Environment.GetEnvironmentVariable("SINGLE_POWER_CPU");
if (cpu is not null && OperatingSystem.IsWindows())
    Process.GetCurrentProcess().ProcessorAffinity = (nint)(1L << int.Parse(cpu));
long basis = long.Parse(args.ElementAtOrDefault(1) ?? "3");
int exponent = int.Parse(args.ElementAtOrDefault(2) ?? "1000000");
int rounds = int.Parse(args.ElementAtOrDefault(3) ?? "5");
string[] modes = (args.ElementAtOrDefault(4) ??
    (command == "validate" ? "runtime,simd,old-simd" : "old-scalar,old-simd,runtime,simd")).Split(',');
int warmupExponent = int.Parse(args.ElementAtOrDefault(5) ?? "10000");
void Print(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
Print(new { Kind = "machine", Runtime = Environment.Version.ToString(), Avx2 = Avx2.IsSupported,
    Avx512F = Avx512F.IsSupported, Avx512BW = Avx512BW.IsSupported, Thread = Environment.CurrentManagedThreadId, Cpu=cpu });
int Count(int e) => e == 0 ? 0 : BitOperations.Log2((uint)e) + BitOperations.PopCount((uint)e) - 1;
BigInteger OldScalar(long b, int e, Action<int,int> p, CancellationToken token)
{
    BigInteger factor = new(b), result = BigInteger.One;
    bool initialized = false;
    int total = Count(e), done = 0;
    while (e > 0)
    {
        token.ThrowIfCancellationRequested();
        if ((e & 1) != 0)
        {
            if (!initialized) { result = factor; initialized = true; }
            else { result *= factor; p(++done, total); }
        }
        e >>= 1;
        if (e > 0) { factor *= factor; p(++done, total); }
    }
    token.ThrowIfCancellationRequested();
    return result;
}
BigInteger Power(string mode, long b, int e, Action<int,int> p, CancellationToken t = default) => mode switch
{
    "old-scalar" => OldScalar(b,e,p,t),
    "old-simd" => LegacyAvx2BigIntegerPower.Pow(b,e,p,Count(e),t),
    "runtime" => SingleThreadBigIntegerPower.Pow(b,e,p,Count(e),t,false),
    "runtime-prefix" => RuntimePrefixPower.Pow(b,e,p,Count(e),t,false),
    "immediate" => ImmediateRuntimePower.Pow(b,e,p,Count(e),t,false),
    "simd" => SingleThreadBigIntegerPower.Pow(b,e,p,Count(e),t,true),
    "scalar-window" => ScalarWindowPower.Pow(b,e,p,Count(e),t,true),
    "cut64" => Cutoff64Power.Pow(b,e,p,Count(e),t,true),
    "cut128" => Cutoff128Power.Pow(b,e,p,Count(e),t,true),
    "cut256" => Cutoff256Power.Pow(b,e,p,Count(e),t,true),
    "cut512" => Cutoff512Power.Pow(b,e,p,Count(e),t,true),
    "win3" => Window3Power.Pow(b,e,p,Count(e),t,true),
    "win4" => Window4Power.Pow(b,e,p,Count(e),t,true),
    "no-window" => NoWindowPower.Pow(b,e,p,Count(e),t,true),
    "profile" => ProfilePower.Pow(b,e,p,Count(e),t,true),
    "direct" => BigInteger.Pow(new BigInteger(b),e),
    _ => throw new ArgumentException(mode)
};
Action<int,int> noop = static (_,_) => {};
if (command == "micro") { KernelBench.Run(); return; }
if (command == "validate")
{
    var cases = new List<(long,int)> { (0,0),(0,7),(1,0),(1,100),(-1,99),(-1,100),
        (long.MinValue,1),(long.MinValue,13),(long.MaxValue,128),(3,511),(3,512),(3,513),
        (65535,1023),(65536,1024),(65537,1025),(999999999999999999,10000),(-17,8191),
        (3,999999),(3,1000000),(3,1000001),(3,1048576) };
    var random = new Random(4711);
    for (int i=0;i<20;i++) cases.Add((random.NextInt64(long.MinValue,long.MaxValue),random.Next(1,2000)));
    int passed = 0;
    foreach (var (b,e) in cases)
    {
        BigInteger expected = BigInteger.Pow(new BigInteger(b),e);
        List<int>? sharedSchedule = null;
        foreach (string mode in modes)
        {
            int last = 0;
            List<int> schedule = [];
            BigInteger actual = Power(mode,b,e,(done,total) => {
                if (done <= last || done > total || total != Count(e)) throw new Exception("Invalid progress");
                last = done;
                schedule.Add(done);
            });
            if (actual != expected || last != Count(e)) throw new Exception($"Mismatch {mode} {b}^{e}");
            if (mode is "runtime" or "simd")
            {
                sharedSchedule ??= schedule;
                if (!sharedSchedule.SequenceEqual(schedule)) throw new Exception("Hardware mode changed power schedule");
            }
            passed++;
        }
    }
    foreach (string mode in modes)
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { Power(mode,3,0,noop,cancelled.Token); throw new Exception("Expected pre-cancellation"); }
        catch (OperationCanceledException) { passed++; }
        foreach (int cancelAt in new[] { 1,10,Count(1000000) })
        {
            using var cts = new CancellationTokenSource();
            try { Power(mode,3,1000000,(done,_) => { if (done >= cancelAt) cts.Cancel(); },cts.Token); throw new Exception("Expected cancellation"); }
            catch (OperationCanceledException) { passed++; }
        }
        try { Power(mode,3,-1,noop); throw new Exception("Expected exponent rejection"); }
        catch (ArgumentOutOfRangeException) { passed++; }
    }
    Print(new { Kind="validation", Passed=passed, Modes=modes });
    return;
}

// Warm each path before rotating the order of matched samples. All arithmetic is
// synchronous on this thread. Hashing and JSON output are outside the timed region.
foreach (string mode in modes)
    for (int i=0;i<(warmupExponent == 10000 ? 4 : 1);i++) Power(mode,basis,warmupExponent,noop);
string? expectedHash = null;
for (int round=0;round<rounds;round++)
for (int i=0;i<modes.Length;i++)
{
    string mode = modes[(i+round) % modes.Length];
    AuditMeasure.Samples.Clear();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    long start = Stopwatch.GetTimestamp();
    BigInteger value = Power(mode,basis,exponent,noop);
    double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    allocated = GC.GetAllocatedBytesForCurrentThread()-allocated;
    string hash = Convert.ToHexString(SHA256.HashData(value.ToByteArray()));
    expectedHash ??= hash;
    if (hash != expectedHash) throw new Exception("Benchmark hash mismatch");
    Print(new { Kind="power", Mode=mode, Base=basis, Exponent=exponent, Round=round,
        Milliseconds=ms, AllocatedBytes=allocated, Bits=value.GetBitLength(), Hash=hash });
    if (mode == "profile") foreach (var sample in AuditMeasure.Samples) Print(new { Kind="stage", Round=round, Sample=sample });
}
