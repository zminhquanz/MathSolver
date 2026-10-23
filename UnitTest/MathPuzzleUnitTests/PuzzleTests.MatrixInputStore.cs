using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixInputStoreRoundTrip()
    {
        using var workspace = TestWorkspace.Temporary("input-store");
        using var store = new MatrixInputStore(3 * 65536 + 3, workspace.DirectoryPath, 1);
        store.Set(0, long.MinValue);
        store.Set(1, 0);
        store.Set(65535, long.MaxValue);
        store.Set(65536, -17);
        store.Set(131072, 29);
        store.Set(196610, 41);
        store.Flush();
        TestAssert.That(store.Read(0) == long.MinValue && store.Read(65535) == long.MaxValue
            && store.Read(65536) == -17 && store.Read(131072) == 29 && store.Read(196610) == 41,
            "Evicted input blocks must round trip exact Int64 values and partial final blocks.");
        TestAssert.That(store.IsFilled(1) && !store.IsFilled(2) && store.Read(2) == 0
            && store.MissingCount == store.Length - 6,
            "Explicit zero and blank must remain distinct after eviction.");
        var boundary = new long[4];
        store.CopyValues(65534, boundary);
        TestAssert.That(boundary.SequenceEqual(new long[] { 0, long.MaxValue, -17, 0 }),
            "CopyValues must cross block boundaries and expose blank values as zero.");
        Parallel.For(0, 100, i => TestAssert.That(store.Read(i % 2 == 0 ? 0 : 196610)
            == (i % 2 == 0 ? long.MinValue : 41), "Parallel positional reads must preserve values."));
        TestAssert.That(store.CachedBytes <= store.CacheLimitBytes && store.CachedBytes <= 532480
            && store.ResidentBytes <= MatrixInputStore.EstimateResidentBytes(store.Length, 1)
            && store.StoredBytes == new FileInfo(store.FilePath).Length,
            "The normalized one-block cache and resident estimate must include values, mask and metadata.");
        string path = store.FilePath;
        store.Dispose();
        TestAssert.That(!File.Exists(path), "Disposal must delete the owned temporary file.");
        TestAssert.Throws<ObjectDisposedException>(() => store.Read(0), "Disposed input must reject reads.");
    }

    public static void CheckMatrixInputStoreFillAndClone()
    {
        using var workspace = TestWorkspace.Temporary("input-clone");
        using var source = new MatrixInputStore(2 * 65536 + 3, workspace.DirectoryPath, 1);
        source.Fill(0, source.Length, 9);
        TestAssert.That(source.CachedBytes == 0 && source.StoredBytes == 0 && source.MissingCount == 0,
            "Uniform fill must use descriptors without expanding or writing all cells.");
        source.Fill(65535, 3, null);
        source.Set(131074, long.MaxValue);
        using var clone = source.Clone(CancellationToken.None);
        TestAssert.That(clone.MissingCount == 3 && clone.Read(0) == 9 && !clone.IsFilled(65536)
            && clone.Read(131074) == long.MaxValue, "Clone must preserve constant and mixed blocks and missing cells.");
        clone.FillMissingWithZero();
        clone.Set(0, -2);
        clone.Fill(65535, 2, 7);
        TestAssert.That(clone.MissingCount == 0 && clone.IsFilled(65537) && clone.Read(65537) == 0
            && source.Read(0) == 9 && !source.IsFilled(65535) && source.MissingCount == 3,
            "Missing fill and clone edits must preserve explicit source data and isolate ownership.");
        source.Fill(65535, 3, 0);
        source.Fill(0, source.Length, null);
        TestAssert.That(source.MissingCount == source.Length && source.CachedBytes == 0 && source.Read(131074) == 0,
            "Whole-block clear must discard cached values and restore blank descriptors.");
        int files = Directory.GetFiles(workspace.DirectoryPath).Length;
        TestAssert.Throws<OperationCanceledException>(() => source.Clone(new CancellationToken(true)),
            "Canceled clones must fail without creating a leaked file.");
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == files,
            "Canceled clone must dispose any incomplete target.");
    }

    public static void CheckMatrixInputStoreStorageFailure()
    {
        using var workspace = TestWorkspace.Temporary("input-failure");
        using var store = new MatrixInputStore(2 * 65536, workspace.DirectoryPath, 1);
        store.Set(0, 123);
        store.Set(65536, 456); // First block has been written and evicted.
        store.Flush();
        using (var corruptor = new FileStream(store.FilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            corruptor.SetLength(3);
        TestAssert.Throws<EndOfStreamException>(() => store.Read(0),
            "A truncated on-disk block must fail instead of silently substituting zeros.");
        TestAssert.Throws<EndOfStreamException>(() => store.Read(0),
            "A failed read must never install a partially read cache entry.");
        TestAssert.That(store.MissingCount == store.Length - 2,
            "A failed block read must leave filled accounting intact.");
        int files = Directory.GetFiles(workspace.DirectoryPath).Length;
        TestAssert.Throws<EndOfStreamException>(() => store.Clone(CancellationToken.None),
            "A clone must propagate source storage failures.");
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == files && store.MissingCount == store.Length - 2,
            "A failed clone must delete its partial target and preserve the source filled count.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => store.Fill(-1, 1, 0), "Negative ranges must be rejected.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => store.Set(store.Length, 0), "Out-of-bounds writes must be rejected.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new MatrixInputStore(1, cacheLimitBytes: -1),
            "Negative cache limits must be rejected before opening a file.");
        TestAssert.Throws<DirectoryNotFoundException>(() => new MatrixInputStore(1, Path.Combine(workspace.DirectoryPath, "missing")),
            "Unavailable temporary directories must propagate file creation failures.");
    }

    public static void CheckMatrixInputStoreBulkAndMaximum()
    {
        using var workspace = TestWorkspace.Temporary("input-bulk");
        using var store = new MatrixInputStore(2 * 65536 + 1, workspace.DirectoryPath, 1);
        store.Set(65534, 4);
        store.WriteValues(65534, new long[] { long.MinValue, 0, long.MaxValue, -8 });
        var actual = new long[4];
        store.CopyValues(65534, actual);
        TestAssert.That(actual.SequenceEqual(new long[] { long.MinValue, 0, long.MaxValue, -8 })
            && store.MissingCount == store.Length - 4 && store.IsFilled(65535),
            "Bulk writes must cross blocks, preserve exact values, and mark zero filled exactly once.");
        store.WriteValues(0, new long[65536]);
        TestAssert.That(store.MissingCount == 65535 && store.Read(65534) == 0 && store.IsFilled(0),
            "A whole constant bulk block must replace old values and update missing accounting.");
        using var maximum = new MatrixInputStore(16384 * 16384, workspace.DirectoryPath);
        maximum.Fill(0, maximum.Length, long.MaxValue);
        TestAssert.That(maximum.Read(maximum.Length - 1) == long.MaxValue && maximum.MissingCount == 0
            && maximum.CachedBytes == 0 && maximum.StoredBytes == 0 && maximum.ResidentBytes < 1024 * 1024,
            "Maximum-size uniform inputs must stay descriptor-sized without scanning or expanding every cell.");
        using var clone = maximum.Clone(CancellationToken.None);
        clone.Fill(0, clone.Length, null);
        clone.FillMissingWithZero();
        TestAssert.That(clone.MissingCount == 0 && clone.Read(clone.Length - 1) == 0 && maximum.Read(0) == long.MaxValue
            && clone.CachedBytes == 0, "Maximum uniform clone, clear and missing fill must stay bounded and isolated.");
    }

    public static void CheckMatrixInputStoreFailedEviction()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows byte-range locks reject writes through the store's other handle.
        using var workspace = TestWorkspace.Temporary("input-write-failure");
        using var store = new MatrixInputStore(2 * 65536, workspace.DirectoryPath, 1);
        store.Set(0, long.MaxValue);
        using var blocker = new FileStream(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        blocker.Lock(524288, 8192); // Permit value write, reject bitmap write to exercise a partial dirty flush.
        try
        {
            TestAssert.Throws<IOException>(() => store.Set(65536, -9),
                "A failed dirty eviction must surface its storage error.");
            TestAssert.That(store.Read(0) == long.MaxValue && store.IsFilled(0)
                && store.MissingCount == store.Length - 1 && store.CachedBytes <= store.CacheLimitBytes,
                "Failed dirty eviction must retain the only complete current block and reject the new edit.");
            TestAssert.That(store.StoredBytes == new FileInfo(store.FilePath).Length,
                "Stored byte accounting must include the bytes written before an I/O failure.");
        }
        finally { blocker.Unlock(524288, 8192); }
        store.Set(65536, -9);
        TestAssert.That(store.Read(0) == long.MaxValue && store.Read(65536) == -9,
            "Dirty eviction must recover without data loss after the storage failure clears.");
    }

    public static void CheckMatrixInputStoreReusesBuffers()
    {
        using var workspace = TestWorkspace.Temporary("input-buffer-reuse");
        using var store = new MatrixInputStore(3 * 65536, workspace.DirectoryPath, 1);
        store.Set(0, -10);
        store.Set(65536, 20);
        store.Set(131072, 30);
        store.Flush();
        for (int i = 0; i < 6; i++) _ = store.Read(i % 3 * 65536);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (int i = 0; i < 60; i++) sum += store.Read(i % 3 * 65536);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestAssert.That(sum == 800, "Reused disk cache buffers must preserve every stored block's exact values.");
        TestAssert.That(allocated < 2 * 1024 * 1024,
            "A warmed one-block cache must reuse large buffers while cycling through stored blocks.");

        store.Fill(0, 65536, null);
        store.Set(1, 0);
        TestAssert.That(store.Read(0) == 0 && store.Read(2) == 0 && !store.IsFilled(0)
            && store.IsFilled(1) && !store.IsFilled(2) && store.MissingCount == store.Length - 3,
            "Loading a blank descriptor into reused buffers must clear previous values and filled bits.");
        store.Fill(65536, 65536, 0);
        store.Set(65537, 9);
        TestAssert.That(store.Read(65536) == 0 && store.Read(65538) == 0 && store.IsFilled(65538),
            "Loading a filled zero descriptor into reused buffers must clear values and fill all bits.");
        store.Fill(131072, 65536, -7);
        store.Set(131073, null);
        TestAssert.That(store.Read(131072) == -7 && store.Read(131074) == -7 && store.Read(131073) == 0
            && store.IsFilled(131074) && !store.IsFilled(131073),
            "Loading a nonzero uniform descriptor into reused buffers must replace values and filled bits.");
    }

    public static void CheckMatrixInputStoreCloneResponsiveness()
    {
        using var workspace = TestWorkspace.Temporary("input-clone-responsive");
        using var source = new MatrixInputStore(3 * 65536, workspace.DirectoryPath, 1);
        source.Fill(0, source.Length, 9);
        source.Set(5, null);
        source.Set(65536, 9);
        source.Set(131072, 9);
        var reports = new List<double>();
        using (var clone = source.Clone(CancellationToken.None, new InputStoreProgress(value =>
        {
            reports.Add(value);
            if (reports.Count != 1) return;
            Task<long> uiRead = Task.Run(() => source.Read(0) + source.Read(131072));
            TestAssert.That(uiRead.Wait(TimeSpan.FromSeconds(2)) && uiRead.Result == 18,
                "A source read on another thread must finish while clone progress is reporting between blocks.");
        })))
            TestAssert.That(clone.Read(0) == 9 && !clone.IsFilled(5) && clone.MissingCount == 1
                && reports.Count == 3 && reports[^1] == 1,
                "Block-wise clone progress must preserve a complete coherent snapshot.");

        Action<MatrixInputStore>[] mutations =
        [
            store => store.Set(0, 23),
            store => store.Fill(65536, 65536, 31),
            store => store.Fill(1, 3, null),
            store => store.WriteValues(10, new long[] { 11, 12, 13 }),
            store => store.FillMissingWithZero()
        ];
        foreach (Action<MatrixInputStore> mutation in mutations)
        {
            source.Fill(0, source.Length, 9);
            source.Set(5, null);
            int files = Directory.GetFiles(workspace.DirectoryPath).Length;
            bool changed = false;
            TestAssert.Throws<InvalidOperationException>(() => source.Clone(CancellationToken.None,
                new InputStoreProgress(_ => { if (!changed) { changed = true; mutation(source); } })),
                "Clone must reject a concurrent logical source edit instead of returning a mixed snapshot.");
            TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == files && source.Read(65535) == 9,
                "Rejected concurrent-edit clones must delete their partial targets and leave the source usable.");
        }
        TestAssert.Throws<InvalidOperationException>(() => source.Clone(CancellationToken.None,
            new InputStoreProgress(value => { if (value == 1) source.Set(0, 37); })),
            "Clone must validate the source version after its final progress callback.");

        using var cancellation = new CancellationTokenSource();
        TestAssert.Throws<OperationCanceledException>(() => source.Clone(cancellation.Token,
            new InputStoreProgress(_ => cancellation.Cancel())),
            "Cancellation during block progress must stop cloning and dispose its target.");
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == 1 && source.Read(0) == 37,
            "Canceled clones must preserve the source and remove only their target.");
        TestAssert.Throws<ObjectDisposedException>(() => source.Clone(CancellationToken.None,
            new InputStoreProgress(_ => source.Dispose())),
            "Closing the source between clone blocks must stop the clone safely.");
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == 0,
            "Closing during clone must release both source and incomplete target files.");
    }

    private sealed class InputStoreProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
