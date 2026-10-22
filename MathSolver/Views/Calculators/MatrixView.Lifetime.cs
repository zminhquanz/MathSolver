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
        if (!active) { _version++; _work?.Cancel(); if (_busy) _statusKey = "Cancelled"; }
        else if (CanTouchUi) { SetBusyState(); if (_statusKey is not null) _message.Text = T(_statusKey); }
    }
    protected override void OnSolverLoaded()
    {
        if (!CanTouchUi) return;
        _active = true; SetBusyState();
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
        _viewportResult = null;
        _viewportDraft = null;
        if (_busy) _statusKey = "Cancelled";
        else ReleaseClosedStorage();
        CalculationAccelerationManager.AccelerationChanged -= OnExecutionSettingsChanged;
        CalculationThreadingManager.ThreadingChanged -= OnExecutionSettingsChanged;
        if (_hostWindow is not null) _hostWindow.Destroying -= OnWindowClosing;
        _hostWindow = null;
    }

    private void ReleaseClosedStorage()
    {
        _viewportResult = null;
        _viewportDraft = null;
        _failedReadDraft = null;
        var result = _result;
        _result = null; _failedReadResult = null;
        DisposeOutcome(result);
        DisposeOutcome(_a);
        DisposeOutcome(_b);
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

    private async Task RunWorkAsync<TResult>(Func<CancellationToken, IProgress<double>, Task<TResult>> work, Func<TResult, Task<bool>> publish, string status)
    {
        if (_busy || !CanTouchUi || !_active) return;
        var cancellation = new CancellationTokenSource();
        _work = cancellation; _busy = true;
        int version = ++_version;
        SetBusyState(); _progress.Progress = 0; Status(status);
        var progress = new Progress<double>(p => { if (CanPublish(version)) _progress.Progress = Math.Clamp(p, 0, 1); });
        TResult? value = default;
        bool hasOutcome = false, published = false;
        try
        {
            value = await work(cancellation.Token, progress);
            hasOutcome = true;
            if (cancellation.IsCancellationRequested)
            {
                if (CanPublish(version)) Status("Cancelled");
            }
            else if (CanPublish(version))
            {
                published = await publish(value);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (CanPublish(version)) Status("Cancelled"); }
        catch (MatrixInputException error)
        { if (CanPublish(version)) ReportError("ImportError", error.Row, error.Column, error.Code); }
        catch (Exception error) when (MatrixMemoryGuard.IsMemoryFailure(error))
        { if (CanPublish(version)) ReportError("MemoryUnavailable"); }
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
            // A completed calculation may arrive after cancellation, a tab switch,
            // or closure; it still owns its temporary file until publication succeeds.
            if (hasOutcome && !published) DisposeOutcome(value);
            if (ReferenceEquals(_work, cancellation)) _work = null;
            _busy = false; cancellation.Dispose();
            // Copy/export may have been reading the current result when close was
            // requested. Release it only after that work has stopped using GetValue.
            if (_closing) ReleaseClosedStorage();
            if (CanTouchUi) SetBusyState();
        }
    }
}
