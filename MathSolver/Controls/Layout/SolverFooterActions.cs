using System.ComponentModel;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Reflows visible footer actions, keeping Clear and Copy together below the primary action on narrow views.</summary>
public static class SolverFooterActions
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled", typeof(bool), typeof(SolverFooterActions), false, propertyChanged: OnEnabledChanged);
    private static readonly BindableProperty ControllerProperty = BindableProperty.CreateAttached(
        "Controller", typeof(Controller), typeof(SolverFooterActions), null);
    public static bool GetIsEnabled(BindableObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(BindableObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    private static void OnEnabledChanged(BindableObject target, object oldValue, object newValue)
    {
        if (target is not Grid grid) return;
        if (grid.GetValue(ControllerProperty) is Controller old) old.Dispose();
        grid.ClearValue(ControllerProperty);
        if ((bool)newValue) grid.SetValue(ControllerProperty, new Controller(grid));
    }

    private sealed class Controller : IDisposable
    {
        private readonly Grid _grid;
        private VisualElement[] _observed = [], _lastActions = [];
        private int _generation, _lastColumns;
        private bool _active, _queued, _disposed;
        internal Controller(Grid grid)
        {
            _grid = grid;
            grid.Loaded += OnLoaded;
            grid.Unloaded += OnUnloaded;
            grid.SizeChanged += OnSizeChanged;
            grid.HandlerChanging += OnHandlerChanging;
            grid.HandlerChanged += OnHandlerChanged;
            grid.PropertyChanged += OnPropertyChanged;
            if (grid.IsLoaded) Activate();
        }
        private bool CanUseUi => _active && _grid.IsLoaded && _grid.IsVisible && _grid.Width > 0
            && _grid.Handler?.MauiContext is not null
            && _grid.Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);
        private void OnLoaded(object? sender, EventArgs e) => Activate();
        private void Activate()
        {
            if (_disposed || _active) return;
            _active = true;
            _observed = _grid.Children.OfType<VisualElement>().ToArray();
            foreach (var child in _observed) child.PropertyChanged += OnPropertyChanged;
            DisplayDensityManager.Changed += OnSizeChanged;
            Schedule();
        }
        private void OnUnloaded(object? sender, EventArgs e) => Deactivate();
        private void OnHandlerChanging(object? sender, HandlerChangingEventArgs e) => Deactivate();
        private void OnHandlerChanged(object? sender, EventArgs e) { if (_grid.IsLoaded) Activate(); }
        private void OnSizeChanged(object? sender, EventArgs e) => Schedule();
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName) Schedule();
        }
        private void Schedule()
        {
            if (_disposed || !_active || _queued) return;
            _queued = true;
            int generation = _generation;
            // Visibility/layout events can occur inside WinUI traversal. Change
            // definitions only after that pass, and only for the same UI lifetime.
            if (!_grid.Dispatcher.Dispatch(() =>
            {
                if (_disposed || generation != _generation) return;
                _queued = false;
                if (CanUseUi) Update();
            })) _queued = false;
        }
        private void Update()
        {
            var actions = _grid.Children.OfType<VisualElement>().Where(view => view.IsVisible).ToArray();
            if (actions.Length == 0) return;
            int columns = Math.Min(actions.Length, ResponsiveLayoutPolicy.Columns(
                _grid.Width - _grid.Padding.HorizontalThickness, 140, 3, _grid.ColumnSpacing));
            bool primaryRow = actions.Length == 3 && columns == 2;
            int rows = primaryRow ? 2 : (actions.Length + columns - 1) / columns;
            if (_lastColumns == columns && _lastActions.SequenceEqual(actions)) return;
            _lastColumns = columns; _lastActions = actions;
            _grid.ColumnDefinitions.Clear(); _grid.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) _grid.ColumnDefinitions.Add(new(GridLength.Star));
            for (int i = 0; i < rows; i++) _grid.RowDefinitions.Add(new(GridLength.Auto));
            for (int i = 0; i < actions.Length; i++)
            {
                Grid.SetRow(actions[i], primaryRow ? (i == 0 ? 0 : 1) : i / columns);
                Grid.SetColumn(actions[i], primaryRow ? (i == 0 ? 0 : i - 1) : i % columns);
                Grid.SetColumnSpan(actions[i], primaryRow && i == 0 ? 2 : 1);
            }
        }
        private void Deactivate()
        {
            _active = _queued = false; _generation++;
            foreach (var child in _observed) child.PropertyChanged -= OnPropertyChanged;
            _observed = [];
            DisplayDensityManager.Changed -= OnSizeChanged;
        }
        public void Dispose()
        {
            _disposed = true; Deactivate();
            _grid.Loaded -= OnLoaded; _grid.Unloaded -= OnUnloaded; _grid.SizeChanged -= OnSizeChanged;
            _grid.HandlerChanging -= OnHandlerChanging; _grid.HandlerChanged -= OnHandlerChanged;
            _grid.PropertyChanged -= OnPropertyChanged;
        }
    }
}
