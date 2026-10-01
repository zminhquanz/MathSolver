using MathSolver.Numerics;
using MathSolver.Services;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckRecoverableResultStorage() => CheckMemoryAsync().GetAwaiter().GetResult();

    private static async Task CheckMemoryAsync()
    {
        await CheckPowerResultLifetimeAsync();
        string directory = Path.Combine(Path.GetTempPath(), $"mathsolver-memory-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var files = new ResultFileStore(directory);
        try
        {
            foreach (int count in new[] { 0, 1, 4095, 4096, 4097, 8200 })
            foreach (bool negative in new[] { false, true })
            {
                int blocks = 0;
                string path = await files.CreateAsync((pending, token) =>
                {
                    using var writer = new StreamWriter(pending);
                    PowerOfTenDecimalWriter.Write(writer, count, negative, 4096, () => blocks++, token);
                });
                string expected = ((negative ? -BigInteger.One : BigInteger.One) * BigInteger.Pow(10, count))
                    .ToString(CultureInfo.InvariantCulture);
                Require(await File.ReadAllTextAsync(path) == expected,
                    "An archived power-of-ten result was changed or truncated.");
                Require(blocks == (count + 1 + 4095) / 4096, "Streaming progress lost a block boundary.");
                for (int save = 0; save < 2; save++)
                {
                    string destination = Path.Combine(directory, $"export-{save}.txt");
                    await using (var source = File.OpenRead(path))
                    await using (var target = File.Create(destination))
                        await source.CopyToAsync(target, 4096);
                    Require(await File.ReadAllTextAsync(destination) == expected && File.Exists(path),
                        "Exporting destroyed the only retained exact result.");
                    File.Delete(destination);
                }
                files.Delete(path);
                Require(!File.Exists(path), "Clearing a result did not delete its backing file.");
            }

            string retained = await files.CreateAsync((path, _) => File.WriteAllText(path, "123456789"));
            bool failed = false;
            try
            {
                await files.CreateAsync((path, _) =>
                {
                    File.WriteAllText(path, "incomplete");
                    throw new IOException("Simulated disk-full write.");
                });
            }
            catch (IOException) { failed = true; }
            Require(failed && Directory.GetFiles(directory).Length == 1 &&
                    await File.ReadAllTextAsync(retained) == "123456789",
                "A failed write damaged the retained result or left a partial archive.");

            using (var cancellation = new CancellationTokenSource())
            {
                bool canceled = false;
                try
                {
                    await files.CreateAsync((path, token) =>
                    {
                        using var writer = new StreamWriter(path);
                        PowerOfTenDecimalWriter.Write(writer, 8200, false, 4096,
                            () => cancellation.Cancel(), token);
                    }, cancellation.Token);
                }
                catch (OperationCanceledException) { canceled = true; }
                Require(canceled && Directory.GetFiles(directory).Length == 1,
                    "Cancellation published an incomplete result or left a pending file.");
            }

            string unrelated = Path.Combine(directory, "user-notes.txt");
            File.WriteAllText(unrelated, "keep");
            bool rejected = false;
            try { files.Delete(unrelated); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected && File.Exists(unrelated), "Result cleanup deleted an unrelated file.");
            string stale = await files.CreateAsync((path, _) => File.WriteAllText(path, "stale"));
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow - TimeSpan.FromDays(8));
            files.DeleteAbandonedFiles(DateTime.UtcNow - TimeSpan.FromDays(7));
            Require(!File.Exists(stale) && File.Exists(retained) && File.Exists(unrelated),
                "Abandoned-file cleanup removed a recent result or unrelated data.");
            files.Delete(retained);
            File.Delete(unrelated);

            var registry = new RecoverableMemoryRegistry();
            var busy = new TestMemoryOwner { Busy = true };
            var healthy = new TestMemoryOwner();
            var failing = new ThrowingMemoryOwner();
            registry.Register(failing);
            registry.Register(busy);
            registry.Register(healthy);
            registry.Register(healthy);
            Require(await registry.ReleaseAsync() == 100 && healthy.Calls == 1 && busy.Calls == 1 && failing.Calls == 1,
                "Memory cleanup failed to skip a busy owner, isolate a failure, or deduplicate registration.");
            busy.Busy = false;
            await Task.WhenAll(registry.ReleaseAsync(), registry.ReleaseAsync());
            Require(healthy.MaxConcurrentCalls == 1 && busy.MaxConcurrentCalls == 1,
                "Repeated lifecycle callbacks overlapped cleanup of the same owner.");
            registry.ReleaseTemporaryResources();
            Require(healthy.TemporaryCleanupCalls == 1 && busy.TemporaryCleanupCalls == 1,
                "Closing a session did not clean up each registered owner exactly once.");
            var weak = RegisterTemporaryOwner(registry);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Require(!weak.TryGetTarget(out _), "The memory policy kept an otherwise unused page alive.");
            GC.KeepAlive(failing);
        }
        finally
        {
            // Only files created by this test under its unique directory are removed.
            foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<TestMemoryOwner> RegisterTemporaryOwner(RecoverableMemoryRegistry registry)
    {
        var owner = new TestMemoryOwner();
        registry.Register(owner);
        return new(owner);
    }

    private static async Task CheckPowerResultLifetimeAsync()
    {
        var view = new MathSolver.Views.PowerRootView();
        BigInteger value = BigInteger.One << 1024;
        string expected = value.ToString(CultureInfo.InvariantCulture);
        try
        {
            AppMemoryPolicy.IsInBackground = false;
            view.SetResult(value);
            view.SetTabActive(true);
            Require(await view.ReleaseRecoverableMemoryAsync() == 0 && view.ResidentValue == value,
                "A visible foreground result was needlessly evicted.");
            view.SetTabActive(false);
            CancellationToken idle = view.IdleToken;
            view.SetTabActive(true);
            Require(idle.IsCancellationRequested && view.StoredPath is null,
                "Returning to the tab did not cancel its delayed archive.");

            AppMemoryPolicy.IsInBackground = true;
            view.SetBusy(true, false);
            Require(await view.ReleaseRecoverableMemoryAsync() == 0, "A running calculation was evicted.");
            view.SetBusy(false, true);
            Require(await view.ReleaseRecoverableMemoryAsync() == 0, "A running export was evicted.");
            view.SetBusy(false, false);
            Require(await view.ReleaseRecoverableMemoryAsync() >= 64 && view.ResidentValue.IsZero &&
                    view.Preview == "Retained preview" && await File.ReadAllTextAsync(view.StoredPath!) == expected,
                "Archiving failed to release the resident number or preserve preview/exact output.");
            Require(await view.ReleaseRecoverableMemoryAsync() == 0,
                "Repeated trim rewrote an already archived result.");
            string path = view.StoredPath!;
            if (OperatingSystem.IsWindows())
            {
                new ResultFileStore(Path.GetDirectoryName(path)!).Delete(path);
                Require(File.Exists(path), "Another store deleted this live session's exact result.");
            }
            view.ClearResult();
            Require(!File.Exists(path), "Clear left a result archive behind.");

            view.SetResult(value, _ => throw new IOException("Simulated disk-full failure."));
            bool failed = false;
            try { await view.ReleaseRecoverableMemoryAsync(); }
            catch (IOException) { failed = true; }
            Require(failed && view.ResidentValue == value && view.StoredPath is null,
                "A failed archive discarded the only exact result in RAM.");

            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var finish = new ManualResetEventSlim();
            view.SetResult(value, _ => { started.SetResult(); finish.Wait(); });
            Task<long> oldArchive = view.ReleaseRecoverableMemoryAsync();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            BigInteger replacement = value + 123;
            view.SetResult(replacement);
            finish.Set();
            try { await oldArchive; }
            catch (OperationCanceledException) { }
            Require(view.ResidentValue == replacement && view.StoredPath is null,
                "A stale completed archive overwrote a newer calculation.");

            long[] releases = await Task.WhenAll(view.ReleaseRecoverableMemoryAsync(), view.ReleaseRecoverableMemoryAsync());
            Require(releases.Count(bytes => bytes > 0) == 1 &&
                    await File.ReadAllTextAsync(view.StoredPath!) == replacement.ToString(CultureInfo.InvariantCulture),
                "Concurrent lifecycle callbacks archived or released the same result twice.");
            path = view.StoredPath!;
            view.ReleaseTemporaryResources();
            Require(!File.Exists(path) && view.StoredPath is null,
                "Closing normally left the retained result on disk.");
        }
        finally
        {
            view.ClearResult();
            AppMemoryPolicy.IsInBackground = false;
            string root = MathSolver.Views.FileSystem.AppDataDirectory;
            string results = Path.Combine(root, "temporary-power-results");
            if (Directory.Exists(results))
            {
                foreach (string path in Directory.GetFiles(results)) File.Delete(path);
                Directory.Delete(results);
            }
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }

    private sealed class TestMemoryOwner : IRecoverableMemoryOwner, ITemporaryResourceOwner
    {
        public bool Busy;
        public int Calls;
        public int MaxConcurrentCalls;
        public int TemporaryCleanupCalls;
        public void ReleaseTemporaryResources() => TemporaryCleanupCalls++;
        private int _activeCalls;
        public async Task<long> ReleaseRecoverableMemoryAsync()
        {
            Calls++;
            int active = Interlocked.Increment(ref _activeCalls);
            MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, active);
            await Task.Yield();
            Interlocked.Decrement(ref _activeCalls);
            return Busy ? 0 : 100;
        }
    }

    private sealed class ThrowingMemoryOwner : IRecoverableMemoryOwner
    {
        public int Calls;
        public Task<long> ReleaseRecoverableMemoryAsync()
        {
            Calls++;
            throw new IOException("Simulated unavailable storage.");
        }
    }
}
