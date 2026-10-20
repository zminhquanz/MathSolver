using MathSolver.Services;
using MathSolver.Services.Core;
using System.Diagnostics;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private bool CanTouchUi => IsLoaded && Handler?.MauiContext is not null
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
        CalculationAccelerationManager.AccelerationChanged -= OnAccelerationChanged;
        CalculationAccelerationManager.AccelerationChanged += OnAccelerationChanged;
        _hostWindow = Window;
        if (_hostWindow is not null) { _hostWindow.Destroying -= OnWindowClosing; _hostWindow.Destroying += OnWindowClosing; }
    }
    protected override void OnSolverUnloaded()
    {
        _active = false; _version++; _work?.Cancel();
        if (_busy) _statusKey = "Cancelled";
        CalculationAccelerationManager.AccelerationChanged -= OnAccelerationChanged;
        if (_hostWindow is not null) _hostWindow.Destroying -= OnWindowClosing;
        _hostWindow = null;
    }
    private void OnWindowClosing(object? sender, EventArgs e)
    { _active = false; _version++; _work?.Cancel(); if (_busy) _statusKey = "Cancelled"; }
    private void OnAccelerationChanged(object? sender, EventArgs e)
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

    private async Task RunWorkAsync<TResult>(Func<CancellationToken, IProgress<double>, Task<TResult>> work, Func<TResult, Task> publish, string status)
    {
        if (_busy || !CanTouchUi || !_active) return;
        var cancellation = new CancellationTokenSource();
        _work = cancellation; _busy = true;
        int version = ++_version;
        SetBusyState(); _progress.Progress = 0; Status(status);
        var progress = new Progress<double>(p => { if (CanPublish(version)) _progress.Progress = Math.Clamp(p, 0, 1); });
        try
        {
            var value = await work(cancellation.Token, progress);
            if (!cancellation.IsCancellationRequested && CanPublish(version)) await publish(value);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (CanPublish(version)) Status("Cancelled"); }
        catch (MatrixInputException error)
        { if (CanPublish(version)) ReportError("ImportError", error.Row, error.Column, error.Code); }
        catch (Exception error)
        {
            Debug.WriteLine(error);
            if (CanPublish(version)) ReportError("Failed");
        }
        finally
        {
            if (ReferenceEquals(_work, cancellation)) _work = null;
            _busy = false; cancellation.Dispose();
            if (CanTouchUi) SetBusyState();
        }
    }
}
