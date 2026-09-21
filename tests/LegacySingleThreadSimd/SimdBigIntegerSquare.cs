using System.Buffers;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
namespace MathSolver.Numerics;

// Single-thread uint32 Karatsuba square with exact 96-bit leaf coefficients.
// See SINGLE_THREAD_LIMB32_REPORT.md for measured dispatch bounds.
internal sealed class SimdBigIntegerSquare : IDisposable
{
    private uint[] arena = [];
    private readonly ulong[] low;
    private readonly ulong[] high;
    private readonly int leaf;
    internal string Backend { get; }
    internal SimdBigIntegerSquare(bool useAvx512)
    {
        Backend = useAvx512 && Avx512F.IsSupported ? "avx512" : Avx2.IsSupported ? "avx2" : "scalar";
        leaf = Backend == "avx512" ? 256 : 128;
        low = ArrayPool<ulong>.Shared.Rent(2 * leaf);
        high = ArrayPool<ulong>.Shared.Rent(2 * leaf);
    }

    // Bounds measured at exponent 1M: 32K bits to 8M bits per square input.
    internal const int MinimumWords = 1024;
    internal const int MaximumWords = 262144;

    internal static bool CanSquare(BigInteger value)
    {
        long words = (value.GetBitLength() + 31) / 32;
        return words >= MinimumWords && words <= MaximumWords;
    }

    internal BigInteger SquareOrRuntime(BigInteger value, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return CanSquare(value) ? SquareBigInteger(value, token) : value * value;
    }

    internal BigInteger PowOrRuntime(BigInteger value, int exponent, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (exponent <= 1 || !BitOperations.IsPow2((uint)exponent))
            return BigInteger.Pow(value, exponent);
        int count = BitOperations.Log2((uint)exponent);
        long bits = value.GetBitLength();
        bool any = false;
        for (int i = 0; i < count; i++)
        {
            long words = (bits + 31) / 32;
            any |= words >= MinimumWords && words <= MaximumWords;
            bits *= 2;
        }
        if (!any) return BigInteger.Pow(value, exponent);
        for (int i = 0; i < count; i++) value = SquareOrRuntime(value, token);
        return value;
    }
    internal uint[] Square(ReadOnlySpan<uint> input,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        int needed=checked(8*input.Length+256);
        if(arena.Length<needed)
        {
            uint[] replacement=ArrayPool<uint>.Shared.Rent(needed);
            if(arena.Length>0)ArrayPool<uint>.Shared.Return(arena);
            arena=replacement;
        }
        uint[] output=new uint[checked(2*input.Length)];
        Recurse(input,output,arena,token);
        return output;
    }
    internal BigInteger SquareBigInteger(BigInteger value,CancellationToken token=default)
    {
        int bytes=value.GetByteCount(isUnsigned:true);
        uint[] input=ArrayPool<uint>.Shared.Rent((bytes+3)/4);
        try
        {
            var words=input.AsSpan(0,(bytes+3)/4); words.Clear();
            if(!value.TryWriteBytes(MemoryMarshal.AsBytes(words),out _,isUnsigned:true,isBigEndian:false))throw new InvalidOperationException("Import");
            var result=Square(words,token);
            return new BigInteger(MemoryMarshal.AsBytes(result.AsSpan()),isUnsigned:true,isBigEndian:false);
        }
        finally{ArrayPool<uint>.Shared.Return(input);}
    }
    public void Dispose()
    {
        if(arena.Length>0)ArrayPool<uint>.Shared.Return(arena);
        ArrayPool<ulong>.Shared.Return(low);ArrayPool<ulong>.Shared.Return(high);
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Recurse(ReadOnlySpan<uint> value,Span<uint> output,Span<uint> scratch,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(value.Length<=leaf){Leaf(value,output);return;}
        int split=(value.Length+1)/2;
        var a=value[..split];var b=value[split..];
        var difference=scratch[..split];var middle=scratch.Slice(split,2*split+1);var rest=scratch[(3*split+1)..];
        bool aGreater=true;
        for(int i=split-1;i>=0;i--){long d=(long)a[i]-(i<b.Length?b[i]:0U);if(d!=0){aGreater=d>0;break;}}
        long borrow=0;
        for(int i=0;i<split;i++)
        {
            long d=aGreater?(long)a[i]-(i<b.Length?b[i]:0U):(long)(i<b.Length?b[i]:0U)-a[i];
            d-=borrow;difference[i]=(uint)d;borrow=d<0?1:0;
        }
        if(borrow!=0)throw new InvalidOperationException("Borrow");
        Recurse(a,output[..(2*split)],rest,token);
        Recurse(b,output[(2*split)..],rest,token);
        Recurse(difference,middle[..(2*split)],rest,token);middle[^1]=0;
        long carry=0;
        for(int i=0;i<middle.Length;i++)
        {
            long d=(i<2*a.Length?(long)output[i]:0)+(i<2*b.Length?(long)output[2*split+i]:0)-middle[i]+carry;
            middle[i]=(uint)d;carry=d>>32;
        }
        if(carry!=0)throw new InvalidOperationException("Middle carry");
        ulong addCarry=0;int j=0;
        for(;j<output.Length-split;j++)
        {
            ulong d=(ulong)output[split+j]+(j<middle.Length?middle[j]:0U)+addCarry;
            output[split+j]=(uint)d;addCarry=d>>32;
        }
        if(addCarry!=0)throw new InvalidOperationException("Add overflow");
        for(;j<middle.Length;j++)if(middle[j]!=0)throw new InvalidOperationException("Middle overflow");
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Leaf(ReadOnlySpan<uint> value,Span<uint> output)
    {
        var lo=low.AsSpan(0,output.Length);var hi=high.AsSpan(0,output.Length);lo.Clear();hi.Clear();
        bool avx512=Backend=="avx512",avx2=Backend=="avx2";
        for(int i=0;i<value.Length;i++)
        {
            uint scalar=value[i];if(scalar==0)continue;
            int j=i+1;
            if(avx512)
            {
                var broadcast=Vector512.Create((ulong)scalar).AsUInt32();
                for(;j+8<=value.Length;j+=8)
                {
                    var input=MemoryMarshal.Read<Vector256<uint>>(MemoryMarshal.AsBytes(value.Slice(j,8)));
                    var product=Avx512F.Multiply(Avx512F.ConvertToVector512UInt64(input).AsUInt32(),broadcast);
                    var old=MemoryMarshal.Read<Vector512<ulong>>(MemoryMarshal.AsBytes(lo.Slice(i+j,8)));
                    var sum=Avx512F.Add(old,product);
                    var overflow=Vector512.LessThan(sum,old)&Vector512.Create(1UL);
                    var upper=MemoryMarshal.Read<Vector512<ulong>>(MemoryMarshal.AsBytes(hi.Slice(i+j,8)))+overflow;
                    MemoryMarshal.Write(MemoryMarshal.AsBytes(lo.Slice(i+j,8)),in sum);
                    MemoryMarshal.Write(MemoryMarshal.AsBytes(hi.Slice(i+j,8)),in upper);
                }
            }
            else if(avx2)
            {
                var broadcast=Vector256.Create((ulong)scalar).AsUInt32();
                for(;j+4<=value.Length;j+=4)
                {
                    var input=MemoryMarshal.Read<Vector128<uint>>(MemoryMarshal.AsBytes(value.Slice(j,4)));
                    var product=Avx2.Multiply(Avx2.ConvertToVector256Int64(input).AsUInt32(),broadcast);
                    var old=MemoryMarshal.Read<Vector256<ulong>>(MemoryMarshal.AsBytes(lo.Slice(i+j,4)));
                    var sum=Avx2.Add(old,product);
                    var overflow=Vector256.LessThan(sum,old)&Vector256.Create(1UL);
                    var upper=MemoryMarshal.Read<Vector256<ulong>>(MemoryMarshal.AsBytes(hi.Slice(i+j,4)))+overflow;
                    MemoryMarshal.Write(MemoryMarshal.AsBytes(lo.Slice(i+j,4)),in sum);
                    MemoryMarshal.Write(MemoryMarshal.AsBytes(hi.Slice(i+j,4)),in upper);
                }
            }
            for(;j<value.Length;j++)
            {
                ulong product=(ulong)scalar*value[j],old=lo[i+j];lo[i+j]=old+product;if(lo[i+j]<old)hi[i+j]++;
            }
        }
        // Cross-products were accumulated once. Double the exact 96-bit coefficient
        // here, then add the diagonal product. No 64-bit product bits are discarded.
        UInt128 carry=0;
        for(int i=0;i<output.Length;i++)
        {
            UInt128 coefficient=(((UInt128)hi[i]<<64)|lo[i])*2+carry;
            if((i&1)==0)coefficient+=(ulong)value[i/2]*value[i/2];
            output[i]=(uint)coefficient;carry=coefficient>>32;
        }
        if(carry!=0)throw new InvalidOperationException("Leaf carry");
    }
}
