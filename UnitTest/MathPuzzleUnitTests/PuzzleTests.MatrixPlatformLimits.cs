using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixPlatformInputLimits()
    {
        IntegerMatrix Read(TextReader reader, int limit) => MatrixTextCodec.Read(reader, maximumDimension: limit);
        MatrixClipboardBlock Clipboard(string text, int limit) => MatrixClipboardCodec.Read(text, maximumDimension: limit);
        string mobileWide = string.Join('\t', Enumerable.Repeat("7", 8193));
        using (var oversized = new StringReader(mobileWide))
            TestAssert.That(TestAssert.Throws<MatrixInputException>(() => Read(oversized, 8192),
                "Mobile file/paste imports must reject 8193 columns before producing a matrix.").Code == MatrixInputError.TooLarge,
                "Platform overflow must retain the existing TooLarge input diagnosis.");
        TestAssert.Throws<MatrixInputException>(() => Clipboard(mobileWide, 8192), "Cell paste must enforce the same mobile cap.");
        string mobileTall = string.Concat(Enumerable.Repeat("7\n", 10000));
        using (var oversized = new StringReader(mobileTall))
        {
            TestAssert.Throws<MatrixInputException>(() => Read(oversized, 8192), "Mobile row limits must be checked while streaming.");
            TestAssert.That(oversized.ReadToEnd().Length > 1000, "A rejected mobile import must not consume/stage the entire oversized file.");
        }
        TestAssert.Throws<MatrixInputException>(() => Clipboard(mobileTall, 8192), "Clipboard row count must also be capped on mobile.");
        foreach (int limit in new[] { 8192, 16384 })
        {
            string wide = string.Join('\t', Enumerable.Repeat("-9223372036854775808", limit));
            string tall = string.Concat(Enumerable.Repeat("0\n", limit));
            using var wideReader = new StringReader(wide);
            using var tallReader = new StringReader(tall);
            using var wideMatrix = Read(wideReader, limit);
            using var tallMatrix = Read(tallReader, limit);
            TestAssert.That(wideMatrix.GetValue(0, limit - 1) == long.MinValue && tallMatrix.Rows == limit,
                "The exact platform boundary must preserve all signed digits and the last row.");
            TestAssert.That(Clipboard(wide, limit).Columns == limit && Clipboard(tall, limit).Rows == limit,
                "Clipboard data at the configured platform boundary remains valid.");
        }
        TestAssert.That(MatrixLimits.GetInputMaximumDimension(false) == 8192 && MatrixLimits.GetInputMaximumDimension(true) == 16384,
            "Mobile inputs must be limited to 8192 while desktop benchmark capacity retains 16384.");
        foreach (int invalid in new[] { 0, -1, 16385, int.MaxValue })
        {
            using var reader = new StringReader("1");
            TestAssert.Throws<ArgumentOutOfRangeException>(() => Read(reader, invalid), "Invalid limits must be rejected before input staging.");
            TestAssert.Throws<ArgumentOutOfRangeException>(() => Clipboard("1", invalid), "Clipboard limits cannot exceed the engine ceiling.");
        }
        Console.WriteLine("Checked mobile/desktop matrix boundaries, early streaming rejection, whole-table/cell clipboard limits and exact integer data.");
    }
}
