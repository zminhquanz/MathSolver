using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // Inputs are normalized base-10,000 limbs and multiplicity is 1 or 2.
    // Raw sums <=29,997 need at most two subtractions. Carry stays <=2.
    // Only the independent raw sum/decomposition is vectorized; inter-lane
    // carry and inter-tile reconciliation preserve the original ordering.
    private static ulong AccumulateNormalizedSegmentTile(
        uint[] destination, int destinationStart, uint[] product, int productStart,
        int count, int multiplicity, FixedWorkerTeam workers, CancellationToken token)
    {
        if (destinationStart < 0 || count > destination.Length - destinationStart)
            throw new InvalidOperationException("Segmented NTT accumulation exceeded the result buffer.");
        int offset = 0;
        ulong carry = 0;
        bool vector = Vector128.IsHardwareAccelerated &&
            ((workers.UseAvx2Ntt && Avx2.IsSupported) ||
             (workers.UseSseNtt && Sse2.IsSupported) || workers.UseNeonNtt);
        if (vector)
        {
            ref uint dst = ref MemoryMarshal.GetArrayDataReference(destination);
            ref uint src = ref MemoryMarshal.GetArrayDataReference(product);
            var limit = Vector128.Create((int)LimbBase - 1);
            var radix = Vector128.Create(LimbBase);
            var one = Vector128.Create(1u);
            Span<uint> remainders = stackalloc uint[4];
            Span<uint> quotients = stackalloc uint[4];
            for (; offset <= count - 4; offset += 4)
            {
                if ((offset & 0xFFFF) == 0) token.ThrowIfCancellationRequested();
                var p = Vector128.LoadUnsafe(ref src, (nuint)(productStart + offset));
                if (multiplicity == 2) p += p;
                var sum = Vector128.LoadUnsafe(ref dst, (nuint)(destinationStart + offset)) + p;
                var first = Vector128.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= first & radix;
                var second = Vector128.GreaterThan(sum.AsInt32(), limit).AsUInt32();
                sum -= second & radix;
                var q = (first & one) + (second & one);
                sum.CopyTo(remainders);
                q.CopyTo(quotients);
                for (int lane = 0; lane < 4; lane++)
                {
                    ulong digit = remainders[lane] + carry;
                    bool overflow = digit >= LimbBase;
                    destination[destinationStart + offset + lane] =
                        (uint)(overflow ? digit - LimbBase : digit);
                    carry = quotients[lane] + (overflow ? 1UL : 0UL);
                }
            }
        }
        for (; offset < count; offset++)
        {
            if ((offset & 0xFFFF) == 0) token.ThrowIfCancellationRequested();
            ulong value = destination[destinationStart + offset] +
                (ulong)product[productStart + offset] * (uint)multiplicity + carry;
            carry = value / LimbBase;
            destination[destinationStart + offset] = (uint)(value - carry * LimbBase);
        }
        return carry;
    }
}
