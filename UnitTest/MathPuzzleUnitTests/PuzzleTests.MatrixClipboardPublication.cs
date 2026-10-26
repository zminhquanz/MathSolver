using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixClipboardPublication() => CheckMatrixClipboardPublicationAsync().GetAwaiter().GetResult();

    private static async Task CheckMatrixClipboardPublicationAsync()
    {
        int writes = 0, flushes = 0, waits = 0;
        Task Wait() { waits++; return Task.CompletedTask; }
        var copied = await MatrixClipboardWriter.TryWriteAsync(
            () => ++writes == 3, () => { flushes++; return true; }, () => true, Wait);
        Require(copied.Copied && copied.Persisted && writes == 3 && flushes == 1 && waits == 2,
            "A temporarily busy clipboard must retry without throwing and persist only after publication.");

        writes = flushes = waits = 0;
        var busy = await MatrixClipboardWriter.TryWriteAsync(
            () => { writes++; return false; }, () => { flushes++; return true; }, () => true, Wait);
        Require(!busy.Copied && !busy.Persisted && writes == 3 && flushes == 0 && waits == 2,
            "A clipboard that remains busy must terminate without flushing or claiming success.");

        writes = flushes = waits = 0;
        var unflushed = await MatrixClipboardWriter.TryWriteAsync(
            () => { writes++; return true; }, () => { flushes++; return false; }, () => true, Wait);
        Require(unflushed.Copied && !unflushed.Persisted && writes == 1 && flushes == 1 && waits == 0,
            "A native flush failure must preserve the successful live copy without republishing or throwing.");

        writes = flushes = waits = 0;
        bool active = false;
        var cancelled = await MatrixClipboardWriter.TryWriteAsync(
            () => { writes++; return true; }, () => { flushes++; return true; }, () => active, Wait);
        Require(!cancelled.Copied && writes == 0 && flushes == 0 && waits == 0,
            "Closing or changing tabs before publication must leave the clipboard untouched.");

        active = true;
        cancelled = await MatrixClipboardWriter.TryWriteAsync(
            () => { writes++; return false; }, () => { flushes++; return true; }, () => active,
            () => { active = false; waits++; return Task.CompletedTask; });
        Require(!cancelled.Copied && writes == 1 && flushes == 0 && waits == 1,
            "A stale copy must stop retrying after its tab or window becomes inactive.");
    }
}
