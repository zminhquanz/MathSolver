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
string[] modes=(args.ElementAtOrDefault(4)??"runtime,current,kara-avx2-1024,kara-avx2-4096,kara-avx512-4096").Split(',');
void Print(object obj)=>Console.WriteLine(JsonSerializer.Serialize(obj));
int Operations(int n)=>n==0?0:BitOperations.Log2((uint)n)+BitOperations.PopCount((uint)n)-1;
BigInteger Run(string mode,long b,int e,Action<int,int>? progress=null,CancellationToken token=default)
{
    progress??=static(_,_)=>{};
    if(mode=="runtime" || mode=="current")return SingleThreadBigIntegerPower.Pow(b,e,progress,Operations(e),token,mode=="current");
    if(mode=="profile")return ProfileCurrent.Pow(b,e,progress,Operations(e),token,true);
    var parts=mode.Split('-'); CandidateSquare.Difference=parts[0]=="diff"; CandidateSquare.Kernel=parts[1]; CandidateSquare.Limit=int.Parse(parts[2]);
    CandidateSquare.LeafSize=parts.Length>3?int.Parse(parts[3]):64;
    return CandidatePower.Pow(b,e,progress,Operations(e),token,true);
}
Print(new{Kind="machine",Runtime=Environment.Version.ToString(),Avx2=Avx2.IsSupported,Avx512=Avx512F.IsSupported,DQ=Avx512DQ.IsSupported,Threads=1});
if(command=="validate")
{
    int passed=0;
    var random=new Random(417);
    foreach(bool difference in new[]{false,true})
    foreach(string kernel in new[]{"scalar","avx2","avx512"})
    foreach(int leaf in new[]{32,64,128})
    {
        CandidateSquare.Difference=difference; CandidateSquare.Kernel=kernel; CandidateSquare.LeafSize=leaf;
        using var square=new CandidateSquare(8192);
        foreach(int size in new[]{1,2,31,32,33,63,64,65,127,128,129,255,256,257,511,512,513,1023,1024,1025,4095,4096})
        foreach(int pattern in new[]{0,1,2})
        {
            ushort[] input=new ushort[size];
            for(int i=0;i<size;i++)input[i]=pattern==0?(ushort)65535:pattern==1?(ushort)random.Next(65536):(ushort)0;
            if(pattern==2)input[^1]=1;
            BigInteger n=new(MemoryMarshal.AsBytes(input.AsSpan()),true,false);
            for(int repeat=0;repeat<2;repeat++)
            {
                var result=square.Square(input,[],default);
                if(new BigInteger(MemoryMarshal.AsBytes(result.AsSpan()),true,false)!=n*n)throw new Exception($"Square mismatch {kernel}/{leaf}/{size}/{pattern}");
                passed++;
            }
        }
        using var cts=new CancellationTokenSource();cts.Cancel();
        try{square.Square(new ushort[100],[],cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){passed++;}
    }
    foreach(string mode in modes)
    {
        foreach(var (b,e) in new (long,int)[]{(0,0),(0,10),(1,100),(-1,101),(long.MinValue,3),(long.MaxValue,1025),(3,999999),(3,1000000),(3,1000001),(-17,4097),(999999999999999999,2049)})
        {
            int last=0; var result=Run(mode,b,e,(done,total)=>{if(done<=last||total!=Operations(e)||done>total)throw new Exception("Progress");last=done;});
            if(result!=BigInteger.Pow(b,e)||last!=Operations(e))throw new Exception("Power mismatch "+mode);
            passed++;
        }
        foreach(int cancelAt in new[]{1,10,Operations(1000000)})
        {
            using var cts=new CancellationTokenSource();
            try{Run(mode,3,1000000,(done,_)=>{if(done>=cancelAt)cts.Cancel();},cts.Token);throw new Exception("Power cancellation ignored");}catch(OperationCanceledException){passed++;}
        }
    }
    Print(new{Kind="validation",Passed=passed});return;
}
if(command=="micro")
{
    var random=new Random(97);
    foreach(int size in new[]{64,128,256,512,1024,2048,4096,8192,16384})
    {
        ushort[] input=new ushort[size];for(int i=0;i<size;i++)input[i]=(ushort)random.Next(65536);input[^1]|=0x8000;
        BigInteger n=new(MemoryMarshal.AsBytes(input.AsSpan()),true,false);
        var expected=n*n;
        foreach(int leaf in new[]{32,64,128})
        foreach(string mode in new[]{"runtime","scalar","avx2","avx512"})
        {
            if(mode=="runtime"&&leaf!=32)continue;
            CandidateSquare.Kernel=mode;CandidateSquare.LeafSize=leaf;
            using var square=new CandidateSquare(size);
            if(new BigInteger(MemoryMarshal.AsBytes(square.Square(input,[],default).AsSpan()),true,false)!=expected)throw new Exception("Micro mismatch");
            for(int w=0;w<10;w++){if(mode=="runtime")GC.KeepAlive(n*n);else square.Square(input,[],default);}
            int iterations=Math.Max(3,32768/size);
            for(int round=0;round<3;round++)
            {
                long allocated=GC.GetAllocatedBytesForCurrentThread();long t=Stopwatch.GetTimestamp();
                for(int i=0;i<iterations;i++){if(mode=="runtime")GC.KeepAlive(n*n);else square.Square(input,[],default);}
                Print(new{Kind="square",Size=size,Leaf=leaf,Mode=mode,Round=round,Microseconds=Stopwatch.GetElapsedTime(t).TotalMicroseconds/iterations,Allocated=(GC.GetAllocatedBytesForCurrentThread()-allocated)/iterations});
            }
        }
    }
    return;
}
foreach(string mode in modes)Run(mode,basis,100000);
string? hash=null;
for(int round=0;round<rounds;round++)
for(int i=0;i<modes.Length;i++)
{
    string mode=modes[(i+round)%modes.Length];Measure.Rows.Clear();
    long alloc=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();
    BigInteger result=Run(mode,basis,exponent);
    double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    alloc=GC.GetAllocatedBytesForCurrentThread()-alloc;
    string digest=Convert.ToHexString(SHA256.HashData(result.ToByteArray()));hash??=digest;if(hash!=digest)throw new Exception("Hash mismatch");
    Print(new{Kind="power",Mode=mode,Base=basis,Exponent=exponent,Round=round,Milliseconds=ms,Allocated=alloc,Hash=digest});
    if(mode=="profile")foreach(var row in Measure.Rows)Print(row);
}
internal static class Measure
{
    internal static List<object> Rows=new();
    internal static BigInteger Pow(BigInteger value,int exponent)
    {
        long start=Stopwatch.GetTimestamp();var result=BigInteger.Pow(value,exponent);
        Rows.Add(new{Kind="runtime-stage",InputBits=value.GetBitLength(),Power=exponent,Milliseconds=Stopwatch.GetElapsedTime(start).TotalMilliseconds});return result;
    }
    internal static BigInteger Square(BigInteger value)
    {
        long start=Stopwatch.GetTimestamp();var result=value*value;
        Rows.Add(new{Kind="runtime-stage",InputBits=value.GetBitLength(),Power=2,Milliseconds=Stopwatch.GetElapsedTime(start).TotalMilliseconds});return result;
    }
}
