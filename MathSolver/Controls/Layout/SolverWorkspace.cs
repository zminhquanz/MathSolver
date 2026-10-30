using System.ComponentModel;
using MathSolver.Services;
using Microsoft.Maui.Layouts;

namespace MathSolver.Controls;

/// <summary>Reflows an existing solver body without replacing its inputs or adding scroll views.</summary>
public static class SolverWorkspace
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled", typeof(bool), typeof(SolverWorkspace), false, propertyChanged: OnEnabledChanged);
    public static readonly BindableProperty StartsResultProperty = BindableProperty.CreateAttached(
        "StartsResult", typeof(bool), typeof(SolverWorkspace), false);
    private static readonly BindableProperty ControllerProperty = BindableProperty.CreateAttached(
        "Controller", typeof(Controller), typeof(SolverWorkspace), null);

    public static bool GetIsEnabled(BindableObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(BindableObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    public static bool GetStartsResult(BindableObject target) => (bool)target.GetValue(StartsResultProperty);
    public static void SetStartsResult(BindableObject target, bool value) => target.SetValue(StartsResultProperty, value);

    // A compact header's description spans the whole scrolling body, above both
    // columns, regardless of which controller finishes initialization first.
    internal static Layout? GetDescriptionHost(ScrollView scroll) =>
        (scroll.GetValue(ControllerProperty) as Controller)?.DescriptionHost ?? scroll.Content as Layout;

    /// <summary>Call after successful publication only. Does not focus the result or change input focus.</summary>
    public static void RevealResult(VisualElement result, bool followFooterLayout = false)
    {
        for (Element? element = result; element is not null; element = element.Parent)
            if (element is ScrollView scroll && scroll.GetValue(ControllerProperty) is Controller controller)
            {
                controller.Reveal(result, followFooterLayout);
                return;
            }
    }

    private static void OnEnabledChanged(BindableObject target, object oldValue, object newValue)
    {
        if (target is not ScrollView scroll) return;
        if (scroll.GetValue(ControllerProperty) is Controller old) old.Dispose();
        scroll.ClearValue(ControllerProperty);
        if ((bool)newValue) scroll.SetValue(ControllerProperty, new Controller(scroll));
    }

    private sealed class Controller : IDisposable
    {
        private readonly ScrollView _scroll;
        private VerticalStackLayout? _body, _inputs, _results;
        private WorkspaceGrid? _grid;
        private SolverResultPlaceholder? _emptyResult;
        private VisualElement? _pendingResult;
        private bool _active, _split, _updating, _queued, _initializing, _disposed;
        private int _lifetimeVersion;
        private int _generation;
        private int _requiredArrange;
        private bool _followFooterLayout;
        private int _revealAttempts;
        private double _revealWidth, _revealWindowWidth, _revealWindowHeight;
        private readonly List<VisualElement> _resultChildren = [];
        private readonly List<VisualElement> _ancestors = [];

        internal Controller(ScrollView scroll)
        {
            _scroll = scroll;
            scroll.Loaded += OnLoaded;
            scroll.Unloaded += OnUnloaded;
            scroll.HandlerChanging += OnHandlerChanging;
            scroll.HandlerChanged += OnHandlerChanged;
            if (scroll.IsLoaded) ScheduleUpdate();
        }

        internal Layout? DescriptionHost => _body;

        private bool CanUseUi => _active && _scroll.IsLoaded && _scroll.Handler?.MauiContext is not null
            && _scroll.Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window)
            && _ancestors.All(view => view.IsVisible);

        private void OnLoaded(object? sender, EventArgs e) => ScheduleUpdate();
        private void OnUnloaded(object? sender, EventArgs e) => Deactivate();
        private void OnHandlerChanging(object? sender, HandlerChangingEventArgs e) => Deactivate();
        private void OnHandlerChanged(object? sender, EventArgs e) { if (_scroll.IsLoaded) ScheduleUpdate(); }

        private void ScheduleUpdate()
        {
            if (_disposed || _queued) return;
            _queued = true;
            int version = _lifetimeVersion;
            if (!_scroll.Dispatcher.Dispatch(() =>
            {
                if (version != _lifetimeVersion || _disposed) return;
                _queued = false;
                if (!_scroll.IsLoaded || _scroll.Handler?.MauiContext is null
                    || _scroll.Window is not { Handler.MauiContext: not null } window
                    || WindowUiLifetime.IsClosing(window)) return;
                Activate();
                UpdateLayout();
            })) _queued = false;
        }

        private void Activate()
        {
            if (_active || _initializing) return;
            _initializing = true;
            try
            {
                if (_grid is null && !CreateWorkspace()) return;
                _active = true;
                _scroll.SizeChanged += OnViewportChanged;
                _body!.SizeChanged += OnBodySizeChanged;
                _grid!.Arranged += OnWorkspaceArranged;
                DisplayDensityManager.Changed += OnViewportChanged;
                for (Element? element = _scroll; element is not null; element = element.Parent)
                    if (element is VisualElement visual)
                    {
                        _ancestors.Add(visual);
                        visual.PropertyChanged += OnAncestorPropertyChanged;
                    }
                foreach (var child in _resultChildren)
                {
                    child.PropertyChanged += OnResultPropertyChanged;
                }
            }
            finally { _initializing = false; }
        }

        private bool CreateWorkspace()
        {
            if (_scroll.Content is not VerticalStackLayout body) return false;
            var children = body.Children.ToArray();
            int firstResult = Array.FindIndex(children, child => child is BindableObject bindable && GetStartsResult(bindable));
            if (firstResult < 0) return false;
            _body = body;
            _inputs = new VerticalStackLayout { Spacing = body.Spacing };
            _results = new VerticalStackLayout { Spacing = body.Spacing };
            _emptyResult = new SolverResultPlaceholder();
            _results.Add(_emptyResult);
            _grid = new WorkspaceGrid { ColumnSpacing = body.Spacing, RowSpacing = body.Spacing };
            body.Children.Clear();
            for (int index = 0; index < children.Length; index++)
            {
                // The header may have moved its description here before this
                // controller initialized. Do not absorb it into the input column.
                if (children[index] is BindableObject description
                    && SolverHeader.GetRole(description) == SolverHeaderRole.Description)
                {
                    body.Children.Add(children[index]);
                    continue;
                }
                (index < firstResult ? _inputs : _results).Children.Add(children[index]);
                if (index >= firstResult && children[index] is VisualElement visual) _resultChildren.Add(visual);
            }
            _grid.Add(_inputs);
            _grid.Add(_results);
            body.Add(_grid);
            System.Diagnostics.Debug.WriteLine($"SolverWorkspace: initialized {firstResult} input sections and {children.Length - firstResult} result sections");
            return true;
        }

        private void Deactivate()
        {
            _active = false;
            _queued = false;
            _lifetimeVersion++;
            _pendingResult = null;
            _generation++;
            _scroll.SizeChanged -= OnViewportChanged;
            if (_body is not null) _body.SizeChanged -= OnBodySizeChanged;
            if (_grid is not null) _grid.Arranged -= OnWorkspaceArranged;
            DisplayDensityManager.Changed -= OnViewportChanged;
            foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= OnAncestorPropertyChanged;
            _ancestors.Clear();
            foreach (var child in _resultChildren)
            {
                child.PropertyChanged -= OnResultPropertyChanged;
            }
        }

        private void OnViewportChanged(object? sender, EventArgs e)
        {
            // A footer may change the available height while an explicit reveal
            // is settling. A real window/width resize still cancels that request.
            bool keepRequest = ReferenceEquals(sender, _scroll) && _followFooterLayout && _pendingResult is not null
                && Math.Abs(_scroll.Width - _revealWidth) < 1 && _scroll.Window is { } window
                && window.Width.Equals(_revealWindowWidth) && window.Height.Equals(_revealWindowHeight);
            if (!keepRequest) _pendingResult = null;
            _generation++;
            ScheduleUpdate();
            if (keepRequest && _grid is not null)
            {
                _requiredArrange = _grid.ArrangeVersion + 1;
                ((IView)_grid).InvalidateMeasure();
            }
        }
        private void OnBodySizeChanged(object? sender, EventArgs e) => ScheduleUpdate();
        private void OnWorkspaceArranged(object? sender, EventArgs e)
        {
            if (_pendingResult is null || _grid is null || _grid.ArrangeVersion < _requiredArrange) return;
            int generation = _generation;
            _scroll.Dispatcher.Dispatch(() => { if (generation == _generation) TryReveal(); });
        }
        private void OnAncestorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != VisualElement.IsVisibleProperty.PropertyName) return;
            _pendingResult = null;
            _generation++;
            ScheduleUpdate();
        }
        private void OnResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName) ScheduleUpdate();
        }

        private void UpdateLayout()
        {
            if (!CanUseUi || _updating || _grid is null || _body is null || _inputs is null || _results is null) return;
            _updating = true;
            try
            {
                _inputs.Spacing = _results.Spacing = _body.Spacing;
                _grid.ColumnSpacing = _grid.RowSpacing = _body.Spacing;
                double width = Math.Min(_scroll.Width, _body.Width) - _body.Padding.HorizontalThickness;
                bool split = ResponsiveLayoutPolicy.ForSolver(width, _scroll.Height, ResponsiveLayoutPolicy.TextScale).UseSideBySide;
                bool hasResults = _resultChildren.Any(child => child.IsVisible);
                // Reserve the destination before publication so entering a result
                // does not resize/reflow the user's input column on desktop.
                if (_emptyResult is not null) _emptyResult.IsVisible = split && !hasResults;
                _results.IsVisible = split || hasResults;
                if (_grid.RowDefinitions.Count > 0 && split == _split) return;
                _split = split;
                _grid.ColumnDefinitions.Clear();
                _grid.RowDefinitions.Clear();
                _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                if (split) _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                else _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _grid.SetRow(_results, split ? 0 : 1);
                _grid.SetColumn(_results, split ? 1 : 0);
            }
            finally { _updating = false; }
        }

        internal void Reveal(VisualElement result, bool followFooterLayout)
        {
            if (!CanUseUi || !ContainsResult(result)) return;
            UpdateLayout();
            _pendingResult = result;
            _followFooterLayout = followFooterLayout;
            _revealAttempts = 0;
            _revealWidth = _scroll.Width;
            _revealWindowWidth = _scroll.Window!.Width;
            _revealWindowHeight = _scroll.Window.Height;
            _generation++;
            _requiredArrange = _grid!.ArrangeVersion + 1;
            // Even unchanged output needs one fresh arrangement before inspecting its position.
            // The layout manager signals completion; no elapsed-time guess or focus change is needed.
            ((IView)_grid).InvalidateMeasure();
        }

        private bool ContainsResult(VisualElement result)
        {
            for (Element? element = result; element is not null; element = element.Parent)
                if (ReferenceEquals(element, _results)) return true;
            return false;
        }

        private void TryReveal()
        {
            var result = _pendingResult;
            _pendingResult = null;
            if (!CanUseUi || result is null || result.Height <= 0
                || !result.IsLoaded || result.Handler?.MauiContext is null
                || !ReferenceEquals(result.Window, _scroll.Window) || !ContainsResult(result)) return;
            double top = 0;
            Element? element = result;
            while (element is VisualElement visual && !ReferenceEquals(element, _scroll.Content))
            {
                if (!visual.IsVisible || visual.Height <= 0) return;
                top += visual.Y;
                element = visual.Parent;
            }
            if (!ReferenceEquals(element, _scroll.Content) || !double.IsFinite(top)) return;
            double leadingHeight = Math.Min(Math.Min(80 * ResponsiveLayoutPolicy.TextScale, result.Height), _scroll.Height);
            if (top >= _scroll.ScrollY && top + leadingHeight <= _scroll.ScrollY + _scroll.Height + 1) return;
            bool confirmAfterArrange = _followFooterLayout && _revealAttempts++ < 3;
            if (confirmAfterArrange) _pendingResult = result;
            _ = ScrollToResultAsync(top, confirmAfterArrange, _generation);
        }

        private async Task ScrollToResultAsync(double top, bool confirmAfterArrange, int generation)
        {
            if (!CanUseUi) return;
            try
            {
                await _scroll.ScrollToAsync(0, Math.Max(0, top), false);
                // Check a fresh arrangement: native anchoring/changed footer height
                // can otherwise restore the old offset after ChangeView completes.
                if (confirmAfterArrange && generation == _generation && CanUseUi && _pendingResult is not null)
                {
                    _requiredArrange = _grid!.ArrangeVersion + 1;
                    ((IView)_grid).InvalidateMeasure();
                }
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) when (!CanUseUi) { }
        }

        public void Dispose()
        {
            _disposed = true;
            Deactivate();
            _scroll.Loaded -= OnLoaded;
            _scroll.Unloaded -= OnUnloaded;
            _scroll.HandlerChanging -= OnHandlerChanging;
            _scroll.HandlerChanged -= OnHandlerChanged;
            if (_body is null || _grid is null || _inputs is null || _results is null) return;
            int index = _body.Children.IndexOf(_grid);
            if (index < 0) return;
            var children = _inputs.Children.Concat(_results.Children.Where(child => !ReferenceEquals(child, _emptyResult))).ToArray();
            _inputs.Children.Clear();
            _results.Children.Clear();
            _body.Children.Remove(_grid);
            foreach (var child in children) _body.Children.Insert(index++, child);
        }
    }

    private sealed class WorkspaceGrid : Grid
    {
        internal int ArrangeVersion { get; private set; }
        internal event EventHandler? Arranged;
        protected override ILayoutManager CreateLayoutManager() => new ArrangementObserver(this, base.CreateLayoutManager());

        private sealed class ArrangementObserver(WorkspaceGrid owner, ILayoutManager inner) : ILayoutManager
        {
            public Size Measure(double widthConstraint, double heightConstraint) => inner.Measure(widthConstraint, heightConstraint);
            public Size ArrangeChildren(Rect bounds)
            {
                Size size = inner.ArrangeChildren(bounds);
                owner.ArrangeVersion++;
                owner.Arranged?.Invoke(owner, EventArgs.Empty);
                return size;
            }
        }
    }
}
