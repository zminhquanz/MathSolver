using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text.Json;
using MathSolver.Numerics;

string command=args.ElementAtOrDefault(0)??"validate";
long basis=long.Parse(args.ElementAtOrDefault(1)??"3");
int exponent=int.Parse(args.ElementAtOrDefault(2)??"1000000");
int rounds=int.Parse(args.ElementAtOrDefault(3)??"5");
string[] modes=(args.ElementAtOrDefault(4)??"current,avx2-32,avx512-32").Split(',');
void Print(object obj)=>Console.WriteLine(JsonSerializer.Serialize(obj));
int Operations(int n)=>n==0?0:BitOperations.Log2((uint)n)+BitOperations.PopCount((uint)n)-1;
BigInteger Run(bool instrumented,long b,int e,Action<int,int>? progress=null,CancellationToken token=default)
{
    SquareBridge.Token=token;progress??=static(_,_)=>{};
    return instrumented?InstrumentedPower.Pow(b,e,progress,Operations(e),token,true):BaselinePower.Pow(b,e,progress,Operations(e),token,true);
}
BigInteger RunMode(string mode,long b,int e,Action<int,int>? progress=null,CancellationToken token=default)
{
    if(mode=="runtime")return BaselinePower.Pow(b,e,progress??(static(_,_)=>{}),Operations(e),token,false);
    if(mode.StartsWith("regressed-"))return RegressedPower.Pow(b,e,progress??(static(_,_)=>{}),Operations(e),token,true,mode=="regressed-avx512");
    if(mode.StartsWith("production-"))return SingleThreadBigIntegerPower.Pow(b,e,progress??(static(_,_)=>{}),Operations(e),token,true,mode=="production-avx512");
    return Run(mode!="current",b,e,progress,token);
}void Configure(string mode)
{
    SquareBridge.Candidate?.Dispose();SquareBridge.Candidate=null;
    if(mode=="current" || mode=="runtime" || mode.StartsWith("regressed-") || mode.StartsWith("production-"))return;
    var parts=mode.Split('-');SquareBridge.Candidate=new Limb32Square(parts[0],int.Parse(parts[1]));
    SquareBridge.MinimumWords=parts.Length>2?int.Parse(parts[2]):1024;
    SquareBridge.MaximumWords=parts.Length>3?int.Parse(parts[3]):int.MaxValue;
}
Print(new{Kind="machine",Runtime=Environment.Version.ToString(),Avx2=Avx2.IsSupported,Avx512=Avx512F.IsSupported,Threads=1,Optimized=!(System.Reflection.CustomAttributeExtensions.GetCustomAttribute<DebuggableAttribute>(System.Reflection.Assembly.GetExecutingAssembly())?.IsJITOptimizerDisabled??false),Tiering=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation")??"default"});
if(command=="validate" || command=="policy")
{
    int passed=0;var random=new Random(7901);
    foreach(var (b,e,eligible) in new (ulong,int,bool)[]{(3,1000000,true),(17,1000000,true),(65537,1000000,true),(999999999999999999,1000000,false),(ulong.MaxValue,1000000,false),(3,999999,false),(3,100000000,false),(0,1000000,false),(1,1000000,false)})
    {
        bool expected=eligible && SingleThreadBigIntegerPower.IsLargeSquareBackendEnabled;
        if(SingleThreadBigIntegerPower.ShouldUseLargeSquareBackend(b,e)!=expected)throw new Exception("Whole-power dispatch regression");passed++;
    }
    if(command=="policy"){Print(new{Kind="policy",Passed=passed,LargeBackend=SingleThreadBigIntegerPower.IsLargeSquareBackendEnabled});return;}
    foreach(var backend in new[]{"scalar","avx2","avx512"})
    foreach(int leaf in new[]{16,32,64,128,256})
    {
        using var kernel=new Limb32Square(backend,leaf);
        using var production=new SimdBigIntegerSquare(backend=="avx512");
        foreach(int n in new[]{0,1,2,3,15,16,17,31,32,33,63,64,65,127,128,129,255,256,257,511,512,513,1023,1024,1025,4095,4096})
        foreach(int pattern in new[]{0,1,2,3})
        {
            uint[] words=new uint[n];
            for(int i=0;i<n;i++)words[i]=pattern==0?uint.MaxValue:pattern==1?(uint)random.NextInt64(1L<<32):pattern==2?(i%2==0?uint.MaxValue:0U):0U;
            BigInteger value=new(MemoryMarshal.AsBytes(words.AsSpan()),true,false);
            for(int repeat=0;repeat<2;repeat++)
            {
                var result=kernel.Square(words);
                if(new BigInteger(MemoryMarshal.AsBytes(result.AsSpan()),true,false)!=value*value)throw new Exception($"Square mismatch {backend}/{leaf}/{n}/{pattern}");passed++;
                var actualProduction=production.Square(words);
                if(new BigInteger(MemoryMarshal.AsBytes(actualProduction.AsSpan()),true,false)!=value*value)throw new Exception("Production square mismatch");passed++;
            }
        }
        foreach(int words in new[]{1023,1024,262144,262145})
        {
            var boundary=BigInteger.One << (32*(words-1));
            if(SimdBigIntegerSquare.CanSquare(boundary)!=(words>=1024&&words<=262144))throw new Exception("Dispatch bounds");passed++;
        }
        using var cts=new CancellationTokenSource();cts.Cancel();
        try{kernel.Square(new uint[100],cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){passed++;}
        if(kernel.SquareBigInteger(123456789)!=new BigInteger(123456789)*123456789)throw new Exception("Reuse after cancellation");passed++;
    }
    foreach(string mode in new[]{"current","scalar-32","avx2-32","avx512-32","avx512-32-1000-4000","production-avx2","production-avx512"})
    {
        Configure(mode);
        foreach(var (b,e) in new (long,int)[]{(0,0),(0,7),(-1,99),(1,100),(long.MinValue,3),(long.MaxValue,1025),(-17,4097),(3,999999),(3,1000000),(3,1000001),(17,1000000)})
        {
            int last=0;var value=RunMode(mode,b,e,(done,total)=>{if(done<=last||done>total||total!=Operations(e))throw new Exception("Progress");last=done;});
            if(value!=BigInteger.Pow(b,e)||last!=Operations(e))throw new Exception("Power mismatch "+mode);passed++;
        }
        foreach(int at in new[]{1,10,Operations(1000000)})
        {
            using var cts=new CancellationTokenSource();
            try{RunMode(mode,3,1000000,(done,_)=>{if(done>=at)cts.Cancel();},cts.Token);throw new Exception("Power cancellation ignored");}catch(OperationCanceledException){passed++;}
        }
    }
    SquareBridge.Candidate?.Dispose();SquareBridge.Candidate=null;
    Print(new{Kind="validation",Passed=passed});return;
}
if(command=="capture" || command=="micro")
{
    SquareBridge.Capture=true;
    BigInteger expectedPower=Run(true,basis,exponent);
    if(expectedPower!=Run(false,basis,exponent))throw new Exception("Capture changed result");
    SquareBridge.Capture=false;
    var inputs=SquareBridge.Inputs.Distinct().OrderByDescending(x=>x.GetBitLength()).Take(3).Reverse().ToArray();
    foreach(var value in inputs)
    {
        long bits=value.GetBitLength();byte[] bytes=value.ToByteArray(true,false);
        uint[] words=new uint[(bytes.Length+3)/4];bytes.AsSpan().CopyTo(MemoryMarshal.AsBytes(words.AsSpan()));
        string hash=Convert.ToHexString(SHA256.HashData(bytes));
        Print(new{Kind="operand",Base=basis,Exponent=exponent,InputBits=bits,Words=words.Length,Hash=hash});
        if(command=="capture")continue;
        var expected=value*value;
        foreach(string mode in modes)
        {
            Configure(mode);
            for(int w=0;w<2;w++)
            {
                if(mode=="current")GC.KeepAlive(value*value);else GC.KeepAlive(SquareBridge.Candidate!.SquareBigInteger(value));
            }
        }
        for(int round=0;round<rounds;round++)
        for(int i=0;i<modes.Length;i++)
        {
            string mode=modes[(i+round)%modes.Length];Configure(mode);
            var kernel=SquareBridge.Candidate;
            // Prime pooled buffers outside the measurement, as repeated power steps reuse them.
            if(kernel is not null)kernel.Square(words);
            long alloc=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
            var result=kernel is null?value*value:kernel.SquareBigInteger(value);
            double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;alloc=GC.GetAllocatedBytesForCurrentThread()-alloc;
            if(result!=expected)throw new Exception("Boundary square mismatch");
            Print(new{Kind="square-conversion",Base=basis,InputBits=bits,Mode=mode,Backend=kernel?.Backend??"runtime",Round=round,Milliseconds=ms,Allocated=alloc});
            if(kernel is not null)
            {
                start=Stopwatch.GetTimestamp();var raw=kernel.Square(words);ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if(new BigInteger(MemoryMarshal.AsBytes(raw.AsSpan()),true,false)!=expected)throw new Exception("Raw square mismatch");
                Print(new{Kind="square-words",Base=basis,InputBits=bits,Mode=mode,Round=round,Milliseconds=ms});
            }
        }
    }
    SquareBridge.Candidate?.Dispose();return;
}
foreach(string mode in modes){Configure(mode);RunMode(mode,basis,100000);}
string? digest=null;
for(int round=0;round<rounds;round++)
for(int i=0;i<modes.Length;i++)
{
    string mode=modes[(i+round)%modes.Length];
    SquareBridge.Candidate?.Dispose();SquareBridge.Candidate=null;
    long alloc=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
    Configure(mode);
    var result=RunMode(mode,basis,exponent);
    double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;alloc=GC.GetAllocatedBytesForCurrentThread()-alloc;
    string hash=Convert.ToHexString(SHA256.HashData(result.ToByteArray()));digest??=hash;if(hash!=digest)throw new Exception("Power hash mismatch");
    Print(new{Kind="power",Base=basis,Exponent=exponent,Mode=mode,Round=round,Milliseconds=ms,Allocated=alloc,Hash=hash});
}
SquareBridge.Candidate?.Dispose();
