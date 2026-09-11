using System.Numerics;
using System.Runtime.InteropServices;

namespace MathSolver.Numerics;

/// <summary>Owned, packed radix-2^32 magnitude. Bit lengths use Int64, not BigInteger.</summary>
internal sealed class LargeBinaryUnsigned
{
    private readonly uint[] words;
    internal ReadOnlyMemory<uint> Words => words;
    internal long StorageBytes => (long)words.Length * sizeof(uint);
    internal long BitLength => words.Length == 0 ? 0 :
        (long)(words.Length - 1) * 32 + 32 - BitOperations.LeadingZeroCount(words[^1]);

    private LargeBinaryUnsigned(uint[] ownedWords) => words = ownedWords;

    internal static LargeBinaryUnsigned FromWords(ReadOnlySpan<uint> value)
    {
        int count = value.Length;
        while (count > 0 && value[count - 1] == 0) count--;
        return new(value[..count].ToArray());
    }

    internal static LargeBinaryUnsigned ShiftWords(ReadOnlySpan<uint> value, long shift, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shift);
        token.ThrowIfCancellationRequested();
        int count = value.Length;
        while (count > 0 && value[count - 1] == 0) count--;
        if (count == 0) return new([]);
        long bitLength = checked((long)(count - 1) * 32 + 32 - BitOperations.LeadingZeroCount(value[count - 1]) + shift);
        int length = checked((int)((bitLength + 31) / 32));
        if (length > Array.MaxLength / sizeof(uint)) throw new ArgumentOutOfRangeException(nameof(shift));
        uint[] result = new uint[length];
        int offset = checked((int)(shift / 32)), bits = (int)(shift % 32);
        uint carry = 0;
        for (int i = 0; i < count; i++)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            ulong word = ((ulong)value[i] << bits) | carry;
            result[offset + i] = (uint)word;
            carry = (uint)(word >> 32);
        }
        if (carry != 0) result[offset + count] = carry;
        token.ThrowIfCancellationRequested();
        return new(result);
    }

    // The NTT uses radix 2^16 in uint slots. Pack and shift in one traversal;
    // never construct the unshifted or oversized shifted BigInteger.
    internal static LargeBinaryUnsigned ShiftRadix16(ReadOnlySpan<uint> value, int shift, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shift);
        token.ThrowIfCancellationRequested();
        int count = value.Length;
        while (count > 0 && value[count - 1] == 0) count--;
        if (count == 0) return new([]);
        long bitLength = (long)(count - 1) * 16 + 32 - BitOperations.LeadingZeroCount(value[count - 1]) + shift;
        int length = checked((int)((bitLength + 31) / 32));
        if (length > Array.MaxLength / sizeof(uint)) throw new ArgumentOutOfRangeException(nameof(shift));
        uint[] result = new uint[length];
        int offset = shift / 32, bits = shift % 32;
        uint carry = 0;
        for (int i = 0; i < count; i += 2)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            uint packed = value[i] | (i + 1 < count ? value[i + 1] << 16 : 0);
            ulong word = ((ulong)packed << bits) | carry;
            result[offset + i / 2] = (uint)word;
            carry = (uint)(word >> 32);
        }
        if (carry != 0) result[offset + (count + 1) / 2] = carry;
        token.ThrowIfCancellationRequested();
        return new(result);
    }

    internal BigInteger ToBigInteger()
    {
        if (words.Length > Array.MaxLength / 32)
            throw new InvalidOperationException("The binary magnitude exceeds BigInteger capacity.");
        return new BigInteger(MemoryMarshal.AsBytes(words.AsSpan()), isUnsigned: true, isBigEndian: false);
    }

    internal uint Remainder(uint divisor, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfZero(divisor);
        ulong remainder = 0;
        for (int i = words.Length - 1; i >= 0; i--)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            remainder = ((remainder << 32) | words[i]) % divisor;
        }
        return (uint)remainder;
    }

    internal static LargeBinaryUnsigned PowFiveAndShiftSingle(int exponent, Action<int, int>? progress,
        CancellationToken token, int leafWords = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        ArgumentOutOfRangeException.ThrowIfLessThan(leafWords, 2);
        token.ThrowIfCancellationRequested();
        if (exponent == 0) return new([1]);
        uint[] value = [5];
        int total = PowerOfTenArithmetic.OperationCount(exponent), done = 0;
        for (int bit = BitOperations.Log2((uint)exponent) - 1; bit >= 0; bit--)
        {
            value = Square(value, token, leafWords);
            progress?.Invoke(++done, total);
            if (((exponent >> bit) & 1) != 0)
            {
                token.ThrowIfCancellationRequested();
                ulong carry = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
                    ulong product = value[i] * 5UL + carry;
                    value[i] = (uint)product; carry = product >> 32;
                }
                if (carry != 0)
                {
                    Array.Resize(ref value, checked(value.Length + 1));
                    value[^1] = (uint)carry;
                }
                progress?.Invoke(++done, total);
            }
        }
        var result = ShiftWords(value, exponent, token);
        progress?.Invoke(total, total);
        token.ThrowIfCancellationRequested();
        return result;
    }

    // Karatsuba squaring over packed binary words, entirely on the calling
    // worker. Runtime BigInteger is used only for bounded leaf squares.
    internal static uint[] Square(ReadOnlySpan<uint> value, CancellationToken token, int leafWords = 4096)
    {
        token.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(leafWords, 2);
        int length = value.Length;
        while (length > 0 && value[length - 1] == 0) length--;
        value = value[..length];
        if (length == 0) return [];
        if (length <= leafWords)
        {
            BigInteger native = new(MemoryMarshal.AsBytes(value), isUnsigned: true, isBigEndian: false);
            BigInteger squared = native * native;
            token.ThrowIfCancellationRequested();
            uint[] result = new uint[checked((int)((squared.GetBitLength() + 31) / 32))];
            if (!squared.TryWriteBytes(MemoryMarshal.AsBytes(result.AsSpan()), out _, isUnsigned: true))
                throw new InvalidOperationException("Binary leaf packing failed.");
            return result;
        }
        int split = length / 2;
        uint[] low = Square(value[..split], token, leafWords);
        uint[] high = Square(value[split..], token, leafWords);
        if (low.Length == 0)
        {
            uint[] shifted = new uint[checked(high.Length + split * 2)];
            high.CopyTo(shifted.AsSpan(split * 2));
            token.ThrowIfCancellationRequested();
            return shifted;
        }
        uint[] sum = LimbKaratsuba.Add(value[..split], value[split..], 1UL << 32, token);
        uint[] middle = Square(sum, token, leafWords);
        LimbKaratsuba.SubtractInPlace(middle, low, 1UL << 32, token);
        LimbKaratsuba.SubtractInPlace(middle, high, 1UL << 32, token);
        uint[] combined = new uint[checked(length * 2)];
        LimbKaratsuba.AddAt(combined, low, 0, 1UL << 32, token);
        LimbKaratsuba.AddAt(combined, middle, split, 1UL << 32, token);
        LimbKaratsuba.AddAt(combined, high, split * 2, 1UL << 32, token);
        return LimbKaratsuba.Trim(combined);
    }
}
