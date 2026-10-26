namespace MathSolver.Services;

internal readonly record struct MatrixClipboardWriteResult(bool Copied, bool Persisted);

/// <summary>Bounded clipboard contention retries, separate from optional persistence.</summary>
internal static class MatrixClipboardWriter
{
    internal static async Task<MatrixClipboardWriteResult> TryWriteAsync(
        Func<bool> trySet, Func<bool> tryFlush, Func<bool> canPublish, Func<Task> retryDelay)
    {
        const int attempts = 3;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (!canPublish()) return default;
            if (trySet())
            {
                // Failure to persist after app exit does not undo a successful live
                // copy. Never republish it: another app may already own the clipboard.
                return new(true, tryFlush());
            }
            if (attempt + 1 < attempts) await retryDelay();
        }
        return default;
    }
}
