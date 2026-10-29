using MathSolver.Services;
using MathSolver.Services.Core;
using System.Diagnostics;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private bool CanTouchUi => !_closing && IsLoaded && Handler?.MauiContext is not null
        && Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);
    private bool CanRefreshLocalization => !_constructed || CanTouchUi;
    private bool CanPublish(int version) => CanTouchUi && _active && version == _version;

    public void SetTabActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        if (!active)
        {
            _viewport.CancelRangeSelection();
            if (CanTouchUi) { _detailsRequested = _technicalRequested = false; CloseMatrixTools(); _cellValuePanel.IsVisible = false; RefreshWorkspacePresentation(); }
            _version++; _work?.Cancel(); if (_busy) _statusKey = "Cancelled";
        }
        else if (CanTouchUi) { SetBusyState(); if (_statusKey is not null) _message.Text = FormatStatus(_statusKey); }
    }
    protected override void OnSolverLoaded()
    {
        if (!CanTouchUi) return;
        AttachMatrixDetailsKeys();
        _active = true; _cellValuePanel.IsVisible = false; SetBusyState();
        CalculationAccelerationManager.AccelerationChanged -= OnExecutionSettingsChanged;
        CalculationAccelerationManager.AccelerationChanged += OnExecutionSettingsChanged;
        CalculationThreadingManager.ThreadingChanged -= OnExecutionSettingsChanged;
        CalculationThreadingManager.ThreadingChanged += OnExecutionSettingsChanged;
        if (_hostWindow is not null && !ReferenceEquals(_hostWindow, Window))
            _hostWindow.Destroying -= OnWindowClosing;
        _hostWindow = Window;
        if (_hostWindow is not null) { _hostWindow.Destroying -= OnWindowClosing; _hostWindow.Destroying += OnWindowClosing; }
    }
    protected override void OnSolverUnloaded()
    {
        _detailsRequested = _technicalRequested = false; CloseMatrixTools();
        DetachMatrixDetailsKeys();
        _active = false; _version++; _work?.Cancel();
        if (_busy) _statusKey = "Cancelled";
        CalculationAccelerationManager.AccelerationChanged -= OnExecutionSettingsChanged;
        CalculationThreadingManager.ThreadingChanged -= OnExecutionSettingsChanged;
        // Tab switches unload this view. Retain its result and closing subscription
        // so a cached, inactive tab still releases its backing file at window close.
    }
    private void OnWindowClosing(object? sender, EventArgs e)
    {
        _closing = true; _active = false; _version++; _work?.Cancel();
        _viewport.DetachSourceReader();
        _viewportResult = null;
        _failedReadDraft = null;
        if (_busy) _statusKey = "Cancelled";
        else ReleaseClosedResult();
        CalculationAccelerationManager.AccelerationChanged -= OnExecutionSettingsChanged;
        CalculationThreadingManager.ThreadingChanged -= OnExecutionSettingsChanged;
        if (_hostWindow is not null) _hostWindow.Destroying -= OnWindowClosing;
        _hostWindow = null;
    }

    private void ReleaseClosedResult()
    {
        _viewportResult = null;
        _failedReadResult = null;
        // Called only after active workers have finished reading inputs/results.
        _viewport.DetachSourceReader();
        try { MatrixRecreation.Release(ref _a, ref _b, ref _result); }
        catch (Exception error) { Debug.WriteLine(error); }
    }

    private static void DisposeOutcome(object? value)
    {
        if (value is not IDisposable disposable) return;
        try { disposable.Dispose(); }
        catch (Exception error) { Debug.WriteLine(error); }
    }
    private void OnExecutionSettingsChanged(object? sender, EventArgs e)
    {
        if (!CanTouchUi) return;
        int version = _version;
        void Invalidate()
        {
            if (!CanTouchUi || !_busy || version != _version) return;
            _version++; _work?.Cancel(); Status("Cancelled");
        }
        if (Dispatcher.IsDispatchRequired) Dispatcher.Dispatch(Invalidate);
        else Invalidate();
    }

    private Task RunWorkAsync<TResult>(Func<CancellationToken, IProgress<double>, Task<TResult>> work,
        Func<TResult, Task> publish, string status, string memoryErrorKey = "MemoryUnavailable") =>
        RunWorkAsync((token, progress, _) => work(token, progress), publish, status, memoryErrorKey: memoryErrorKey);

    private async Task RunWorkAsync<TResult>(
        Func<CancellationToken, IProgress<double>, IProgress<MatrixCalculationProgress>, Task<TResult>> work,
        Func<TResult, Task> publish, string status, bool showCalculationProgress = false,
        string memoryErrorKey = "MemoryUnavailable")
    {
        if (_busy || !CanTouchUi || !_active) return;
        var cancellation = new CancellationTokenSource();
        _work = cancellation; _busy = true;
        int version = ++_version;
        _calculationStage = showCalculationProgress ? MatrixCalculationStage.Preparing : null;
        SetBusyState(); _progress.Progress = 0; Status(status);
        bool acceptingProgress = true;
        bool CanUpdateProgress() => acceptingProgress && _busy && CanPublish(version)
            && ReferenceEquals(_work, cancellation) && !cancellation.IsCancellationRequested;
        void UpdateFraction(double fraction)
        {
            if (!double.IsFinite(fraction)) return;
            _progress.Progress = Math.Max(_progress.Progress, Math.Clamp(fraction, 0, 0.99));
            RefreshProgress();
        }
        // Construct both adapters on the UI context and bind them to this job.
        // Queued reports may outlive workers, publication, stop, or a tab/window.
        var progress = new Progress<double>(fraction =>
        {
            if (CanUpdateProgress()) UpdateFraction(fraction);
        });
        var calculationProgress = new Progress<MatrixCalculationProgress>(report =>
        {
            if (!showCalculationProgress || !CanUpdateProgress()) return;
            _calculationStage = report.Stage == MatrixCalculationStage.Completed
                ? MatrixCalculationStage.Finalizing : report.Stage;
            UpdateFraction(report.Fraction);
        });
        TResult? value = default;
        bool hasOutcome = false, published = false;
        try
        {
            value = await work(cancellation.Token, progress, calculationProgress);
            acceptingProgress = false;
            hasOutcome = true;
            if (!cancellation.IsCancellationRequested && CanPublish(version))
            {
                await publish(value);
                published = true;
            }
            else if (cancellation.IsCancellationRequested && CanPublish(version)) Status("Cancelled");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (CanPublish(version)) Status("Cancelled"); }
        catch (MatrixInputException error)
        { if (CanPublish(version)) ReportError("ImportError", error.Row, error.Column, error.Code); }
        catch (Exception error) when (MatrixMemoryGuard.IsMemoryFailure(error))
        { if (CanPublish(version)) ReportError(memoryErrorKey); }
        catch (Exception error) when (IsResultStorageFailure(error))
        {
            Debug.WriteLine(error);
            if (CanPublish(version)) ReportError("StorageUnavailable");
        }
        catch (Exception error)
        {
            Debug.WriteLine(error);
            if (CanPublish(version)) ReportError("Failed");
        }
        finally
        {
            acceptingProgress = false;
            // A completed calculation may arrive after cancellation, a tab switch,
            // or closure; it still owns its temporary file until publication succeeds.
            if (hasOutcome && !published) DisposeOutcome(value);
            if (ReferenceEquals(_work, cancellation)) _work = null;
            _busy = false; cancellation.Dispose();
            // Copy/export may have been reading the current result when close was
            // requested. Release it only after that work has stopped using GetValue.
            if (_closing) ReleaseClosedResult();
            if (CanTouchUi) SetBusyState();
        }
    }
}
