namespace MathSolver.Numerics;

/// <summary>Formats an already-computed +/-10^k without allocating its decimal expansion.</summary>
internal static class PowerOfTenDecimalWriter
{
    public static void Write(TextWriter writer, int zeroCount, bool negative,
        int blockDigits, Action? blockWritten, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(zeroCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(blockDigits, 1);
        token.ThrowIfCancellationRequested();
        if (negative) writer.Write('-');
        writer.Write('1');
        string zeros = new('0', blockDigits);
        int count = Math.Min(zeroCount, blockDigits - 1);
        writer.Write(zeros.AsSpan(0, count));
        blockWritten?.Invoke();
        int remaining = zeroCount - count;
        while (remaining > 0)
        {
            token.ThrowIfCancellationRequested();
            count = Math.Min(remaining, blockDigits);
            writer.Write(zeros.AsSpan(0, count));
            remaining -= count;
            blockWritten?.Invoke();
        }
        token.ThrowIfCancellationRequested();
    }
}
