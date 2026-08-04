using MathSolver.Services;

namespace MathSolver.Views.Base;

/// <summary>
/// Base view for calculator modules that need to react to a language change.
/// It owns the event lifetime so embedded tabs do not leak static event handlers.
/// </summary>
public class LocalizedSolverView : ContentView
{
    private bool _isCultureSubscribed;

    public LocalizedSolverView()
    {
        Loaded += OnViewLoaded;
        Unloaded += OnViewUnloaded;
    }

    /// <summary>
    /// Call after InitializeComponent so the complete visual tree is available.
    /// </summary>
    protected void InitializeLocalization()
    {
        LocalizationService.Attach(this);
    }

    protected virtual void RefreshLocalizedContent()
    {
        LocalizationService.Attach(this);
    }

    protected virtual void OnSolverLoaded()
    {
    }

    protected virtual void OnSolverUnloaded()
    {
    }

    private void OnViewLoaded(object? sender, EventArgs e)
    {
        SubscribeCultureChanged();
        RefreshLocalizedContent();
        OnSolverLoaded();
    }

    private void OnViewUnloaded(object? sender, EventArgs e)
    {
        UnsubscribeCultureChanged();
        OnSolverUnloaded();
    }

    private void SubscribeCultureChanged()
    {
        if (_isCultureSubscribed)
        {
            return;
        }

        LocalizationService.CultureChanged += OnCultureChanged;
        _isCultureSubscribed = true;
    }

    private void UnsubscribeCultureChanged()
    {
        if (!_isCultureSubscribed)
        {
            return;
        }

        LocalizationService.CultureChanged -= OnCultureChanged;
        _isCultureSubscribed = false;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(RefreshLocalizedContent);
    }
}
