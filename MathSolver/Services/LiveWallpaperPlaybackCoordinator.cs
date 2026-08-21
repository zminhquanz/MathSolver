namespace MathSolver.Services;

/// <summary>
/// Coordinates optional live-wallpaper playback with short, latency-sensitive
/// work such as local LLM inference. A suspended wallpaper keeps its source
/// loaded for a fast resume, but the native player is paused so video decode,
/// composition and texture updates stop competing for compute/bandwidth.
/// </summary>
public static class LiveWallpaperPlaybackCoordinator
{
    private static int _suspensionCount;

    public static event EventHandler? PlaybackPolicyChanged;

    public static bool IsPlaybackSuspended =>
        Volatile.Read(ref _suspensionCount) > 0;

    public static IDisposable SuspendForHighPriorityWork()
    {
        int count = Interlocked.Increment(ref _suspensionCount);

        if (count == 1)
        {
            PlaybackPolicyChanged?.Invoke(null, EventArgs.Empty);
        }

        return new SuspensionToken();
    }

    private sealed class SuspensionToken : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            int count = Interlocked.Decrement(ref _suspensionCount);

            if (count <= 0)
            {
                // Defensive normalization in case a future caller is ever
                // changed incorrectly; the public token is otherwise balanced.
                Interlocked.Exchange(ref _suspensionCount, 0);
                PlaybackPolicyChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
