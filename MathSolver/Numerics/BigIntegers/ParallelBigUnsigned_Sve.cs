#if ANDROID && NET11_0_OR_GREATER
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;

namespace MathSolver.Numerics;

// The policy admits only the current CoreCLR retail SVE width, 128 bits.
// Fixed 128-bit byref loads and stores can therefore hold one full SVE vector.
#pragma warning disable SYSLIB5003 // .NET 11 SVE intrinsics are experimental.
internal sealed partial class ParallelBigUnsigned
{
    private readonly struct SveNtt { }
    private readonly struct Sve2Ntt { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NttKernelIsa SveIsa<T>() where T : struct =>
        typeof(T) == typeof(Sve2Ntt) ? NttKernelIsa.Sve2 : NttKernelIsa.Sve;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> ReduceOnceSve(Vector<uint> value, Vector<uint> modulus) =>
        Sve.Min(value, Sve.Subtract(value, modulus));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> SubtractModuloSve(Vector<uint> left, Vector<uint> right, Vector<uint> modulus)
    {
        var difference = Sve.Subtract(left, right);
        return Sve.ConditionalSelect(Sve.CompareLessThan(left, right), Sve.Add(difference, modulus), difference);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> MultiplyHighSve<T>(Vector<uint> left, Vector<uint> right) where T : struct
    {
        if (typeof(T) == typeof(Sve2Ntt))
        {
            var even = Sve2.MultiplyWideningEven(left, right);
            var odd = Sve2.MultiplyWideningOdd(left, right);
            var highEven = Sve2.ShiftRightLogicalNarrowingEven(even, 32);
            return Sve2.ShiftRightLogicalNarrowingOdd(highEven, odd, 32);
        }

        var lower = Sve.Multiply(Sve.ZeroExtendWideningLower(left), Sve.ZeroExtendWideningLower(right));
        var upper = Sve.Multiply(Sve.ZeroExtendWideningUpper(left), Sve.ZeroExtendWideningUpper(right));
        // In each 64-bit product, the high word is in the odd 32-bit slot.
        return Sve.UnzipOdd(Vector.AsVectorUInt32(lower), Vector.AsVectorUInt32(upper));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> MultiplyShoupSve<T>(Vector<uint> value, Vector<uint> multiplier,
        Vector<uint> multiplierShoup, Vector<uint> modulus) where T : struct
    {
        var quotient = MultiplyHighSve<T>(value, multiplierShoup);
        var product = Sve.MultiplySubtract(Sve.Multiply(value, multiplier), quotient, modulus);
        return ReduceOnceSve(product, modulus);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteCachedGroupsSve<T>(uint[] values, uint modulus, uint[] twiddles, uint[] shoup,
        int group, int groupEnd, int half, int twiddleOffset, int first, int last, bool inverse) where T : struct
    {
        const int width = 4;
        var mod = Vector.Create(modulus);
        ref uint data = ref MemoryMarshal.GetArrayDataReference(values);
        ref uint roots = ref MemoryMarshal.GetArrayDataReference(twiddles);
        ref uint quotients = ref MemoryMarshal.GetArrayDataReference(shoup);
        if (last - first >= width)
            NttKernelExecution.Record(inverse ? NttKernelPhase.Inverse : NttKernelPhase.Forward, SveIsa<T>());

        for (; group < groupEnd; group += half * 2)
        {
            int j = first;
            for (; j <= last - width; j += width)
            {
                nuint leftIndex = (nuint)(group + j);
                nuint rightIndex = leftIndex + (nuint)half;
                var left = Vector.LoadUnsafe(ref data, leftIndex);
                var right = Vector.LoadUnsafe(ref data, rightIndex);
                var root = Vector.LoadUnsafe(ref roots, (nuint)(twiddleOffset + j));
                var quotient = Vector.LoadUnsafe(ref quotients, (nuint)(twiddleOffset + j));
                if (inverse) right = MultiplyShoupSve<T>(right, root, quotient, mod);
                var sum = ReduceOnceSve(Sve.Add(left, right), mod);
                var difference = SubtractModuloSve(left, right, mod);
                if (!inverse) difference = MultiplyShoupSve<T>(difference, root, quotient, mod);
                sum.StoreUnsafe(ref data, leftIndex);
                difference.StoreUnsafe(ref data, rightIndex);
            }

            for (; j < last; j++)
            {
                int index = group + j;
                uint left = values[index];
                uint right = values[index + half];
                uint root = twiddles[twiddleOffset + j];
                if (inverse) right = (uint)((ulong)right * root % modulus);
                uint sum = left + right;
                uint difference = left >= right ? left - right : left + modulus - right;
                values[index] = sum >= modulus ? sum - modulus : sum;
                values[index + half] = inverse ? difference : (uint)((ulong)difference * root % modulus);
            }
        }
    }

    private static void ExecuteCachedGroupsSve(NttKernelIsa isa, uint[] values, uint modulus,
        uint[] twiddles, uint[] shoup, int group, int groupEnd, int half, int twiddleOffset,
        int first, int last, bool inverse)
    {
        if (isa == NttKernelIsa.Sve2)
            ExecuteCachedGroupsSve<Sve2Ntt>(values, modulus, twiddles, shoup, group, groupEnd, half, twiddleOffset, first, last, inverse);
        else
            ExecuteCachedGroupsSve<SveNtt>(values, modulus, twiddles, shoup, group, groupEnd, half, twiddleOffset, first, last, inverse);
    }
}
#pragma warning restore SYSLIB5003
#endif
