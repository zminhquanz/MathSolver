using System.Buffers;
using System.Numerics;
using System.Threading;

namespace MathSolver.Numerics;

/// <summary>Exact tiled carry operations for uint slots containing 16-bit limbs.</summary>
internal static class BinaryRadix16
{
    internal const int TileLength = 1 << 14;
    internal const int CarryLookaheadTileLength = 1 << 9; // 512 limbs

    // execute joins all ranges before returning, using the caller's worker team.
    // <=10M binary-power carry uses 512-limb micro-tiles: each tile is first
    // normalized with carry-in zero, then its exact bOut = G | (P & bIn)
    // transfer is prefix-composed. This bounds the remaining ordered chain to
    // 512 radix-2^16 digits instead of 16K while preserving exact carry.
    internal static ulong NormalizeTiles(uint[] destination, int offset, int count,
        ulong incomingCarry, Action<int, Action<int, int>> execute,
        Func<int, int, ulong> normalize, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (count <= 0) return incomingCarry;

        int tileLength = CarryLookaheadTileLength;
        int tiles = (count + tileLength - 1) / tileLength;
        ulong[] carries = ArrayPool<ulong>.Shared.Rent(Math.Max(1, tiles));
        try
        {
            execute(tiles, (from, to) =>
            {
                for (int tile = from; tile < to; tile++)
                {
                    token.ThrowIfCancellationRequested();
                    int start = tile * tileLength;
                    carries[tile] = normalize(start, Math.Min(count, start + tileLength));
                }
            });

            if (tiles == 1)
            {
                ulong overflow = AddCarry(destination, offset, count, incomingCarry, token);
                return checked(carries[0] + overflow);
            }

            int carryBitCount = tiles - 1;
            byte[] generate = ArrayPool<byte>.Shared.Rent(carryBitCount);
            byte[] propagate = ArrayPool<byte>.Shared.Rent(carryBitCount);
            try
            {
                Array.Clear(generate, 0, carryBitCount);
                Array.Clear(propagate, 0, carryBitCount);

                generate[0] = WouldOverflow(
                    destination, offset, Math.Min(tileLength, count), incomingCarry, token)
                    ? (byte)1 : (byte)0;

                if (carryBitCount > 1)
                {
                    execute(carryBitCount - 1, (from, to) =>
                    {
                        for (int relative = from; relative < to; relative++)
                        {
                            int tile = relative + 1;
                            int start = tile * tileLength;
                            int tileCount = Math.Min(tileLength, count - start);
                            ulong baseCarry = carries[tile - 1];

                            bool g = WouldOverflow(
                                destination, offset + start, tileCount, baseCarry, token);
                            generate[tile] = g ? (byte)1 : (byte)0;
                            if (!g)
                            {
                                bool p = WouldOverflow(
                                    destination, offset + start, tileCount,
                                    checked(baseCarry + 1UL), token);
                                propagate[tile] = p ? (byte)1 : (byte)0;
                            }
                        }
                    });
                }

                for (int distance = 1; distance < carryBitCount; distance <<= 1)
                {
                    for (int i = carryBitCount - 1; i >= distance; i--)
                    {
                        byte p = propagate[i];
                        generate[i] = (byte)(generate[i] | (p & generate[i - distance]));
                        propagate[i] = (byte)(p & propagate[i - distance]);
                    }
                }

                long finalOverflowBits = 0;
                int lastTile = tiles - 1;
                execute(tiles, (from, to) =>
                {
                    for (int tile = from; tile < to; tile++)
                    {
                        int start = tile * tileLength;
                        int tileCount = Math.Min(tileLength, count - start);
                        ulong tileInput = tile == 0
                            ? incomingCarry
                            : checked(carries[tile - 1] + generate[tile - 1]);
                        ulong overflow = AddCarry(
                            destination, offset + start, tileCount, tileInput, token);

                        if (tile < lastTile)
                        {
                            System.Diagnostics.Debug.Assert(overflow == generate[tile],
                                "Binary radix-16 carry prefix disagreed with tile reconciliation.");
                        }
                        else
                        {
                            Interlocked.Exchange(ref finalOverflowBits, unchecked((long)overflow));
                        }
                    }
                });

                return checked(carries[lastTile] +
                    unchecked((ulong)Volatile.Read(ref finalOverflowBits)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(generate, clearArray: false);
                ArrayPool<byte>.Shared.Return(propagate, clearArray: false);
            }
        }
        finally { ArrayPool<ulong>.Shared.Return(carries, clearArray: false); }
    }

    private static bool WouldOverflow(uint[] destination, int start, int count,
        ulong carry, CancellationToken token)
    {
        if (carry == 0 || count <= 0) return false;
        int end = checked(start + count);
        int index = start;

        while (carry > 1 && index < end)
        {
            carry = (destination[index] + carry) >> 16;
            index++;
        }

        if (carry == 0) return false;
        if (index >= end) return true;

        // carry == 1 escapes iff every remaining digit is 0xffff.
        while (index < end)
        {
            if ((index & 0x3fff) == 0) token.ThrowIfCancellationRequested();
            if (destination[index] != 0xffff) return false;
            index++;
        }
        return true;
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
