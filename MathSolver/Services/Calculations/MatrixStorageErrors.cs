namespace MathSolver.Services.Core;

internal static class MatrixStorageErrors
{
    internal static bool IsFailure(Exception error)
    {
        if (error is IOException or UnauthorizedAccessException or ObjectDisposedException) return true;
        if (error is not AggregateException aggregate || aggregate.InnerExceptions.Count == 0) return false;
        foreach (var inner in aggregate.InnerExceptions)
            if (!IsFailure(inner)) return false;
        return true;
    }
}
