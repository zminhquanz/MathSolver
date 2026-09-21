using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace MathSolver.Numerics;

// Experimental single-threaded square. One arena and one leaf accumulator per power.
internal sealed class CandidateSquare : IDisposable
{
    internal static int Limit = 4096;
    internal static int LeafSize = 64;
    internal static string Kernel = "avx2";
    internal static bool Difference;
    private readonly ushort[] arena;
    private readonly ulong[] coefficients;
    internal CandidateSquare(int capacity)
    {
        arena = ArrayPool<ushort>.Shared.Rent(checked(capacity * 8 + 256));
        coefficients = ArrayPool<ulong>.Shared.Rent(checked(capacity * 2 + 8));
    }
    public void Dispose() { ArrayPool<ushort>.Shared.Return(arena); ArrayPool<ulong>.Shared.Return(coefficients); }
    internal static bool CanSquare(int length) => length <= Limit;
    internal static bool CanMultiply(int left, int right) => Math.Min(left,right) <= 4 && left + right <= 2 * Limit + 8;
    internal ushort[] Square(ushort[] input, ulong[] unused, CancellationToken token)
    {
        ushort[] output = new ushort[checked(input.Length * 2)];
        Recurse(input, output, arena, token);
        int count = output.Length;
        while (count > 1 && output[count - 1] == 0) count--;
        if (count != output.Length) Array.Resize(ref output,count);
        return output;
    }
    private void Recurse(ReadOnlySpan<ushort> value, Span<ushort> output, Span<ushort> scratch, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (value.Length <= LeafSize) { Leaf(value,output,token); return; }
        if (Difference) { DifferenceSquare(value,output,scratch,token); return; }
        int split = (value.Length + 1) / 2;
        var low = value[..split]; var high = value[split..];
        int sumSize = split + 1;
        var sum = scratch[..sumSize];
        var middle = scratch.Slice(sumSize, 2 * sumSize);
        var remaining = scratch[(3 * sumSize)..];
        uint carry = 0;
        for (int i=0; i<split; i++)
        {
            uint digit = (uint)low[i] + (i<high.Length ? high[i] : 0U) + carry;
            sum[i] = (ushort)digit; carry = digit >> 16;
        }
        sum[split] = (ushort)carry;
        Recurse(low,output[..(2*split)],remaining,token);
        Recurse(high,output[(2*split)..],remaining,token);
        Recurse(sum,middle,remaining,token);
        Subtract(middle,output[..(2*split)]);
        Subtract(middle,output[(2*split)..]);
        carry=0;
        int j=0;
        for (; j<output.Length-split; j++)
        {
            uint digit=(uint)output[split+j]+(j<middle.Length ? middle[j] : 0U)+carry;
            output[split+j]=(ushort)digit; carry=digit>>16;
        }
        if(carry!=0) throw new InvalidOperationException("Square overflow");
        for(;j<middle.Length;j++) if(middle[j]!=0) throw new InvalidOperationException("Middle overflow");
    }
    private void DifferenceSquare(ReadOnlySpan<ushort> value, Span<ushort> output, Span<ushort> scratch, CancellationToken token)
    {
        int split=(value.Length+1)/2;
        var low=value[..split]; var high=value[split..];
        var difference=scratch[..split]; var middle=scratch.Slice(split,2*split+1); var remaining=scratch[(3*split+1)..];
        bool lowGreater=true;
        for(int i=split-1;i>=0;i--) { int d=low[i]-(i<high.Length?high[i]:0); if(d!=0){lowGreater=d>0;break;} }
        int borrow=0;
        for(int i=0;i<split;i++)
        {
            int d=lowGreater?low[i]-(i<high.Length?high[i]:0):(i<high.Length?high[i]:0)-low[i];
            d-=borrow; difference[i]=(ushort)d;borrow=d<0?1:0;
        }
        if(borrow!=0)throw new InvalidOperationException("Difference borrow");
        Recurse(low,output[..(2*split)],remaining,token);
        Recurse(high,output[(2*split)..],remaining,token);
        Recurse(difference,middle[..(2*split)],remaining,token); middle[^1]=0;
        long carry=0;
        for(int i=0;i<middle.Length;i++)
        {
            long d=(i<2*low.Length?output[i]:0L)+(i<2*high.Length?output[2*split+i]:0L)-middle[i]+carry;
            middle[i]=(ushort)d;carry=d>>16;
        }
        if(carry!=0)throw new InvalidOperationException("Difference middle carry");
        uint addCarry=0; int j=0;
        for(;j<output.Length-split;j++)
        {
            uint d=(uint)output[split+j]+(j<middle.Length?middle[j]:0U)+addCarry;
            output[split+j]=(ushort)d;addCarry=d>>16;
        }
        if(addCarry!=0)throw new InvalidOperationException("Difference add overflow");
        for(;j<middle.Length;j++)if(middle[j]!=0)throw new InvalidOperationException("Difference overflow");
    }
    private static void Subtract(Span<ushort> target, ReadOnlySpan<ushort> value)
    {
        int borrow=0;
        for(int i=0;i<target.Length;i++)
        {
            int digit=target[i]-(i<value.Length ? value[i] : 0)-borrow;
            target[i]=(ushort)digit; borrow=digit<0?1:0;
        }
        if(borrow!=0) throw new InvalidOperationException("Square underflow");
    }
    private void Leaf(ReadOnlySpan<ushort> value, Span<ushort> output, CancellationToken token)
    {
        Span<ulong> acc=coefficients.AsSpan(0,output.Length); acc.Clear();
        for(int i=0;i<value.Length;i++)
        {
            token.ThrowIfCancellationRequested();
            ushort scalar=value[i]; acc[2*i]+=(ulong)scalar*scalar;
            if(scalar==0)continue;
            int j=i+1;
            if(Kernel=="avx512" && Avx512F.IsSupported && Avx512DQ.IsSupported)
            {
                for(;j+8<=value.Length;j+=8)
                {
                    var u16=MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(value.Slice(j,8)));
                    var u32=Avx2.ConvertToVector256Int32(u16).AsUInt32();
                    var u64=Avx512F.ConvertToVector512UInt64(u32);
                    var product=Avx512DQ.MultiplyLow(u64,Vector512.Create((ulong)scalar*2));
                    var old=MemoryMarshal.Read<Vector512<ulong>>(MemoryMarshal.AsBytes(acc.Slice(i+j,8)));
                    var total=Avx512F.Add(old,product);
                    MemoryMarshal.Write(MemoryMarshal.AsBytes(acc.Slice(i+j,8)),in total);
                }
            }
            else if(Kernel!="scalar" && Avx2.IsSupported)
            {
                for(;j+16<=value.Length;j+=16)
                    AuditKernel.AccumulateSixteenProductsAvx2(acc,i+j,MemoryMarshal.Read<Vector256<ushort>>(MemoryMarshal.AsBytes(value.Slice(j,16))),Vector256.Create(scalar),true);
                if(j+8<=value.Length)
                {
                    AuditKernel.AccumulateEightProductsSse2(acc,i+j,MemoryMarshal.Read<Vector128<ushort>>(MemoryMarshal.AsBytes(value.Slice(j,8))),Vector128.Create(scalar),true);
                    j+=8;
                }
            }
            for(;j<value.Length;j++)acc[i+j]+=2UL*scalar*value[j];
        }
        ulong carry=0;
        for(int i=0;i<output.Length;i++) { ulong sum=acc[i]+carry; output[i]=(ushort)sum; carry=sum>>16; }
        if(carry!=0)throw new InvalidOperationException("Leaf overflow");
    }
}
