using System.Globalization;
using System.Numerics;

namespace MathSolver.Numerics;

/// <summary>Streams decimal digits from a materialized integer using bounded leaves.</summary>
internal static class BigIntegerDecimalWriter
{
    internal static void Write(TextWriter writer, BigInteger magnitude, int digitCount,
        int leafDigits, Action blockWritten, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(blockWritten);
        ArgumentOutOfRangeException.ThrowIfNegative(magnitude.Sign);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(digitCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leafDigits);
        var powers = new Dictionary<int, BigInteger>();
        WriteCore(magnitude, digitCount, false);
        token.ThrowIfCancellationRequested();

        void WriteCore(BigInteger value, int width, bool pad)
        {
            token.ThrowIfCancellationRequested();
            if (width <= leafDigits)
            {
                string text = value.ToString(CultureInfo.InvariantCulture);
                if (pad && text.Length < width) writer.Write(new string('0', width - text.Length));
                writer.Write(text);
                token.ThrowIfCancellationRequested();
                blockWritten();
                return;
            }
            int lowWidth = width / 2;
            if (!powers.TryGetValue(lowWidth, out BigInteger divisor))
            {
                divisor = BigInteger.Pow(10, lowWidth);
                powers[lowWidth] = divisor;
            }
            BigInteger high = BigInteger.DivRem(value, divisor, out BigInteger low);
            WriteCore(high, width - lowWidth, pad);
            WriteCore(low, lowWidth, true);
        }
    }

    internal static int CountBlocks(int digitCount, int leafDigits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(digitCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(leafDigits);
        return digitCount <= leafDigits ? 1 :
            checked(CountBlocks(digitCount - digitCount / 2, leafDigits) + CountBlocks(digitCount / 2, leafDigits));
    }
}
