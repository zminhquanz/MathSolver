using System.Diagnostics;

namespace MathSolver.Services;

public static class AppMemoryPolicy
{
    public const long LargeResultThresholdBytes = 32L * 1024 * 1024;
    public static readonly TimeSpan InactiveResultDelay = TimeSpan.FromSeconds(60);
    private static readonly RecoverableMemoryRegistry Registry = new();
    private static int _cleanupQueued;
    private static int _cleanupPending;
    public static bool IsInBackground { get; private set; }

    public static void Register(IRecoverableMemoryOwner owner) => Registry.Register(owner);
    public static void EndSession() => Registry.ReleaseTemporaryResources();

    public static void EnterBackground()
    {
        IsInBackground = true;
        LiveWallpaperManager.NotifyHostSuspended();
        RequestCleanup();
    }

    public static void Resume()
    {
        IsInBackground = false;
        LiveWallpaperManager.NotifyHostResumed();
    }

    public static void RequestCleanup()
    {
        Interlocked.Exchange(ref _cleanupPending, 1);
        if (Interlocked.Exchange(ref _cleanupQueued, 1) != 0) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                do
                {
                    Interlocked.Exchange(ref _cleanupPending, 0);
                    await CollectReleasedMemoryAsync(await Registry.ReleaseAsync());
                    // A calculation or inference can finish while another owner
                    // is archiving. Its request must run after this cycle.
                } while (Volatile.Read(ref _cleanupPending) != 0);
            }
            catch (Exception error) { Debug.WriteLine($"Memory cleanup postponed: {error.Message}"); }
            finally
            {
                Interlocked.Exchange(ref _cleanupQueued, 0);
                if (Volatile.Read(ref _cleanupPending) != 0) RequestCleanup();
            }
        });
    }

    public static Task CollectReleasedMemoryAsync(long releasedBytes) =>
        releasedBytes >= LargeResultThresholdBytes
            ? Task.Run(() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized,
                blocking: false, compacting: false))
            : Task.CompletedTask;
}
