using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixFileTransferOwnership()
    {
        using var workspace = TestWorkspace.Temporary("input-transfer");
        var rejected = new MatrixDraft(3, 3, fileBacked: true, temporaryDirectory: workspace.DirectoryPath);
        string rejectedPath = rejected.Matrix.StoragePath!;
        using (var outcome = new MatrixDraftTransfer(rejected)) outcome.Dispose();
        TestAssert.That(!File.Exists(rejectedPath), "An unaccepted import/paste outcome must delete its staged backing file.");
        using var accepted = new MatrixDraft(3, 3, fileBacked: true, temporaryDirectory: workspace.DirectoryPath);
        accepted.TrySetCell(2, 2, "17");
        using (var outcome = new MatrixDraftTransfer(accepted, 2, 3))
        {
            TestAssert.That(outcome.Rows == 2 && outcome.Columns == 3, "Paste destination shape must survive staging.");
            outcome.Detach();
        }
        TestAssert.That(File.Exists(accepted.Matrix.StoragePath) && accepted.GetCellText(2, 2) == "17",
            "Disposing a consumed outcome must leave the accepted input owned and readable by the session.");
        string stagedPath;
        using (var staged = new MatrixDraftTransfer(accepted.WithPastedCells(0, 0, MatrixClipboardCodec.Read("99"))))
            stagedPath = staged.Draft!.Matrix.StoragePath!;
        TestAssert.That(!File.Exists(stagedPath) && accepted.GetCellText(0, 0) == "" && accepted.GetCellText(2, 2) == "17",
            "Canceling an unpublished paste must delete its clone and preserve the original input.");
    }
}
