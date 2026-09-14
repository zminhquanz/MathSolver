using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.Intrinsics.X86;
using MathSolver.Numerics;
using MathSolver.Services.Core;

void Print(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
Print(new { Kind="machine", Runtime=Environment.Version.ToString(), Workers=Environment.ProcessorCount,
    Optimized = !System.Reflection.CustomAttributeExtensions.GetCustomAttribute<DebuggableAttribute>(
        System.Reflection.Assembly.GetExecutingAssembly())!.IsJITOptimizerDisabled,
    Avx2=Avx2.IsSupported, Avx512=Avx512F.IsSupported, Simd=MathSolver.Services.CalculationAccelerationManager.UsePowerNttAvx2 });
string command = args.ElementAtOrDefault(0) ?? "validate";
int workers = int.Parse(args.ElementAtOrDefault(3) ?? Math.Min(8,Environment.ProcessorCount).ToString());
var engine = new PowerRootEngine();
if(command == "ntt-reference")
{
    long referenceBase=long.Parse(args[1]); int referenceExponent=int.Parse(args[2]);
    var timer=Stopwatch.StartNew();
    var result=await engine.ComputeMemoryBoundedParallelPowerAsync(referenceBase,referenceExponent,workers,static(_,_)=>{},default);
    double seconds=timer.Elapsed.TotalSeconds;
    ParallelBigUnsigned.ValidateDecimalResidues(result.Magnitude,(ulong)referenceBase,referenceExponent);
    Print(new{Kind="ntt-reference",Base=referenceBase,Exponent=referenceExponent,Workers=workers,
        Seconds=seconds,result.Magnitude.StorageBytes,Diagnostics=result.Diagnostics,Passed=true});
    return;
}
if(command == "ntt-tune")
{
    long tuneBase=long.Parse(args[1]); int tuneExponent=int.Parse(args[2]);
    int cap=int.Parse(args.ElementAtOrDefault(4)??"0");
    bool cached=bool.Parse(args.ElementAtOrDefault(5)??"true");
    bool? persistent=args.Length>6?bool.Parse(args[6]):null;
    int m=PowerOfTenArithmetic.GetBinaryExponent(tuneBase,tuneExponent);
    var timer=Stopwatch.StartNew();
    var result=cap==0 && cached && persistent is null
        ? await engine.ComputePowerOfTenAsync(tuneBase,tuneExponent,workers,null,default)
        : ParallelBigUnsigned.PowFiveAndShift(m,workers,null,default,1<<cap,true,cached,persistent);
    double seconds=timer.Elapsed.TotalSeconds;
    var magnitude=result.LargeMagnitude??throw new Exception("Use a large result for tuning");
    foreach(uint prime in new uint[]{1000000007,1000000009,4294967291})
        if(magnitude.Remainder(prime,default)!=(uint)BigInteger.ModPow(BigInteger.Abs(tuneBase),tuneExponent,prime))
            throw new Exception("Tuned NTT residue mismatch");
    Print(new{Kind="ntt-tune",Base=tuneBase,Exponent=tuneExponent,Workers=workers,Cap=cap,Cached=cached,
        Persistent=persistent,Seconds=seconds,magnitude.StorageBytes,result.NttTransformLimit,Diagnostics=result.Diagnostics,Passed=true});
    return;
}
if(command == "wide")
{
    Print(LargeBinaryValidation.ValidateWide());
    return;
}
if (command is "binary-bench" or "binary-export")
{
    LargeBinaryValidation.Benchmark(long.Parse(args.ElementAtOrDefault(1) ?? "10"),
        int.Parse(args.ElementAtOrDefault(2) ?? "1000000"), workers,
        int.Parse(args.ElementAtOrDefault(4) ?? "3"), command == "binary-export");
    return;
}
if (command == "large")
{
    long b = long.Parse(args.ElementAtOrDefault(1) ?? "1000000000000000000");
    int e = int.Parse(args.ElementAtOrDefault(2) ?? "100000000");
    Print(await LargeBinaryValidation.ValidateLarge(engine, b, e, workers));
    return;
}
if (command == "validate")
{
    int passed = 0;
    var cases = new List<(long,int)> { (10,0),(10,1),(10,2),(-10,3),(-100,4),(-1000,5),
        (10,511),(10,512),(10,513),(100,1024),(1000,10000),(1000000000000000000,5000) };
    var random = new Random(20260911);
    for(int i=0;i<20;i++) cases.Add(((i%2==0 ? 1 : -1)*(long)BigInteger.Pow(10,random.Next(1,19)),random.Next(1,500)));
    foreach(var (b,e) in cases)
    {
        BigInteger expected=BigInteger.Pow(new BigInteger(b),e);
        foreach(int w in new[] {1,2,workers}.Distinct())
        {
            int last=0, total=PowerOfTenArithmetic.OperationCount(PowerOfTenArithmetic.GetBinaryExponent(b,e));
            var actual=await engine.ComputePowerOfTenAsync(b,e,w,(done,count)=>{
                if(done<=last || done>count || count!=total) throw new Exception("Invalid progress"); last=done;
            },default);
            if(actual.Value!=expected || last!=total) throw new Exception($"Wrong {b}^{e}, workers={w}");
            if(e>0 && engine.SelectPowerStrategy(b,e,out _)!=PowerRootComputationStrategy.FactorizedPowerOfTen)
                throw new Exception("Wrong strategy");
            passed++;
        }
    }
    foreach(int shift in new[] { 1,15,16,17,9999,10000,10001,16384,20000 })
    {
        var actual=ParallelBigUnsigned.PowFiveAndShift(shift,workers,null,default,1024);
        if(actual.Value!=BigInteger.Pow(10,shift)) throw new Exception($"Segment/shift mismatch {shift}");
        if(actual.Diagnostics?.NttWorkspacePeakBytes>1024L*4*4) throw new Exception("Transform lease budget exceeded");
        passed++;
    }
    foreach(int w in new[] {1,workers}.Distinct())
    foreach(int cancelAt in new[] {0,1,PowerOfTenArithmetic.OperationCount(10000)})
    {
        using var cts=new CancellationTokenSource(); if(cancelAt==0) cts.Cancel();
        try { await engine.ComputePowerOfTenAsync(10,10000,w,(done,_)=>{if(done>=cancelAt)cts.Cancel();},cts.Token);
            throw new Exception("Cancellation was ignored"); }
        catch(OperationCanceledException) {passed++;}
    }
    foreach(var (b,e) in new (long,int)[]{(2,100),(long.MinValue,10),(10,-1),(1000000000000000000,int.MaxValue)})
    {
        try { PowerOfTenArithmetic.Pow(b,e,workers,null,default); throw new Exception("Invalid input accepted"); }
        catch(ArgumentException) {passed++;}
    }
    if(engine.SelectPowerStrategy(3,100,out _)!=PowerRootComputationStrategy.SingleThreadedBigIntegerPower ||
       engine.SelectPowerStrategy(long.MinValue,3,out _)!=PowerRootComputationStrategy.BitShift)
        throw new Exception("Ordinary strategy regression");
    passed++;
    passed+=ParallelBigUnsigned.ValidateBinarySquares(workers);
    foreach (BigInteger value in new[] { BigInteger.Zero, BigInteger.Pow(3,12000), BigInteger.Pow(10,8192)+1234567 })
    {
        string expected=value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var writer=new StringWriter(); int blocks=0;
        BigIntegerDecimalWriter.Write(writer,value,expected.Length,4096,()=>blocks++,default);
        if(writer.ToString()!=expected || blocks!=BigIntegerDecimalWriter.CountBlocks(expected.Length,4096))
            throw new Exception("Materialized decimal writer mismatch");
        passed++;
    }
    using (var cancelledExport=new CancellationTokenSource())
    {
        try { BigIntegerDecimalWriter.Write(TextWriter.Null,BigInteger.One,1,4096,()=>cancelledExport.Cancel(),cancelledExport.Token);
            throw new Exception("Export cancellation ignored"); }
        catch(OperationCanceledException) {passed++;}
    }
    if(PowerOfTenArithmetic.SelectWorkerCount(100000,24)!=1 ||
       PowerOfTenArithmetic.SelectWorkerCount(1000001,24)!=Math.Min(8,Environment.ProcessorCount) ||
       PowerOfTenArithmetic.SelectWorkerCount(1000001,1)!=1)
        throw new Exception("Worker selection mismatch");
    passed++;
    using(var midTransform=new CancellationTokenSource())
    {
        midTransform.CancelAfter(5);
        try { await engine.ComputePowerOfTenAsync(1000,1000000,workers,null,midTransform.Token);
            throw new Exception("Running transform cancellation ignored"); }
        catch(OperationCanceledException) {passed++;}
    }
    foreach (int w in new[] {1,workers}.Distinct())
    {
        var ordinary=ParallelBigUnsigned.Pow(3,10000,w,null,default);
        using var writer=new StringWriter();
        ordinary.Magnitude.WriteDecimalBlocks(writer,4096,static()=>{},default,false);
        if(BigInteger.Parse(writer.ToString())!=BigInteger.Pow(3,10000)) throw new Exception("Decimal NTT regression");
        passed++;
    }
    passed += LargeBinaryValidation.ValidateSmall(workers);
    Print(new {Kind="validation",Passed=passed});
    return;
}

long basis=long.Parse(args.ElementAtOrDefault(1)??"10");
int exponent=int.Parse(args.ElementAtOrDefault(2)??"1000000");
int rounds=int.Parse(args.ElementAtOrDefault(4)??"3");
string[] modes=(args.ElementAtOrDefault(5)??"direct,single,parallel").Split(',');
string? hash=null;
BigInteger Run(string mode, long b, int e, out ParallelPowerDiagnostics? diagnostics)
{
    diagnostics=null;
    if(mode=="direct") return SingleThreadBigIntegerPower.Pow(b,e,static(_,_)=>{},0,default,false);
    int selectedWorkers = mode=="single" ? 1 :
        mode.StartsWith("parallel",StringComparison.Ordinal) && int.TryParse(mode.AsSpan(8),out int count) ? count : workers;
    var result=PowerOfTenArithmetic.Pow(b,e,selectedWorkers,null,default);
    diagnostics=result.Diagnostics; return result.Value;
}
foreach(var mode in modes) Run(mode,basis,1000,out _);
for(int round=0;round<rounds;round++)
foreach(string mode in round%2==0 ? modes : modes.Reverse())
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    using var process=Process.GetCurrentProcess(); process.Refresh();
    long privateBefore=process.PrivateMemorySize64, peakPrivate=privateBefore;
    using var monitorStop=new CancellationTokenSource();
    var monitor=new Thread(()=>{
        using var sampler=Process.GetCurrentProcess();
        while(!monitorStop.IsCancellationRequested) {sampler.Refresh(); peakPrivate=Math.Max(peakPrivate,sampler.PrivateMemorySize64); Thread.Sleep(10);}
    }) {IsBackground=true}; monitor.Start();
    long allocated=GC.GetTotalAllocatedBytes(true), start=Stopwatch.GetTimestamp();
    BigInteger result=Run(mode,basis,exponent,out var diagnostics);
    double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    allocated=GC.GetTotalAllocatedBytes(true)-allocated;
    monitorStop.Cancel(); monitor.Join();
    process.Refresh(); peakPrivate=Math.Max(peakPrivate,process.PrivateMemorySize64);
    string actualHash=Convert.ToHexString(SHA256.HashData(result.ToByteArray()));
    hash??=actualHash; if(hash!=actualHash)throw new Exception("Benchmark hash mismatch");
    Print(new {Kind="power", Mode=mode,Base=basis,Exponent=exponent,Round=round,Milliseconds=ms,
        ResultBytes=result.GetByteCount(),AllocatedBytes=allocated,PrivateBefore=privateBefore,PeakPrivate=peakPrivate,
        TransformLeasePeak=diagnostics?.NttWorkspacePeakBytes??0,PoolRetainedPeak=diagnostics?.NttPoolPeakRetainedBytes??0,
        NttProducts=diagnostics?.NttMultiplicationCount??0,Hash=actualHash});
    if(command=="export")
    {
        start=Stopwatch.GetTimestamp();
        using var writer=new StringWriter();
        int digitCount=checked(PowerOfTenArithmetic.GetBinaryExponent(basis,exponent)+1), blocks=0;
        if(result.Sign<0)writer.Write('-');
        BigIntegerDecimalWriter.Write(writer,BigInteger.Abs(result),digitCount,4096,()=>blocks++,default);
        string digits=writer.ToString();
        string magnitudeDigits=result.Sign<0?digits[1..]:digits;
        if(blocks!=BigIntegerDecimalWriter.CountBlocks(digitCount,4096) || magnitudeDigits.Length!=digitCount ||
            magnitudeDigits[0]!='1' || magnitudeDigits.AsSpan(1).ContainsAnyExcept('0'))
            throw new Exception("Decimal export mismatch");
        Print(new {Kind="export",Mode=mode,Milliseconds=Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            Digits=digitCount,Characters=digits.Length});
    }
}
