namespace MathSolver.Numerics;

/// <summary>Scalar limb helpers and decimal multiplication; never schedules NTT or tasks.</summary>
internal static class LimbKaratsuba
{
    internal static uint[] MultiplyDecimal(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        left = Significant(left); right = Significant(right);
        if (left.Length == 0 || right.Length == 0) return [];
        const ulong radix = 10000;
        if (Math.Min(left.Length, right.Length) <= 32)
        {
            uint[] result = new uint[checked(left.Length + right.Length)];
            for (int i = 0; i < left.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                ulong carry = 0;
                for (int j = 0; j < right.Length; j++)
                {
                    if ((j & 0xffff) == 0) token.ThrowIfCancellationRequested();
                    ulong product = (ulong)left[i] * right[j] + result[i + j] + carry;
                    result[i + j] = (uint)(product % radix); carry = product / radix;
                }
                result[i + right.Length] = (uint)carry;
            }
            return Trim(result);
        }
        int split = Math.Max(left.Length, right.Length) / 2;
        int l = Math.Min(split, left.Length), r = Math.Min(split, right.Length);
        uint[] low = MultiplyDecimal(left[..l], right[..r], token);
        uint[] high = MultiplyDecimal(left[l..], right[r..], token);
        uint[] leftSum = Add(left[..l], left[l..], radix, token);
        uint[] rightSum = Add(right[..r], right[r..], radix, token);
        uint[] middle = MultiplyDecimal(leftSum, rightSum, token);
        SubtractInPlace(middle, low, radix, token);
        SubtractInPlace(middle, high, radix, token);
        uint[] combined = new uint[checked(left.Length + right.Length)];
        AddAt(combined, low, 0, radix, token);
        AddAt(combined, middle, split, radix, token);
        AddAt(combined, high, split * 2, radix, token);
        return Trim(combined);
    }

    internal static uint[] Add(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, ulong radix, CancellationToken token)
    {
        int length = Math.Max(left.Length, right.Length);
        uint[] result = new uint[checked(length + 1)];
        ulong carry = 0;
        for (int i = 0; i < length; i++)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            ulong sum = (i < left.Length ? left[i] : 0UL) + (i < right.Length ? right[i] : 0UL) + carry;
            carry = sum >= radix ? 1UL : 0;
            result[i] = (uint)(sum - carry * radix);
        }
        result[length] = (uint)carry;
        return Trim(result);
    }

    internal static void SubtractInPlace(Span<uint> target, ReadOnlySpan<uint> value, ulong radix, CancellationToken token)
    {
        long borrow = 0;
        for (int i = 0; i < target.Length; i++)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            long difference = target[i] - (i < value.Length ? (long)value[i] : 0) - borrow;
            borrow = difference < 0 ? 1 : 0;
            target[i] = (uint)(difference + borrow * (long)radix);
        }
        if (borrow != 0 || Significant(value).Length > target.Length)
            throw new InvalidOperationException("Karatsuba subtraction underflow.");
    }

    internal static void AddAt(Span<uint> target, ReadOnlySpan<uint> value, int offset, ulong radix, CancellationToken token)
    {
        value = Significant(value);
        ulong carry = 0;
        int i = 0;
        for (; i < value.Length || carry != 0; i++)
        {
            if ((i & 0xffff) == 0) token.ThrowIfCancellationRequested();
            ulong sum = target[offset + i] + (i < value.Length ? (ulong)value[i] : 0) + carry;
            carry = sum >= radix ? 1UL : 0;
            target[offset + i] = (uint)(sum - carry * radix);
        }
    }

    internal static uint[] Trim(uint[] value)
    {
        int length = Significant(value).Length;
        if (length != value.Length) Array.Resize(ref value, length);
        return value;
    }

    private static ReadOnlySpan<uint> Significant(ReadOnlySpan<uint> value)
    {
        int length = value.Length;
        while (length > 0 && value[length - 1] == 0) length--;
        return value[..length];
    }
}
