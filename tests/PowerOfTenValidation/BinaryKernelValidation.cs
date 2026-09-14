using System.Numerics;
namespace MathSolver.Numerics;
internal sealed partial class ParallelBigUnsigned
{
    private static BigInteger PackShiftedBinary(BinaryMagnitude value, int shift, CancellationToken token)
    {
        BigInteger result = BigInteger.Zero;
        for (int i = value.Count - 1; i >= 0; i--)
        {
            token.ThrowIfCancellationRequested();
            result = (result << 16) + value.Limbs[i];
        }
        return result << shift;
    }

    internal static void ValidateDecimalResidues(ParallelBigUnsigned value, ulong basis, int exponent)
    {
        foreach(uint prime in new uint[]{1000000007,1000000009,4294967291})
        {
            ulong remainder=0;
            for(int i=value._limbCount-1;i>=0;i--) remainder=(remainder*10000+value._limbs[i])%prime;
            if(remainder!=(uint)BigInteger.ModPow(basis,exponent,prime))throw new Exception("Reference NTT residue mismatch");
        }
    }

    internal static int ValidateBinarySquares(int workerCount)
    {
        int totalPassed=0;
        foreach(bool cached in new[]{false,true})
        foreach(bool persistent in new[]{false,true})
        {
        bool avx2=MathSolver.Services.CalculationAccelerationManager.UsePowerNttAvx2;
        using var pool=new NttBufferPool(3,4);
        using var twiddles=new NttTwiddleBufferPool();
        using var plans=new SharedNttTwiddlePlans(twiddles,avx2,!persistent&&avx2&&System.Runtime.Intrinsics.X86.Avx512F.IsSupported);
        using var workers=new FixedWorkerTeam(workerCount,pool,plans,persistent);
        var diagnostics=new PowerDiagnosticsCollector();
        var arithmetic=new BinaryPowerWorkspace(workers,diagnostics,1024,default,cached);
        int passed=0;
        var random=new Random(117);
        foreach(int length in new[]{499,500,501,511,512,513,1023,1024,1025,1501,2048,2049})
        foreach(bool allOnes in new[]{true,false})
        {
            uint[] limbs=new uint[length];
            for(int i=0;i<length;i++)limbs[i]=allOnes?65535U:(uint)random.Next(65536);
            limbs[^1]|=0x8000;
            var value=new BinaryMagnitude(limbs,length);
            BigInteger native=PackShiftedBinary(value,0,default);
            for(int repeat=0;repeat<2;repeat++)
            {
                var squared=arithmetic.Square(value);
                if(PackShiftedBinary(squared,0,default)!=native*native)throw new Exception("Binary square/CRT/reused pair buffer mismatch");
                passed++;
            }
        }
        if((diagnostics.CreateSnapshot(workerCount).LargeForwardTransformSavedCount>0)!=cached)
            throw new Exception("Incorrect spectrum reuse diagnostics");
        using var cancelled=new CancellationTokenSource();
        cancelled.Cancel();
        try { new BinaryPowerWorkspace(workers,diagnostics,1024,cancelled.Token,cached).Square(new(new uint[2049],2049));
            throw new Exception("Cancelled cached square completed"); }
        catch(OperationCanceledException){passed++;}
        if(cached)
        {
            using var during=new CancellationTokenSource();
            uint[] activeLimbs=new uint[(1<<20)+1]; Array.Fill(activeLimbs,65535U);
            during.CancelAfter(5);
            try { new BinaryPowerWorkspace(workers,diagnostics,1<<20,during.Token,true)
                    .Square(new(activeLimbs,activeLimbs.Length)); throw new Exception("Active cached NTT ignored cancellation"); }
            catch(OperationCanceledException){passed++;}
            // Reusing the team after cancellation also checks that exception
            // paths return cached spectra/product leases instead of deadlocking.
            uint[] retryLimbs=new uint[1025]; Array.Fill(retryLimbs,65535U);
            var retry=arithmetic.Square(new(retryLimbs,retryLimbs.Length));
            if(PackShiftedBinary(retry,0,default)!=BigInteger.Pow((BigInteger.One<<(1025*16))-1,2))
                throw new Exception("Worker reuse after cancellation failed");
            passed++;
        }
        totalPassed+=passed;
        }
        if(SelectBinaryTransformLength(1_000_000,32L<<30)!=SmallBinaryTransformLength ||
            SelectBinaryTransformLength(1_800_000_000,32L<<30)!=MaximumBinaryTransformLength ||
            SelectBinaryTransformLength(1_800_000_000,0)!=SmallBinaryTransformLength)
            throw new Exception("Adaptive memory policy mismatch");
        return totalPassed+1;
    }
}
