using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#if ANDROID
using System.Runtime.Intrinsics.Arm;
#endif
using System.Threading;

namespace MathSolver.Numerics;

/// <summary>Exact tiled carry operations for uint slots containing 16-bit limbs.</summary>
internal static class BinaryRadix16
{
    internal const int TileLength = 1 << 14;
    internal const int LegacyCarryLookaheadTileLength = 1 << 9; // persistent >10M checkpoint
    internal const int CarryLookaheadTileLength = 1 << 6; // 64 limbs; <=10M second-level prefix

    // execute joins all ranges before returning, using the caller's worker team.
    // <=10M binary-power carry uses 64-limb micro-tiles: each tile is first
    // normalized with carry-in zero, then its exact bOut = G | (P & bIn)
    // transfer is prefix-composed. This bounds the remaining ordered chain to
    // 64 radix-2^16 digits instead of 16K while preserving exact carry.
    internal static ulong NormalizeTiles(uint[] destination, int offset, int count,
        ulong incomingCarry, Action<int, Action<int, int>> execute,
        Func<int, int, ulong> normalize, CancellationToken token, int carryTileLength,
        bool useAvx512, bool useAvx2, bool useSse2, bool useNeon)
    {
        token.ThrowIfCancellationRequested();
        if (count <= 0) return incomingCarry;

        if (carryTileLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(carryTileLength));
        int tileLength = carryTileLength;
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
            if (carryTileLength == LegacyCarryLookaheadTileLength)
            {
                // Persistent >10M deliberately keeps the accepted byte-prefix
                // implementation byte-for-byte in behavior.
                byte[] generate = ArrayPool<byte>.Shared.Rent(carryBitCount);
                byte[] propagate = ArrayPool<byte>.Shared.Rent(carryBitCount);
                try
                {
                    Array.Clear(generate, 0, carryBitCount);
                    Array.Clear(propagate, 0, carryBitCount);

                    generate[0] = WouldOverflow(
                        destination, offset, Math.Min(tileLength, count), incomingCarry,
                        token, false, false, false, false) ? (byte)1 : (byte)0;

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
                                    destination, offset + start, tileCount, baseCarry,
                                    token, false, false, false, false);
                                generate[tile] = g ? (byte)1 : (byte)0;
                                if (!g)
                                {
                                    bool p = WouldOverflow(
                                        destination, offset + start, tileCount,
                                        checked(baseCarry + 1UL), token,
                                        false, false, false, false);
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

            // <=10M: build G/P directly in packed ulong bitsets.  This removes
            // both byte arrays and the byte->word->byte conversion around the
            // Kogge-Stone prefix.  Each worker owns complete words, so no atomic
            // bit updates are required.
            int wordCount = (carryBitCount + 63) >> 6;
            ulong[] generateWords = ArrayPool<ulong>.Shared.Rent(wordCount);
            ulong[] propagateWords = ArrayPool<ulong>.Shared.Rent(wordCount);
            try
            {
                Array.Clear(generateWords, 0, wordCount);
                Array.Clear(propagateWords, 0, wordCount);

                if (WouldOverflow(
                    destination, offset, Math.Min(tileLength, count), incomingCarry,
                    token, useAvx512, useAvx2, useSse2, useNeon))
                {
                    generateWords[0] = 1UL;
                }

                execute(wordCount, (wordFrom, wordTo) =>
                {
                    for (int word = wordFrom; word < wordTo; word++)
                    {
                        int firstBit = word << 6;
                        int lastBit = Math.Min(carryBitCount, firstBit + 64);
                        int bitIndex = Math.Max(1, firstBit);
                        ulong gWord = word == 0 ? generateWords[0] : 0UL;
                        ulong pWord = 0UL;

                        for (; bitIndex < lastBit; bitIndex++)
                        {
                            int tile = bitIndex;
                            int start = tile * tileLength;
                            int tileCount = Math.Min(tileLength, count - start);
                            ulong baseCarry = carries[tile - 1];
                            ulong bit = 1UL << (bitIndex & 63);

                            bool g = WouldOverflow(
                                destination, offset + start, tileCount, baseCarry,
                                token, useAvx512, useAvx2, useSse2, useNeon);
                            if (g)
                            {
                                gWord |= bit;
                            }
                            else if (WouldOverflow(
                                destination, offset + start, tileCount,
                                checked(baseCarry + 1UL), token,
                                useAvx512, useAvx2, useSse2, useNeon))
                            {
                                pWord |= bit;
                            }
                        }

                        generateWords[word] = gWord;
                        propagateWords[word] = pWord;
                    }
                });

                PrefixGeneratePropagatePackedWords(
                    generateWords, propagateWords, carryBitCount);

                long finalOverflowBits = 0;
                int lastTile = tiles - 1;
                execute(tiles, (from, to) =>
                {
                    for (int tile = from; tile < to; tile++)
                    {
                        int start = tile * tileLength;
                        int tileCount = Math.Min(tileLength, count - start);
                        ulong prefixBit = tile == 0
                            ? 0UL
                            : GetPackedBit(generateWords, tile - 1);
                        ulong tileInput = tile == 0
                            ? incomingCarry
                            : checked(carries[tile - 1] + prefixBit);
                        ulong overflow = AddCarry(
                            destination, offset + start, tileCount, tileInput, token);

                        if (tile < lastTile)
                        {
                            System.Diagnostics.Debug.Assert(
                                overflow == GetPackedBit(generateWords, tile),
                                "Binary radix-16 packed carry prefix disagreed with tile reconciliation.");
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
                ArrayPool<ulong>.Shared.Return(generateWords, clearArray: false);
                ArrayPool<ulong>.Shared.Return(propagateWords, clearArray: false);
            }
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(carries, clearArray: false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetPackedBit(ulong[] words, int bitIndex) =>
        (words[bitIndex >> 6] >> (bitIndex & 63)) & 1UL;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void PrefixGeneratePropagatePackedWords(
        ulong[] generateWords,
        ulong[] propagateWords,
        int count)
    {
        if (count <= 1) return;
        int wordCount = (count + 63) >> 6;
        int tailBits = count & 63;
        ulong tailMask = tailBits == 0 ? ulong.MaxValue : (1UL << tailBits) - 1UL;

        for (int distance = 1; distance < count; distance <<= 1)
        {
            int wordShift = distance >> 6;
            int bitShift = distance & 63;
            for (int word = wordCount - 1; word >= 0; word--)
            {
                ulong oldG = generateWords[word];
                ulong oldP = propagateWords[word];
                ulong shiftedG = 0;
                ulong shiftedP = 0;
                int sourceWord = word - wordShift;
                if (sourceWord >= 0)
                {
                    shiftedG = generateWords[sourceWord] << bitShift;
                    shiftedP = propagateWords[sourceWord] << bitShift;
                    if (bitShift != 0 && sourceWord > 0)
                    {
                        shiftedG |= generateWords[sourceWord - 1] >> (64 - bitShift);
                        shiftedP |= propagateWords[sourceWord - 1] >> (64 - bitShift);
                    }
                }

                int wordStart = word << 6;
                ulong targetMask;
                if (distance <= wordStart) targetMask = ulong.MaxValue;
                else if (distance >= wordStart + 64) targetMask = 0;
                else targetMask = ~((1UL << (distance - wordStart)) - 1UL);

                ulong newG = oldG | (oldP & shiftedG & targetMask);
                ulong newP = (oldP & ~targetMask) |
                             ((oldP & shiftedP) & targetMask);
                if (word == wordCount - 1)
                {
                    newG &= tailMask;
                    newP &= tailMask;
                }
                generateWords[word] = newG;
                propagateWords[word] = newP;
            }
        }
    }

    private static bool WouldOverflow(uint[] destination, int start, int count,
        ulong carry, CancellationToken token,
        bool useAvx512, bool useAvx2, bool useSse2, bool useNeon)
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

        // carry == 1 escapes iff every remaining digit is 0xffff.  Scan the
        // long all-ones run with the selected SIMD backend; AVX-512 deliberately
        // uses YMM here because CompareEqual+MoveMask is compact and avoids a
        // wider mask-extraction dependency for this tiny 64-limb micro-tile.
        ref uint data = ref MemoryMarshal.GetArrayDataReference(destination);

        if ((useAvx512 || useAvx2) && Avx2.IsSupported)
        {
            Vector256<int> max = Vector256.Create(unchecked((int)0xffffU));
            int vectorEnd = end - Vector256<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector256<int> values =
                    Vector256.LoadUnsafe(ref data, (nuint)index).AsInt32();
                Vector256<int> equal = Avx2.CompareEqual(values, max);
                if (Avx2.MoveMask(equal.AsByte()) != -1) return false;
                index += Vector256<uint>.Count;
            }
        }
        else if (useSse2 && Sse2.IsSupported)
        {
            Vector128<int> max = Vector128.Create(unchecked((int)0xffffU));
            int vectorEnd = end - Vector128<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector128<int> values =
                    Vector128.LoadUnsafe(ref data, (nuint)index).AsInt32();
                Vector128<int> equal = Sse2.CompareEqual(values, max);
                if (Sse2.MoveMask(equal.AsByte()) != 0xffff) return false;
                index += Vector128<uint>.Count;
            }
        }
#if ANDROID
        else if (useNeon && AdvSimd.IsSupported)
        {
            Vector128<uint> max = Vector128.Create(0xffffU);
            int vectorEnd = end - Vector128<uint>.Count + 1;
            while (index < vectorEnd)
            {
                Vector128<uint> equal = AdvSimd.CompareEqual(
                    Vector128.LoadUnsafe(ref data, (nuint)index), max);
                if (equal.GetElement(0) != uint.MaxValue ||
                    equal.GetElement(1) != uint.MaxValue ||
                    equal.GetElement(2) != uint.MaxValue ||
                    equal.GetElement(3) != uint.MaxValue)
                {
                    return false;
                }
                index += Vector128<uint>.Count;
            }
        }
#endif

        while (index < end)
        {
            if ((index & 0x3fff) == 0) token.ThrowIfCancellationRequested();
            if (destination[index] != 0xffffU) return false;
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
    // The SIMD paths pack adjacent radix-2^16 limbs into uint32 words before
    // applying the cross-word shift.  AVX-512 machines deliberately use YMM
    // here to avoid an unnecessary 512->256 compaction step; SSE2 and NEON
    // cover the 128-bit backends.
    internal static uint[] PackShifted(
        uint[] limbs,
        int count,
        int shift,
        Action<int, Action<int, int>> execute,
        bool useAvx2,
        bool useSse2,
        bool useNeon,
        CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shift);
        token.ThrowIfCancellationRequested();
        while (count > 0 && limbs[count - 1] == 0) count--;
        if (count == 0) return [];
        long bitLength = (long)(count - 1) * 16 +
                         32 - BitOperations.LeadingZeroCount(limbs[count - 1]) + shift;
        int length = checked((int)((bitLength + 31) / 32));
        if (length > Array.MaxLength / sizeof(uint))
            throw new ArgumentOutOfRangeException(nameof(shift));

        uint[] words = new uint[length];
        int offset = shift / 32;
        int bits = shift % 32;
        int inputCount = count;

        // Preserve the original scalar packer when SIMD is intentionally
        // disabled (including persistent >10M BinaryPower).
        if (!useAvx2 && !useSse2 && !useNeon)
        {
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
                    words[offset + word] = bits == 0
                        ? current
                        : (current << bits) | (previous >> (32 - bits));
                }
            });
            token.ThrowIfCancellationRequested();
            return words;
        }
        Vector256<int> evenIndices = Vector256.Create(0, 2, 4, 6, 0, 0, 0, 0);
        Vector256<int> oddIndices = Vector256.Create(1, 3, 5, 7, 0, 0, 0, 0);

        execute(length - offset, (from, to) =>
        {
            int word = from;

            // Keep word zero scalar when a cross-word shift is active because
            // its synthetic previous packed word is exactly zero.
            if (bits != 0 && word == 0 && word < to)
            {
                uint current = 0;
                if (inputCount > 0) current = limbs[0];
                if (inputCount > 1) current |= limbs[1] << 16;
                words[offset] = current << bits;
                word++;
            }

            if (useAvx2 && Avx2.IsSupported)
            {
                const int OutputsPerBatch = 4;
                while (word + OutputsPerBatch <= to)
                {
                    int inputIndex = word * 2;
                    if (inputIndex + 7 >= inputCount)
                        break;

                    Vector256<uint> currentLimbs =
                        Vector256.LoadUnsafe(ref limbs[inputIndex]);
                    Vector256<uint> currentEven = Avx2.PermuteVar8x32(
                        currentLimbs.AsInt32(), evenIndices).AsUInt32();
                    Vector256<uint> currentOdd = Avx2.PermuteVar8x32(
                        currentLimbs.AsInt32(), oddIndices).AsUInt32();
                    Vector256<uint> currentPacked = Avx2.Or(
                        currentEven,
                        Avx2.ShiftLeftLogical(currentOdd, 16));

                    Vector128<uint> packed4 = currentPacked.GetLower();
                    if (bits == 0)
                    {
                        packed4.StoreUnsafe(ref words[offset + word]);
                    }
                    else
                    {
                        Vector256<uint> previousLimbs =
                            Vector256.LoadUnsafe(ref limbs[inputIndex - 2]);
                        Vector256<uint> previousEven = Avx2.PermuteVar8x32(
                            previousLimbs.AsInt32(), evenIndices).AsUInt32();
                        Vector256<uint> previousOdd = Avx2.PermuteVar8x32(
                            previousLimbs.AsInt32(), oddIndices).AsUInt32();
                        Vector128<uint> previousPacked = Avx2.Or(
                            previousEven,
                            Avx2.ShiftLeftLogical(previousOdd, 16)).GetLower();

                        Vector128<uint> shifted = Sse2.Or(
                            Sse2.ShiftLeftLogical(packed4, (byte)bits),
                            Sse2.ShiftRightLogical(previousPacked, (byte)(32 - bits)));
                        shifted.StoreUnsafe(ref words[offset + word]);
                    }
                    word += OutputsPerBatch;
                }
            }

            if (useSse2 && Sse2.IsSupported)
            {
                const int OutputsPerBatch = 2;
                while (word + OutputsPerBatch <= to)
                {
                    int inputIndex = word * 2;
                    if (inputIndex + 3 >= inputCount)
                        break;

                    Vector128<uint> currentLimbs =
                        Vector128.LoadUnsafe(ref limbs[inputIndex]);
                    Vector128<uint> currentEven =
                        Sse2.Shuffle(currentLimbs.AsInt32(), 0xD8).AsUInt32(); // [0,2,1,3]
                    Vector128<uint> currentOdd =
                        Sse2.Shuffle(currentLimbs.AsInt32(), 0xDD).AsUInt32(); // [1,3,1,3]
                    Vector128<uint> packed = Sse2.Or(
                        currentEven,
                        Sse2.ShiftLeftLogical(currentOdd, 16));

                    uint packed0 = packed.GetElement(0);
                    uint packed1 = packed.GetElement(1);
                    if (bits == 0)
                    {
                        words[offset + word] = packed0;
                        words[offset + word + 1] = packed1;
                    }
                    else
                    {
                        int previousIndex = inputIndex - 2;
                        Vector128<uint> previousLimbs =
                            Vector128.LoadUnsafe(ref limbs[previousIndex]);
                        Vector128<uint> previousEven = Sse2.Shuffle(previousLimbs.AsInt32(), 0xD8).AsUInt32();
                        Vector128<uint> previousOdd = Sse2.Shuffle(previousLimbs.AsInt32(), 0xDD).AsUInt32();
                        Vector128<uint> previousPacked = Sse2.Or(
                            previousEven,
                            Sse2.ShiftLeftLogical(previousOdd, 16));
                        uint previous0 = previousPacked.GetElement(0);
                        uint previous1 = previousPacked.GetElement(1);
                        words[offset + word] =
                            (packed0 << bits) | (previous0 >> (32 - bits));
                        words[offset + word + 1] =
                            (packed1 << bits) | (previous1 >> (32 - bits));
                    }
                    word += OutputsPerBatch;
                }
            }

#if ANDROID
            if (useNeon && AdvSimd.Arm64.IsSupported)
            {
                const int OutputsPerBatch = 2;
                Vector128<ulong> low16Mask = Vector128.Create(0xffffUL);
                Vector128<ulong> low32Mask = Vector128.Create(0xffff_ffffUL);
                while (word + OutputsPerBatch <= to)
                {
                    int inputIndex = word * 2;
                    if (inputIndex + 3 >= inputCount)
                        break;

                    Vector128<ulong> current64 =
                        Vector128.LoadUnsafe(ref limbs[inputIndex]).AsUInt64();
                    Vector128<ulong> currentPacked = Vector128.BitwiseOr(
                        Vector128.BitwiseAnd(current64, low16Mask),
                        AdvSimd.ShiftLeftLogical(
                            AdvSimd.ShiftRightLogical(current64, 32), 16));

                    Vector128<ulong> result = currentPacked;
                    if (bits != 0)
                    {
                        Vector128<ulong> previous64 =
                            Vector128.LoadUnsafe(ref limbs[inputIndex - 2]).AsUInt64();
                        Vector128<ulong> previousPacked = Vector128.BitwiseOr(
                            Vector128.BitwiseAnd(previous64, low16Mask),
                            AdvSimd.ShiftLeftLogical(
                                AdvSimd.ShiftRightLogical(previous64, 32), 16));
                        result = Vector128.BitwiseAnd(
                            Vector128.BitwiseOr(
                                AdvSimd.ShiftLeftLogical(currentPacked, (byte)bits),
                                AdvSimd.ShiftRightLogical(previousPacked, (byte)(32 - bits))),
                            low32Mask);
                    }

                    words[offset + word] = (uint)result.GetElement(0);
                    words[offset + word + 1] = (uint)result.GetElement(1);
                    word += OutputsPerBatch;
                }
            }
#endif

            for (; word < to; word++)
            {
                if ((word & 0x3fff) == 0) token.ThrowIfCancellationRequested();
                int i = word * 2;
                uint current = i < inputCount ? limbs[i] : 0;
                if (i + 1 < inputCount) current |= limbs[i + 1] << 16;
                uint previous = i >= 2 ? limbs[i - 2] : 0;
                if (i >= 1 && i - 1 < inputCount) previous |= limbs[i - 1] << 16;
                words[offset + word] = bits == 0
                    ? current
                    : (current << bits) | (previous >> (32 - bits));
            }
        });
        token.ThrowIfCancellationRequested();
        return words;
    }
}
