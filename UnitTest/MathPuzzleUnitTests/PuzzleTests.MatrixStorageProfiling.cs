using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixStorageProfiling()
    {
        // Lost parallel publications, incorrect signed widths, or inflated successful-write
        // counts fail these independently derived values and byte totals.
        foreach (bool disk in new[] { false, true })
        {
            using var store = new MatrixBlockStore(32, 32, 16, disk, null, 0);
            BigInteger[] boundaries = [long.MinValue, -(BigInteger.One << 127),
                -(BigInteger.One << 191), -(BigInteger.One << 521)];
            using var rendezvous = new CountdownEvent(4);
            using var release = new ManualResetEventSlim();
            var workers = Enumerable.Range(0, 4).Select(tile => Task.Factory.StartNew(() =>
            {
                var values = new BigInteger[256];
                for (int i = 0; i < values.Length; i++) values[i] = boundaries[tile] + i;
                store.WriteBlock(tile / 2, tile % 2, 16, 16, values, stage =>
                {
                    if (stage != MatrixCalculationStage.Encoding) return;
                    rendezvous.Signal();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Encoding workers did not release.");
                });
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            try
            {
                TestAssert.That(rendezvous.Wait(TimeSpan.FromSeconds(10)),
                    "Every worker must reach encoding without holding the shared store gate.");
            }
            finally { release.Set(); }
            TestAssert.That(Task.WaitAll(workers, TimeSpan.FromSeconds(10)), "Parallel store writes must finish.");
            store.Complete();
            TestAssert.That(store.StoredBytes == 256L * (8 + 16 + 24 + 66), "Parallel encodings must retain their minimum exact widths.");
            TestAssert.That(store.StorageKinds.Order().SequenceEqual(Enum.GetValues<MatrixNumberStorage>()), "Parallel tiles must retain all exact storage kinds.");
            for (int r = 0; r < 32; r++)
                for (int c = 0; c < 32; c++)
                {
                    int tile = r / 16 * 2 + c / 16;
                    TestAssert.That(store.GetValue(r, c) == boundaries[tile] + r % 16 * 16 + c % 16,
                        "Parallel store commits must retain exact values and logical tile positions.");
                }
            var stats = store.Statistics;
            TestAssert.That(stats.BlocksWritten == 4, "Profile counts must represent successful tile publications.");
            TestAssert.That(stats.EncodingTime > TimeSpan.Zero && stats.LockWaitTime >= TimeSpan.Zero && stats.WriteTime > TimeSpan.Zero,
                "Successful parallel writes must measure actual encoding and writing separately from gate waiting.");
            TestAssert.Throws<InvalidOperationException>(() => store.WriteConstantBlock(0, 0, 16, 16, 0), "Completed writes must fail before encoding.");
            TestAssert.That(store.Statistics.BlocksWritten == 4, "Rejected writes must not inflate publication counts.");
        }

        using var uniform = new MatrixBlockStore(1, 2, 16, false, null, 0);
        var stages = new List<MatrixCalculationStage>();
        uniform.WriteConstantBlock(0, 0, 1, 2, BigInteger.One << 200, stage =>
        {
            stages.Add(stage);
            // A separate observer must be able to acquire the gate during either callback.
            var observer = Task.Factory.StartNew(() => uniform.Statistics,
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            TestAssert.That(observer.Wait(TimeSpan.FromSeconds(10)), "Constant-write activity callbacks must run outside the store gate.");
        });
        uniform.Complete();
        TestAssert.That(stages.SequenceEqual(new[] { MatrixCalculationStage.Encoding, MatrixCalculationStage.Writing }),
            "Constant writes must report encoding before committing the payload.");
        TestAssert.That(uniform.StoredBytes == 26 && uniform.Statistics.BlocksWritten == 1 && uniform.GetValue(0, 1) == BigInteger.One << 200,
            "Constant writes must profile one compact exact publication.");
    }

    public static void CheckMatrixStoragePublicationRaces()
    {
        foreach (bool disk in new[] { false, true })
        {
            // Pausing a fully encoded tile must leave the gate available. A competing
            // publication wins, and revalidation must prevent the stale tile overwriting it.
            using var duplicate = new MatrixBlockStore(1, 1, 16, disk, null, 0);
            RunPausedMatrixStoreWrite(duplicate, () => duplicate.WriteConstantBlock(0, 0, 1, 1, 29), typeof(InvalidOperationException));
            duplicate.Complete();
            TestAssert.That(duplicate.GetValue(0, 0) == 29 && duplicate.Statistics.BlocksWritten == 1,
                "The winning duplicate write must survive stale-writer rejection.");

            using var completed = new MatrixBlockStore(1, 1, 16, disk, null, 0);
            RunPausedMatrixStoreWrite(completed, () =>
            {
                TestAssert.Throws<InvalidOperationException>(completed.Complete, "An in-flight encoding must not count as a published tile.");
                completed.WriteConstantBlock(0, 0, 1, 1, 31);
                completed.Complete();
            }, typeof(InvalidOperationException));
            TestAssert.That(completed.GetValue(0, 0) == 31 && completed.Statistics.BlocksWritten == 1,
                "Completion must remain immutable after a competing in-flight tile resumes.");

            using var disposed = new MatrixBlockStore(1, 1, 16, disk, null, 0);
            string? path = disposed.BackingFilePath;
            RunPausedMatrixStoreWrite(disposed, disposed.Dispose, typeof(ObjectDisposedException));
            TestAssert.That(disposed.Statistics.BlocksWritten == 0 && (path is null || !File.Exists(path)),
                "Disposal during encoding must reject publication and release the owned file.");
        }
    }

    private static void RunPausedMatrixStoreWrite(MatrixBlockStore store, Action competingAction, Type expectedException)
    {
        using var ready = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        Exception? failure = null;
        var worker = Task.Factory.StartNew(() =>
        {
            try
            {
                store.WriteBlock(0, 0, 1, 1, new BigInteger[] { 17 }, stage =>
                {
                    if (stage != MatrixCalculationStage.Writing) return;
                    ready.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Paused writer did not release.");
                });
            }
            catch (Exception ex) { failure = ex; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            TestAssert.That(ready.Wait(TimeSpan.FromSeconds(10)), "The encoded writer must reach the pre-commit activity callback.");
            competingAction();
        }
        finally { release.Set(); }
        TestAssert.That(worker.Wait(TimeSpan.FromSeconds(10)), "The paused writer must terminate after release.");
        TestAssert.That(failure?.GetType() == expectedException, "A resumed writer must revalidate disposal, completion, and duplicate publication.");
    }
}
