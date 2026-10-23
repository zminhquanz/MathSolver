using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Runtime.ExceptionServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixPasteMemoryPreflight()
    {
        using var original = new MatrixDraft(4, 4);
        original.TrySetCell(0, 0, "99"); original.TrySetCell(3, 3, "-7");
        var scalar = MatrixClipboardCodec.Read("42\r\n");
        int memoryExceptions = 0, checks = 0;
        EventHandler<FirstChanceExceptionEventArgs> observer = (_, args) =>
        { if (args.Exception is MatrixMemoryException) memoryExceptions++; };
        AppDomain.CurrentDomain.FirstChanceException += observer;
        try
        {
            foreach (var block in new[] { scalar, MatrixClipboardCodec.Read("1\t\r\n-2\t0\r\n") })
            {
                int rows = block.Rows == 1 ? 4 : 2, columns = block.Columns == 1 ? 4 : 2;
                var refused = original.TryWithPastedCells(0, 0, rows, columns, block, default, required =>
                {
                    checks++;
                    TestAssert.That(required > 0 && required < 1024 * 1024,
                        "Paste must budget bounded block scratch and metadata rather than whole-matrix clones.");
                    return new(required, 0);
                });
                TestAssert.That(refused.Draft is null && !refused.Memory.CanAllocate,
                    "Insufficient memory must return a refusal, not a staged draft or an exception.");
            }
            var reported = MatrixMemoryGuard.CheckAvailable(2415919104L, 1370492601L, 0, true);
            TestAssert.That(!reported.CanAllocate && reported.AvailableBytes == 1102057145L,
                "The reported 16384-square allocation must be rejected without allocating 2.25 GiB.");

            using var large = new MatrixDraft(16384, 16384);
            large.TrySetCell(16383, 16383, "7");
            Func<long, MatrixMemoryCheck> deny = required => new(required, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var largeRefusal = large.TryWithPastedCells(0, 0, 16384, 16384, scalar, default, deny);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestAssert.That(largeRefusal.Draft is null && largeRefusal.Memory.RequiredBytes < 1024 * 1024
                && allocated < 1024 * 1024 && large.GetCellText(16383, 16383) == "7" && large.MissingCount == 268435455,
                "A refused 16384-square constant fill must not allocate cell-sized buffers and must retain the source.");
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= observer; }
        TestAssert.That(checks == 2 && memoryExceptions == 0 && original.MissingCount == 14
            && original.GetCellText(0, 0) == "99" && original.GetCellText(3, 3) == "-7",
            "Refused scalar and rectangular pastes must retain every original value without first-chance memory exceptions.");

        using var success = original.TryWithPastedCells(0, 0, 4, 4, scalar);
        TestAssert.That(success.Memory.CanAllocate && success.Draft is { IsComplete: true } filled
            && filled.GetCellText(0, 0) == "42" && filled.GetCellText(3, 3) == "42" && original.MissingCount == 14,
            "A permitted whole-selection paste must still stage all values without changing the source.");
        using var rectangleAttempt = original.TryWithPastedCells(1, 1, 2, 2, MatrixClipboardCodec.Read("1\t\r\n-2\t0\r\n"),
            default, MatrixMemoryGuard.CheckAvailable);
        var rectangle = rectangleAttempt.Draft!;
        TestAssert.That(rectangle.GetCellText(0, 0) == "99" && rectangle.GetCellText(3, 3) == "-7"
            && rectangle.GetCellText(1, 1) == "1" && rectangle.GetCellText(1, 2) == ""
            && rectangle.GetCellText(2, 1) == "-2" && rectangle.GetCellText(2, 2) == "0" && rectangle.MissingCount == 11,
            "The non-throwing path must preserve surrounding cells, blanks and explicit zero.");
        Func<long, MatrixMemoryCheck> mustNotCheck = _ => throw new Exception("Rejected paste must not reach allocation planning.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        TestAssert.Throws<OperationCanceledException>(() => original.TryWithPastedCells(0, 0, 4, 4, scalar, cancelled.Token, mustNotCheck),
            "Cancellation must take precedence over memory planning.");
        TestAssert.Throws<MatrixInputException>(() => original.TryWithPastedCells(3, 3, 2, 2, scalar, default, mustNotCheck),
            "An out-of-bounds paste must be rejected before memory planning.");
        Console.WriteLine("Checked paste refusal without first-chance memory exceptions, reported 16384-square budget, intact source, scalar/rectangular staging and cancellation.");
    }
}
