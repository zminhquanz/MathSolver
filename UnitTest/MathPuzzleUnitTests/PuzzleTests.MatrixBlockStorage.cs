using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixBlockStorage()
    {
        // Wrong signed width, padding, or tile addressing loses one of these boundaries.
        BigInteger[][] boundaries =
        [
            [long.MinValue, long.MaxValue],
            [-(BigInteger.One << 127), (BigInteger.One << 127) - 1],
            [-(BigInteger.One << 191), (BigInteger.One << 191) - 1],
            [-(BigInteger.One << 521), (BigInteger.One << 521) + 17]
        ];
        foreach (bool disk in new[] { false, true })
        {
            using var store = new MatrixBlockStore(17, 65, 16, disk, null, 4096);
            // Reverse completion order and concurrent writes must preserve logical order.
            Parallel.For(0, 10, reverse =>
            {
                int tile = 9 - reverse, br = tile / 5, bc = tile % 5;
                int h = br == 0 ? 16 : 1, w = bc == 4 ? 1 : 16;
                if (br == 1 || bc == 4)
                    store.WriteConstantBlock(br, bc, h, w, -19 - tile);
                else
                {
                    var values = new BigInteger[h * w];
                    for (int i = 0; i < values.Length; i++) values[i] = boundaries[bc][i % 2];
                    store.WriteBlock(br, bc, h, w, values);
                }
            });
            store.Complete();
            TestAssert.That(store.StorageKinds.Order().SequenceEqual(Enum.GetValues<MatrixNumberStorage>()),
                "Every tile must use the smallest exact numeric storage kind.");
            TestAssert.That(store.StoredBytes == 256L * (8 + 16 + 24 + 66) + 6 * 8,
                "Nonuniform values must use their signed width and uniform edge tiles must store only one value.");
            var random = new Random(917);
            for (int i = 0; i < 2000; i++)
            {
                int r = random.Next(17), c = random.Next(65), bc = c / 16;
                BigInteger expected = r == 16 || bc == 4 ? -19 - (r / 16 * 5 + bc) : boundaries[bc][c % 2];
                TestAssert.That(store.GetValue(r, c) == expected, "Exact random reads must survive tile boundaries and cache eviction.");
                TestAssert.That(store.CachedBytes >= 0 && store.CachedBytes <= store.CacheLimitBytes,
                    "The encoded LRU must stay within its byte limit, including oversized uncached tiles.");
            }
            Parallel.For(0, 200, i => TestAssert.That(store.GetValue(i % 16, 16) == -(BigInteger.One << 127),
                "Concurrent completed reads must preserve the file cursor and cache."));
            TestAssert.That(store.IsFileBacked == disk && store.CacheLimitBytes == 4096,
                "Storage mode and cache reserve must describe actual configured storage.");
            if (disk)
                TestAssert.That(store.CachedBytes > 0 && new FileInfo(store.BackingFilePath!).Length == store.StoredBytes,
                    "Disk cache must retain eligible encoded tiles and stored byte accounting must match the file.");
        }

        // Non-power-of-two blocks must still validate and locate partial edge cells.
        using var odd = new MatrixBlockStore(18, 19, 17, false, null, 0);
        odd.WriteBlock(0, 0, 17, 17, Enumerable.Repeat(new BigInteger(-7), 289).ToArray());
        odd.WriteConstantBlock(0, 1, 17, 2, 11);
        odd.WriteConstantBlock(1, 0, 1, 17, -13);
        odd.WriteConstantBlock(1, 1, 1, 2, 23);
        odd.Complete();
        TestAssert.That(odd.StoredBytes == 32 && odd.GetValue(17, 18) == 23 && odd.GetValue(16, 16) == -7,
            "WriteBlock must detect uniform tiles and non-power-of-two edge addressing must work.");
    }

    public static void CheckMatrixBlockStorageLifetime()
    {
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new MatrixBlockStore(0, 1, 16, false, null, 0), "Zero rows must be rejected.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new MatrixBlockStore(1, 16385, 16, false, null, 0), "Oversized dimensions must be rejected.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new MatrixBlockStore(1, 1, 15, false, null, 0), "Invalid block sizes must be rejected.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new MatrixBlockStore(1, 1, 257, false, null, 0), "Oversized blocks must be rejected.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => new MatrixBlockStore(1, 1, 16, false, null, -1), "Negative cache reserves must be rejected.");

        string directory = Path.Combine(Path.GetTempPath(), "mathsolver-storage-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string unrelated = Path.Combine(directory, "preserve.txt");
        File.WriteAllText(unrelated, "owned by someone else");
        try
        {
            var store = new MatrixBlockStore(17, 17, 16, true, directory, 8);
            string path = store.BackingFilePath!;
            TestAssert.That(File.Exists(path), "A disk store must create its owned temporary file.");
            TestAssert.Throws<InvalidOperationException>(() => store.GetValue(0, 0), "An unpublished store must not expose partial reads.");
            TestAssert.Throws<InvalidOperationException>(() => store.Complete(), "Incomplete tiles must prevent publication.");
            TestAssert.Throws<ArgumentException>(() => store.WriteBlock(0, 0, 15, 16, new BigInteger[240]), "Wrong tile shape must be rejected.");
            TestAssert.Throws<ArgumentException>(() => store.WriteBlock(0, 0, 16, 16, new BigInteger[255]), "Wrong value count must be rejected.");
            TestAssert.Throws<ArgumentOutOfRangeException>(() => store.WriteConstantBlock(2, 0, 1, 16, 1), "Invalid tile indices must be rejected.");
            store.WriteConstantBlock(0, 0, 16, 16, long.MinValue);
            TestAssert.Throws<InvalidOperationException>(() => store.WriteConstantBlock(0, 0, 16, 16, 1), "Duplicate tile writes must be rejected.");
            store.WriteConstantBlock(1, 1, 1, 1, 3);
            store.WriteConstantBlock(1, 0, 1, 16, 2);
            store.WriteConstantBlock(0, 1, 16, 1, 1);
            store.Complete();
            TestAssert.Throws<InvalidOperationException>(() => store.WriteConstantBlock(0, 0, 16, 16, 4), "Completed stores must be immutable.");
            TestAssert.Throws<ArgumentOutOfRangeException>(() => store.GetValue(-1, 0), "Invalid rows must be rejected.");
            TestAssert.Throws<ArgumentOutOfRangeException>(() => store.GetValue(0, 17), "Invalid columns must be rejected.");
            TestAssert.That(store.GetValue(0, 0) == long.MinValue && store.GetValue(16, 16) == 3 && store.CachedBytes == 8,
                "A one-value encoded cache must evict previous uniform tiles.");
            store.Dispose();
            store.Dispose();
            TestAssert.That(!File.Exists(path) && File.Exists(unrelated), "Dispose must delete only the store's owned file and be idempotent.");
            TestAssert.Throws<ObjectDisposedException>(() => store.GetValue(0, 0), "Disposed stores must reject reads.");
            TestAssert.Throws<ObjectDisposedException>(() => store.Complete(), "Disposed stores must reject completion.");
            TestAssert.Throws<ObjectDisposedException>(() => store.WriteConstantBlock(0, 0, 16, 16, 5), "Disposed stores must reject writes.");
            string incompletePath;
            using (var unfinished = new MatrixBlockStore(1, 1, 16, true, directory, 0)) incompletePath = unfinished.BackingFilePath!;
            TestAssert.That(!File.Exists(incompletePath), "Abandoning an incomplete result must delete the temporary file.");
            using var zeroCache = new MatrixBlockStore(1, 1, 16, true, directory, 0);
            zeroCache.WriteConstantBlock(0, 0, 1, 1, -(BigInteger.One << 191));
            zeroCache.Complete();
            TestAssert.That(zeroCache.GetValue(0, 0) == -(BigInteger.One << 191) && zeroCache.CachedBytes == 0,
                "Zero-cache direct disk reads must preserve signed values without retaining payloads.");
        }
        finally
        {
            File.Delete(unrelated);
            Directory.Delete(directory);
        }
    }
}
