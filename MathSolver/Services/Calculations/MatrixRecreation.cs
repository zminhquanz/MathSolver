namespace MathSolver.Services.Core;

/// <summary>Owns newly allocated drafts until publication or disposal.</summary>
internal sealed class MatrixCreationAttempt : IDisposable
{
    internal MatrixDraft? A { get; private set; }
    internal MatrixDraft? B { get; private set; }
    internal MatrixMemoryCheck Memory { get; }
    internal bool IsCanceled { get; }

    internal MatrixCreationAttempt(MatrixDraft? a = null, MatrixDraft? b = null,
        MatrixMemoryCheck memory = default, bool canceled = false)
    { A = a; B = b; Memory = memory; IsCanceled = canceled; }

    public void Dispose()
    {
        var a = A; var b = B;
        A = null; B = null;
        try { a?.Dispose(); }
        finally { b?.Dispose(); }
    }

    internal bool TryPublish(ref MatrixDraft? a, ref MatrixDraft? b,
        int aRows, int aColumns, int bRows, int bColumns)
    {
        if (IsCanceled || A is not { } nextA || B is not { } nextB
            || nextA.Rows != aRows || nextA.Columns != aColumns
            || nextB.Rows != bRows || nextB.Columns != bColumns) return false;
        a = nextA; b = nextB;
        A = null; B = null;
        return true;
    }
}

/// <summary>Destructive recreation: release the previous session before allocating another.</summary>
internal static class MatrixRecreation
{
    private const long CollectionThreshold = 16L * 1024 * 1024;

    // The UI must also detach viewport readers before calling TryCreate on its worker.
    internal static long Release(ref MatrixDraft? a, ref MatrixDraft? b, ref MatrixCalculation? result)
    {
        long bytes = (a?.ResidentBytes ?? 0) + (b?.ResidentBytes ?? 0)
            + (result is null ? 0 : result.IsFileBacked ? result.CachedBytes : result.StoredBytes);
        var previousA = a; var previousB = b; var previous = result;
        a = null;
        b = null;
        result = null;
        try { previousA?.Dispose(); }
        finally
        {
            try { previousB?.Dispose(); }
            finally { previous?.Dispose(); }
        }
        return bytes;
    }

    internal static MatrixCreationAttempt TryCreate(int aRows, int aColumns, int bRows, int bColumns,
        long releasedBytes, CancellationToken token = default, IProgress<double>? progress = null,
        string? temporaryDirectory = null)
    {
        ValidateDimension(aRows, nameof(aRows)); ValidateDimension(aColumns, nameof(aColumns));
        ValidateDimension(bRows, nameof(bRows)); ValidateDimension(bColumns, nameof(bColumns));
        ArgumentOutOfRangeException.ThrowIfNegative(releasedBytes);
        // Run on the worker, after the UI drops all old readers. Return unused
        // committed segments as well: an ordinary full GC can retain multi-GB
        // buffers' backing memory and make the fresh OS commit check refuse a retry.
        long required = MatrixDraft.EstimateResidentBytes(aRows, aColumns)
            + MatrixDraft.EstimateResidentBytes(bRows, bColumns);
        // Retrying after a canceled/refused creation has no live input models,
        // but unpublished large buffers may still await collection.
        if (releasedBytes >= CollectionThreshold || required >= CollectionThreshold)
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        if (token.IsCancellationRequested) return new(canceled: true);
        var memory = MatrixMemoryGuard.CheckAvailable(required);
        if (!memory.CanAllocate) return new(memory: memory);
        MatrixDraft? a = null, b = null;
        try
        {
            a = new MatrixDraft(aRows, aColumns, temporaryDirectory: temporaryDirectory);
            progress?.Report(.5);
            if (token.IsCancellationRequested) return new(canceled: true);
            b = new MatrixDraft(bRows, bColumns, temporaryDirectory: temporaryDirectory);
            progress?.Report(1);
            if (token.IsCancellationRequested) return new(canceled: true);
            var attempt = new MatrixCreationAttempt(a, b, memory);
            a = null; b = null;
            return attempt;
        }
        finally
        {
            try { a?.Dispose(); }
            finally { b?.Dispose(); }
        }
    }

    private static void ValidateDimension(int value, string name)
    {
        if (value is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(name);
    }
}
