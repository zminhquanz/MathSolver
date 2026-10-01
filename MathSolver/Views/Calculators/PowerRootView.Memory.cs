using MathSolver.Services;
using System.Diagnostics;
using System.Numerics;

namespace MathSolver.Views;

public partial class PowerRootView : IRecoverableMemoryOwner, ITemporaryResourceOwner
{
    // AppData prevents Android's cache eviction from deleting the only exact
    // copy while the preview is still visible. Files remain private to this app.
    private static readonly Lazy<ResultFileStore> ResultFiles = new(() =>
    {
        var files = new ResultFileStore(Path.Combine(FileSystem.AppDataDirectory, "temporary-power-results"));
        files.DeleteAbandonedFiles(DateTime.UtcNow - TimeSpan.FromDays(7));
        return files;
    });
    private readonly SemaphoreSlim _resultStorageGate = new(1, 1);
    private CancellationTokenSource? _inactiveResultCancellation;
    private CancellationTokenSource _resultLifetimeCancellation = new();
    private FileStream? _resultFileLease;
    private bool _isTabActive;

    public void SetTabActive(bool active)
    {
        _isTabActive = active;
        _inactiveResultCancellation?.Cancel();
        if (!active || AppMemoryPolicy.IsInBackground)
            ScheduleInactiveResultArchive();
    }

    private void ScheduleInactiveResultArchive()
    {
        _inactiveResultCancellation?.Cancel();
        if (_isCalculating || _isExporting ||
            (_isTabActive && !AppMemoryPolicy.IsInBackground) ||
            _calculationState is null || GetResidentResultBytes(_calculationState) < AppMemoryPolicy.LargeResultThresholdBytes)
            return;

        var cancellation = new CancellationTokenSource();
        _inactiveResultCancellation = cancellation;
        _ = ArchiveAfterIdleAsync(cancellation);
    }

    private async Task ArchiveAfterIdleAsync(CancellationTokenSource cancellation)
    {
        try
        {
            if (!AppMemoryPolicy.IsInBackground)
                await Task.Delay(AppMemoryPolicy.InactiveResultDelay, cancellation.Token);
            long released = await ArchiveResultAsync(cancellation.Token);
            await AppMemoryPolicy.CollectReleasedMemoryAsync(released);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Debug.WriteLine($"Power result kept in memory: {error.Message}"); }
        finally
        {
            if (ReferenceEquals(_inactiveResultCancellation, cancellation))
                _inactiveResultCancellation = null;
            cancellation.Dispose();
        }
    }

    public Task<long> ReleaseRecoverableMemoryAsync()
    {
        if (_isTabActive && !AppMemoryPolicy.IsInBackground) return Task.FromResult(0L);
        _inactiveResultCancellation?.Cancel();
        return ArchiveResultAsync(CancellationToken.None);
    }

    public void ReleaseTemporaryResources() => ReleaseStoredResult();

    private async Task<long> ArchiveResultAsync(CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _resultLifetimeCancellation.Token);
        cancellationToken = lifetime.Token;
        await _resultStorageGate.WaitAsync(cancellationToken);
        string? archivePath = null;
        try
        {
            PowerCalculationState? state = _calculationState;
            if (_isCalculating || _isExporting || state is null || state.CachedResultPath is not null)
                return 0;
            long bytes = GetResidentResultBytes(state);
            if (bytes < AppMemoryPolicy.LargeResultThresholdBytes) return 0;
            int version = _calculationVersion;
            bool useSimd = CalculationAccelerationManager.UsePowerExportSimd;
            archivePath = await ResultFiles.Value.CreateAsync((path, token) =>
                WriteFullResultFile(path, state, null, token, useSimd), cancellationToken);

            // A clear or new calculation can occur while the file is being written.
            // It must never be overwritten by the completed archive of an old result.
            cancellationToken.ThrowIfCancellationRequested();
            if (version != _calculationVersion || !ReferenceEquals(state, _calculationState))
                return 0;

            // Keep an open read lease so another Windows app instance cannot
            // prune this session's only exact copy as an old temporary file.
            _resultFileLease = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _calculationState = state with
            {
                Result = BigInteger.Zero,
                ParallelMagnitude = null,
                BinaryMagnitude = null,
                CachedResultPath = archivePath,
                WasBinaryMagnitude = state.HasBinaryMagnitude
            };
            archivePath = null; // The current result now owns the completed file.
            return bytes;
        }
        finally
        {
            if (archivePath is not null) ResultFiles.Value.Delete(archivePath);
            _resultStorageGate.Release();
        }
    }

    private static long GetResidentResultBytes(PowerCalculationState state) =>
        state.CachedResultPath is not null ? 0 :
        state.Result.GetByteCount() + (state.ParallelMagnitude?.StorageBytes ?? 0) +
        (state.BinaryMagnitude?.StorageBytes ?? 0);

    private void ReleaseStoredResult()
    {
        _inactiveResultCancellation?.Cancel();
        _resultLifetimeCancellation.Cancel();
        _resultLifetimeCancellation.Dispose();
        _resultLifetimeCancellation = new();
        _resultFileLease?.Dispose();
        _resultFileLease = null;
        ResultFiles.Value.Delete(_calculationState?.CachedResultPath);
        _calculationState = null;
    }
}
