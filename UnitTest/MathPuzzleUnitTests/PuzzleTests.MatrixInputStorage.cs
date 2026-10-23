using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixInputStorage()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var draft = new MatrixDraft(1024, 1024);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestAssert.That(allocated < 1024 * 1024, $"An empty 1024-square draft must allocate under 1 MiB, observed {allocated} bytes.");
        TestAssert.That(draft.MissingCount == 1024 * 1024 && !draft.HasAnyValues, "Empty paged drafts preserve missing cells.");
        TestAssert.That(draft.Matrix.DenseData is null && !draft.Matrix.IsFileBacked, "Empty drafts have no dense array or temporary file.");
        TestAssert.Throws<InvalidOperationException>(() => _ = draft.Matrix.Data, "Paged Data must not materialize input.");
        using var largest = new MatrixDraft(16384, 16384);
        before = GC.GetAllocatedBytesForCurrentThread();
        using var filled = largest.WithPastedCells(0, 0, largest.Rows, largest.Columns, MatrixClipboardCodec.Read("-7"));
        long fillAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestAssert.That(fillAllocated < 1024 * 1024 && filled.IsComplete && filled.Matrix.StoredBytes == 0
            && filled.Matrix.TryGetUniformValue(out long uniform) && uniform == -7 && filled.GetCellText(16383, 16383) == "-7",
            "A maximum-size scalar fill must be metadata-only with exact uniform proof.");
        using var patched = filled.WithPastedCells(0, 4095, MatrixClipboardCodec.Read("0\t\t9\r\n3\t4\t5"));
        TestAssert.That(patched.MissingCount == 1 && patched.GetCellText(0, 4095) == "0" && patched.GetCellText(0, 4096) == ""
            && patched.GetCellText(0, 4097) == "9" && patched.GetCellText(1, 4096) == "4"
            && filled.GetCellText(0, 4096) == "-7" && largest.MissingCount == 268435456,
            "A cross-block rectangle preserves blank versus zero and leaves both source generations unchanged.");
        using var zeros = patched.WithMissingFilledWithZero();
        TestAssert.That(zeros.IsComplete && zeros.GetCellText(0,4096) == "0" && patched.GetCellText(0,4096) == "",
            "Fill-missing snapshots change only occupancy and retain the original blanks.");
        Span<long> panel = stackalloc long[5];
        patched.Matrix.CopyRowTo(0, 4094, panel);
        TestAssert.That(panel.SequenceEqual(new long[] {-7, 0, 0, 9, -7}), "Row panels cross block boundaries exactly.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => patched.Matrix.GetValue(-1,0), "Paged reads validate row bounds.");
        using var dense = new IntegerMatrix(1, 2, [7,7]);
        TestAssert.That(!dense.TryGetUniformValue(out _) && dense.DenseData == dense.Data, "Mutable dense inputs cannot claim metadata proof.");
        Console.WriteLine($"Empty1024-square allocation={allocated}; full16384-square fill allocation={fillAllocated}; patchedStored={patched.Matrix.StoredBytes}; cache={patched.Matrix.CachedBytes}/{patched.Matrix.CacheLimitBytes}.");
        CheckInputStorageFiles();
    }

    private static void CheckInputStorageFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "matrix-input-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var builder = new MatrixInputBuilder(5000, temporaryDirectory: directory, cacheLimitBytes: 65536))
            {
                long[] row = Enumerable.Range(0,5000).Select(i => (long)i - 2500).ToArray();
                for (int r = 0; r < 5; r++) builder.AddRow(row);
                var matrix = builder.Complete();
                TestAssert.That(matrix.Rows == 5 && matrix.GetValue(4,4999) == 2499 && matrix.StoredBytes == 200000,
                    "Streaming builder finalizes non-block-aligned dimensions and exact row values.");
                var original = new MatrixDraft(matrix);
                var changed = original.WithPastedCells(0,0, MatrixClipboardCodec.Read("99"));
                original.Dispose();
                TestAssert.That(changed.Matrix.GetValue(4,4999) == 2499 && changed.GetCellText(0,0) == "99",
                    "Disposing the original must retain blocks referenced by a snapshot.");
                Parallel.For(0, 100, i =>
                {
                    long value = changed.Matrix.GetValue(i % 5, 4999);
                    TestAssert.That(value == 2499, "Parallel cache reads retain exact values.");
                });
                for (int r = 0; r < 5; r++) for (int c = 0; c < 5000; c += 500) changed.Matrix.GetValue(r,c);
                TestAssert.That(changed.Matrix.CachedBytes <= 65536, "All generations share one bounded cache.");
                long bytes = changed.Matrix.StoredBytes;
                using var cancel = new CancellationTokenSource();
                TestAssert.Throws<OperationCanceledException>(() => changed.TryWithPastedCells(0,0,1,1,MatrixClipboardCodec.Read("8"), cancel.Token,
                    required => { cancel.Cancel(); return new(required,long.MaxValue); }), "Cancellation after preflight releases unpublished resources.");
                TestAssert.That(changed.Matrix.StoredBytes == bytes && changed.GetCellText(0,0) == "99", "Cancelled paste preserves source file and values.");
                changed.Dispose();
            }
            TestAssert.That(!Directory.EnumerateFiles(directory).Any(), "Last-owner disposal removes all input files.");
            // Replacing the only block must not retain files merely because they were ancestors.
            var edit = new MatrixDraft(1, 100, directory);
            for (int generation = 1; generation <= 12; generation++)
            {
                var next = edit.WithPastedCells(0, 0, MatrixClipboardCodec.Read(generation.ToString()));
                edit.Dispose(); edit = next;
                TestAssert.That(Directory.EnumerateFiles(directory).Count() == 1,
                    "Replacing the sole block must release each obsolete generation file immediately.");
            }
            long stored = edit.Matrix.StoredBytes;
            long before = GC.GetAllocatedBytesForCurrentThread();
            edit.TrySetCell(0,0,"12");
            long allocated = GC.GetAllocatedBytesForCurrentThread()-before;
            TestAssert.That(edit.Matrix.StoredBytes == stored && allocated < 8192,
                "Committing an unchanged cell must not write a new block.");
            edit.Dispose();
            using (var abandoned = new MatrixInputBuilder(4096, temporaryDirectory: directory))
                abandoned.AddRow(Enumerable.Range(0,4096).Select(i => (long)i).ToArray());
            TestAssert.That(!Directory.EnumerateFiles(directory).Any(), "Abandoned builders remove their partial files.");
            using var empty = new MatrixDraft(8192, 8192, directory);
            using var cancellation = new CancellationTokenSource();
            using var watcherReady = new ManualResetEventSlim();
            bool observedDeltaFile = false;
            var watcher = new Thread(() =>
            {
                watcherReady.Set();
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (!Directory.EnumerateFiles(directory).Any() && timeout.Elapsed < TimeSpan.FromSeconds(10)) Thread.Yield();
                observedDeltaFile = Directory.EnumerateFiles(directory).Any();
                cancellation.Cancel();
            });
            watcher.Start(); watcherReady.Wait();
            try
            {
                TestAssert.Throws<OperationCanceledException>(() => empty.WithPastedCells(0,0,8192,1,
                    MatrixClipboardCodec.Read("1"), cancellation.Token), "A running multi-block paste must observe cancellation.");
            }
            finally { watcher.Join(); }
            TestAssert.That(observedDeltaFile && !Directory.EnumerateFiles(directory).Any()
                && empty.Matrix.StoredBytes == 0 && empty.MissingCount == 67108864,
                "Cancellation after a real delta-file write must remove the unpublished file and preserve the source.");
        }
        finally
        {
            // Dispose is responsible for files; this removes only the test's now-empty directory.
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
    }
}
