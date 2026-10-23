using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Runtime.CompilerServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixRecreationRelease()
    {
        MatrixDraft? a = new(16, 16), b = new(16, 16);
        a.TrySetCell(0, 0, "7"); b.TrySetCell(15, 15, "9");
        MatrixCalculation? result = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Multiply,
            executionOptions: new() { InMemoryResultLimitBytes = 0, ResultCacheBytes = 4096 });
        var oldResult = result;
        _ = result.GetValue(0, 0);
        long released = MatrixRecreation.Release(ref a, ref b, ref result);
        TestAssert.That(result is null && a is null && b is null,
            "Recreation must leave no input model, rather than expose a valid-looking 1x1 replacement.");
        TestAssert.That(released >= 16L * 16 * 18 && oldResult.CachedBytes == 0,
            "Recreation must release result cache and account for both old input arrays/masks.");
        TestAssert.Throws<ObjectDisposedException>(() => oldResult.GetValue(0, 0), "Old C must no longer be readable after reset.");
        using var fresh = MatrixRecreation.TryCreate(17, 19, 23, 29, released);
        TestAssert.That(fresh.A is { Rows: 17, Columns: 19, HasAnyValues: false }
            && fresh.B is { Rows: 23, Columns: 29, HasAnyValues: false }, "A successful replacement must have new dimensions and empty cells.");
        fresh.Dispose();
        TestAssert.That(fresh.A is null && fresh.B is null, "An unpublished or discarded creation must not retain either buffer.");
    }

    public static void CheckMatrixRecreationCollectionAndStop()
    {
        var discarded = ReleaseLargeMatrixSession();
        using var fresh = MatrixRecreation.TryCreate(3, 4, 4, 5, discarded.Bytes);
        TestAssert.That(discarded.Arrays.All(reference => !reference.IsAlive),
            "Old large matrix buffers must be collected before the next memory check and replacement allocation.");
        TestAssert.That(fresh.A is { Rows: 3, Columns: 4 } && fresh.B is { Rows: 4, Columns: 5 },
            "Collected old buffers must not prevent creating a fresh pair.");
        using var preStop = new CancellationTokenSource(); preStop.Cancel();
        using var preCanceled = MatrixRecreation.TryCreate(3, 4, 4, 5, 0, preStop.Token);
        TestAssert.That(preCanceled.IsCanceled && preCanceled.A is null && preCanceled.B is null,
            "Pre-canceled recreation must not allocate drafts or throw cancellation exceptions.");
        using var stop = new CancellationTokenSource();
        using var canceled = MatrixRecreation.TryCreate(16, 16, 16, 16, 0, stop.Token,
            new MatrixProgress(value => { if (value >= .5) stop.Cancel(); }));
        TestAssert.That(canceled.IsCanceled && canceled.A is null && canceled.B is null,
            "Stopping after allocating A must discard unpublished input buffers without throwing.");
    }

    public static void CheckMatrixDisposedDenseBuffers()
    {
        var a = new IntegerMatrix(16, 16); var b = new IntegerMatrix(16, 16);
        var result = MatrixEngine.Calculate(a, b, MatrixOperation.Add);
        TestAssert.That(result.WideValues is not null, "Fixture must exercise dense C memory.");
        result.Dispose(); result.Dispose();
        TestAssert.That(result.WideValues is null && result.BigValues is null,
            "Disposing C must drop large dense buffers even if an old async outcome still references the result object.");
    }

    public static void CheckMatrixCreationPublication()
    {
        MatrixDraft? a = null, b = null;
        using var created = MatrixRecreation.TryCreate(17, 19, 23, 29, 0);
        TestAssert.That(!created.TryPublish(ref a, ref b, 1, 1, 1, 1) && a is null && b is null,
            "A mismatched creation must never publish a fallback shape or overwrite the empty session.");
        TestAssert.That(created.TryPublish(ref a, ref b, 17, 19, 23, 29), "A matching result must transfer to the actual input owners.");
        created.Dispose();
        TestAssert.That(a is { Rows: 17, Columns: 19 } && b is { Rows: 23, Columns: 29 }
            && created.A is null && created.B is null, "Disposing the completed outcome must not reset the published input models.");
        TestAssert.That(!created.TryPublish(ref a, ref b, 17, 19, 23, 29)
            && a?.Rows == 17 && b?.Rows == 23, "An already consumed outcome must not change published dimensions.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference[] Arrays, long Bytes) ReleaseLargeMatrixSession()
    {
        MatrixDraft? a = new(1024, 1024, fileBacked: false), b = new(1024, 1024, fileBacked: false);
        MatrixCalculation? result = MatrixEngine.Calculate(a.Matrix, b.Matrix, MatrixOperation.Add);
        WeakReference[] arrays = [new(a.Matrix.Data), new(b.Matrix.Data), new(result.WideValues!)];
        long bytes = MatrixRecreation.Release(ref a, ref b, ref result);
        return (arrays, bytes);
    }
}
