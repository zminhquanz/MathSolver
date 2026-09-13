using System.Buffers;
using System.Numerics;

namespace MathSolver.Numerics;

/// <summary>Exact tiled carry operations for uint slots containing 16-bit limbs.</summary>
internal static class BinaryRadix16
{
    internal const int TileLength = 1 << 14;

    // execute joins all ranges before returning, using the caller's worker team.
    // Each tile starts with zero carry. Once joined, inject the real carry into
    // its normalized digits; only an overflow is added to that tile's carry-out.
    // This also handles arbitrarily long runs of 0xffff (no fixed carry lookahead).
    internal static ulong NormalizeTiles(uint[] destination, int offset, int count,
        ulong incomingCarry, Action<int, Action<int, int>> execute,
        Func<int, int, ulong> normalize, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int tiles = (count + TileLength - 1) / TileLength;
        ulong[] carries = ArrayPool<ulong>.Shared.Rent(Math.Max(1, tiles));
        try
        {
            execute(tiles, (from, to) =>
            {
                for (int tile = from; tile < to; tile++)
                {
                    token.ThrowIfCancellationRequested();
                    int start = tile * TileLength;
                    carries[tile] = normalize(start, Math.Min(count, start + TileLength));
                }
            });
            ulong carry = incomingCarry;
            for (int tile = 0; tile < tiles; tile++)
            {
                token.ThrowIfCancellationRequested();
                int start = tile * TileLength;
                carry = AddCarry(destination, offset + start,
                    Math.Min(TileLength, count - start), carry, token);
                carry = checked(carries[tile] + carry);
            }
            return carry;
        }
        finally { ArrayPool<ulong>.Shared.Return(carries); }
    }

    internal static ulong AddCarry(uint[] destination, int start, int count,
        ulong carry, CancellationToken token)
    {
        for (int i = 0; i < count && carry != 0; i++)
        {
            if ((i & 0x3fff) == 0) token.ThrowIfCancellationRequested();
            ulong sum = destination[start + i] + carry;
            destination[start + i] = (uint)(sum & 0xffff);
            carry = sum >> 16;
        }
        return carry;
    }

    // Each packed output word depends only on its own input pair and the
    // previous pair. No carry chain or unshifted BigInteger is required.
    internal static uint[] PackShifted(uint[] limbs, int count, int shift,
        Action<int, Action<int, int>> execute, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shift);
        token.ThrowIfCancellationRequested();
        while (count > 0 && limbs[count - 1] == 0) count--;
        if (count == 0) return [];
        long bitLength = (long)(count - 1) * 16 + 32 - BitOperations.LeadingZeroCount(limbs[count - 1]) + shift;
        int length = checked((int)((bitLength + 31) / 32));
        if (length > Array.MaxLength / sizeof(uint)) throw new ArgumentOutOfRangeException(nameof(shift));
        uint[] words = new uint[length];
        int offset = shift / 32, bits = shift % 32;
        int inputCount = count;
        execute(length - offset, (from, to) =>
        {
            for (int word = from; word < to; word++)
            {
                if ((word & 0x3fff) == 0) token.ThrowIfCancellationRequested();
                int i = word * 2;
                uint current = i < inputCount ? limbs[i] : 0;
                if (i + 1 < inputCount) current |= limbs[i + 1] << 16;
                uint previous = i >= 2 ? limbs[i - 2] : 0;
                if (i >= 1 && i - 1 < inputCount) previous |= limbs[i - 1] << 16;
                words[offset + word] = bits == 0 ? current :
                    (current << bits) | (previous >> (32 - bits));
            }
        });
        token.ThrowIfCancellationRequested();
        return words;
    }
}
