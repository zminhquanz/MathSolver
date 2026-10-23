using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixFreshMemorySnapshots()
    {
        const long gib = 1024L * 1024 * 1024;
        // A 32 GiB machine can have 6 GiB reusable RAM while the old 90% GC
        // high-load threshold leaves a much smaller gap. That threshold is not free RAM.
        long available = MatrixMemoryGuard.MeasureHeadroom(32 * gib, gib, 29 * gib, 26 * gib,
            physicalAvailable: 6 * gib, commitAvailable: 7 * gib);
        var check = MatrixMemoryGuard.CheckAvailable(2L * 16384 * 16384 * 9, available, gib, true);
        TestAssert.That(check.CanAllocate && available == 6 * gib,
            "Fresh OS free RAM must replace the GC high-load gap, avoiding a false refusal for the maximum pair.");
        long commitBound = MatrixMemoryGuard.MeasureHeadroom(32 * gib, gib, 29 * gib, 26 * gib, 6 * gib, 2 * gib);
        long heapBound = MatrixMemoryGuard.MeasureHeadroom(4 * gib, 3 * gib, 29 * gib, 26 * gib, 6 * gib, 7 * gib);
        TestAssert.That(commitBound == 2 * gib && heapBound == gib,
            "Fresh physical RAM must not bypass commit capacity or a configured managed-heap limit.");
        long fallback = MatrixMemoryGuard.MeasureHeadroom(32 * gib, gib, 29 * gib, 26 * gib, null, null);
        TestAssert.That(fallback == 3 * gib, "Unavailable platform counters must retain the conservative fallback.");
        TestAssert.That(MatrixMemoryGuard.MeasureHeadroom(32 * gib, gib, 29 * gib, 26 * gib, 0, 7 * gib) == 0,
            "An exhausted physical budget must remain a real refusal.");
    }
}
