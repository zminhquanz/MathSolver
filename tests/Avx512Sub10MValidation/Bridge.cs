using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MathSolver.Numerics;
internal sealed partial class ParallelBigUnsigned
{
    internal string CheckAndHash(ulong basis, int exponent)
    {
        foreach (uint prime in new uint[] { 1000000007, 1000000009, 4294967291 })
        {
            ulong remainder = 0;
            for (int i = _limbCount - 1; i >= 0; i--) remainder = (remainder * LimbBase + _limbs[i]) % prime;
            if (remainder != (ulong)BigInteger.ModPow(basis, exponent, prime)) throw new Exception("Residue mismatch");
        }
        return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(_limbs.AsSpan(0, _limbCount))));
    }

    internal static void ValidateUncachedStage()
    {
        var random = new Random(512);
        foreach (int workerCount in new[] { 1, 3, 24 })
        {
            using var pool = new NttBufferPool();
            using var twiddles = new NttTwiddleBufferPool();
            using var plans = new SharedNttTwiddlePlans(twiddles, true, true);
            using var workers = new FixedWorkerTeam(workerCount, pool, plans);
            foreach (var (p, primitive) in new[] { (FirstModulus,FirstPrimitiveRoot), (SecondModulus,SecondPrimitiveRoot) })
            foreach (int stage in new[] { 4, 8, 16, 32, 64, 1024, 65536 })
            {
                uint root = (uint)ModPow(primitive, (p-1)/(uint)stage, p);
                uint[] original = new uint[stage*3];
                for (int i=0;i<original.Length;i++) original[i] = i%7==0 ? p-1 : (uint)random.NextInt64(p);
                uint[] expected = (uint[])original.Clone();
                int half = stage/2;
                for (int group=0;group<expected.Length;group+=stage)
                {
                    ulong twiddle=1;
                    for(int i=0;i<half;i++)
                    {
                        ulong a=original[group+i], b=original[group+i+half];
                        expected[group+i]=(uint)((a+b)%p);
                        expected[group+i+half]=(uint)(((a+p-b)%p)*twiddle%p);
                        twiddle=twiddle*root%p;
                    }
                }
                ExecuteForwardUncachedStageAvx512(original,p,root,stage,workers,default);
                if(!original.AsSpan().SequenceEqual(expected)) throw new Exception($"Stage mismatch {p} {stage} {workerCount}");
                // Arbitrary worker tails, with untouched prefix/suffix guards.
                foreach(var (first,last) in new[] { (1,2),(1,half-1),(0,half) })
                {
                    if(last<=first) continue;
                    uint[] tail=(uint[])expected.Clone();
                    uint[] reference=(uint[])tail.Clone();
                    for(int i=first;i<last;i++)
                    {
                        ulong a=tail[i],b=tail[i+half],t=ModPow(root,(uint)i,p);
                        reference[i]=(uint)((a+b)%p);
                        reference[i+half]=(uint)(((a+p-b)%p)*t%p);
                    }
                    ProcessForwardUncachedStageSegmentAvx512(tail,p,root,stage,0,first,last,default);
                    if(!tail.AsSpan().SequenceEqual(reference)) throw new Exception("Tail/guard mismatch");
                }
            }
            using var cancel=new CancellationTokenSource(); cancel.Cancel();
            try { ExecuteForwardUncachedStageAvx512(new uint[64],FirstModulus,1,64,workers,cancel.Token); throw new Exception("Cancellation ignored"); }
            catch(OperationCanceledException) { }
        }
        Console.WriteLine("PASS both primes, 1/3/24 workers, width cascade, tails, guards, cancellation");
        using var benchPool=new NttBufferPool();
        using var benchTwiddles=new NttTwiddleBufferPool();
        using var benchPlans=new SharedNttTwiddlePlans(benchTwiddles,true,true);
        using var benchWorkers=new FixedWorkerTeam(24,benchPool,benchPlans);
        foreach(int size in new[]{1<<20,1<<24})
        foreach(var (p,primitive) in new[]{(FirstModulus,FirstPrimitiveRoot),(SecondModulus,SecondPrimitiveRoot)})
        {
            uint root=(uint)ModPow(primitive,(p-1)/(uint)size,p);
            uint[] data=new uint[size]; Array.Fill(data,p-1);
            ExecuteForwardUncachedStageAvx2(data,p,root,size,benchWorkers,default);
            ExecuteForwardUncachedStageAvx512(data,p,root,size,benchWorkers,default);
            for(int round=0;round<6;round++)
            foreach(bool wide in round%2==0?new[]{false,true}:new[]{true,false})
            {
                var timer=Stopwatch.StartNew();
                for(int repeat=0;repeat<8;repeat++)
                    if(wide) ExecuteForwardUncachedStageAvx512(data,p,root,size,benchWorkers,default);
                    else ExecuteForwardUncachedStageAvx2(data,p,root,size,benchWorkers,default);
                Console.WriteLine($"MICRO N={size} p={p} avx512={wide} round={round} ms={timer.Elapsed.TotalMilliseconds/8:F4}");
            }
        }
    }
}
