using System.Runtime.CompilerServices;

namespace MathSolver.Numerics;

internal sealed partial class ParallelBigUnsigned
{
    // floor(2^64 / p). Both production NTT primes are odd, so the value is
    // identical to ulong.MaxValue / p. For any ulong product,
    // high64(product * reciprocal) underestimates floor(product / p) by at most
    // one. One conditional subtraction therefore gives the exact residue.
    private const ulong FirstBarrettReciprocal = 9_162_596_893UL;
    private const ulong SecondBarrettReciprocal = 39_268_272_336UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReduceFirstProductBarrett(ulong product) =>
        ReduceProductBarrett(product, FirstModulus, FirstBarrettReciprocal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReduceSecondProductBarrett(ulong product) =>
        ReduceProductBarrett(product, SecondModulus, SecondBarrettReciprocal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReduceProductBarrett(ulong product, uint modulus, ulong reciprocal)
    {
        ulong quotient = Math.BigMul(product, reciprocal, out _);
        ulong remainder = product - quotient * modulus;
        if (remainder >= modulus)
            remainder -= modulus;
        return (uint)remainder;
    }

    // The NTT uses only two fixed primes. Keep the generic fallback exact for
    // any future modulus instead of silently applying the wrong reciprocal.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReduceNttProductBarrett(ulong product, uint modulus) =>
        modulus == FirstModulus ? ReduceFirstProductBarrett(product) :
        modulus == SecondModulus ? ReduceSecondProductBarrett(product) :
        (uint)(product % modulus);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MultiplyNttBarrett(ulong left, ulong right, uint modulus) =>
        ReduceNttProductBarrett(left * right, modulus);
}
