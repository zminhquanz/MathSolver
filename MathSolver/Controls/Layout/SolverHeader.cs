using MathSolver.Services;

namespace MathSolver.Controls;

public enum SolverHeaderRole { None, Symbol, Title, Description }

/// <summary>Keeps solver titles visible and moves their existing description into the body when compact.</summary>
public static class SolverHeader
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled", typeof(bool), typeof(SolverHeader), false, propertyChanged: OnEnabledChanged);
    public static readonly BindableProperty RoleProperty = BindableProperty.CreateAttached(
        "Role", typeof(SolverHeaderRole), typeof(SolverHeader), SolverHeaderRole.None);
    public static readonly BindableProperty CompactTitleProperty = BindableProperty.CreateAttached(
        "CompactTitle", typeof(string), typeof(SolverHeader), string.Empty,
        propertyChanged: (target, _, _) =>
        {
            if (target.GetValue(ControllerProperty) is Controller controller) controller.ScheduleUpdate();
        });
    private static readonly BindableProperty ControllerProperty = BindableProperty.CreateAttached(
        "Controller", typeof(Controller), typeof(SolverHeader), null);

    public static bool GetIsEnabled(BindableObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(BindableObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    public static SolverHeaderRole GetRole(BindableObject target) => (SolverHeaderRole)target.GetValue(RoleProperty);
    public static void SetRole(BindableObject target, SolverHeaderRole value) => target.SetValue(RoleProperty, value);
    public static string GetCompactTitle(BindableObject target) => (string)target.GetValue(CompactTitleProperty);
    public static void SetCompactTitle(BindableObject target, string value) => target.SetValue(CompactTitleProperty, value);

    private static void OnEnabledChanged(BindableObject target, object oldValue, object newValue)
    {
        if (target is not VerticalStackLayout header) return;
        if (header.GetValue(ControllerProperty) is Controller old) old.Dispose();
        header.ClearValue(ControllerProperty);
        if ((bool)newValue) header.SetValue(ControllerProperty, new Controller(header));
    }

    private sealed class Controller : IDisposable
    {
        private readonly VerticalStackLayout _header;
        private VisualElement? _root;
        private ScrollView? _scroll;
        private VerticalStackLayout? _body;
        private readonly List<(VisualElement View, bool Visible)> _symbols = [];
        private readonly List<(View View, Layout Parent, int Index)> _descriptions = [];
        private readonly List<(Label Original, bool Visible, Label Compact, Layout Parent)> _titles = [];
        private Thickness _fullPadding;
        private bool _active, _updating, _compact, _hasFullPadding, _queued, _initializing, _disposed;
        private int _generation;
        private string? _appliedCompactTitle;

        internal Controller(VerticalStackLayout header)
        {
            _header = header;
            header.Loaded += OnLoaded;
            header.Unloaded += OnUnloaded;
            header.HandlerChanging += OnHandlerChanging;
            header.HandlerChanged += OnHandlerChanged;
            if (header.IsLoaded) ScheduleUpdate();
        }

        private bool CanUseUi => _active && _header.IsLoaded && _header.Handler?.MauiContext is not null
            && _header.Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window);

        private void OnLoaded(object? sender, EventArgs e) => ScheduleUpdate();
        private void OnUnloaded(object? sender, EventArgs e) => Deactivate();
        private void OnHandlerChanging(object? sender, HandlerChangingEventArgs e) => Deactivate();
        private void OnHandlerChanged(object? sender, EventArgs e) { if (_header.IsLoaded) ScheduleUpdate(); }

        internal void ScheduleUpdate()
        {
            if (_disposed || _queued) return;
            _queued = true;
            int generation = _generation;
            // Loaded/SizeChanged can be raised inside WinUI's child/layout traversal.
            // Finish that traversal before inserting or moving native children.
            if (!_header.Dispatcher.Dispatch(() =>
            {
                if (generation != _generation || _disposed) return;
                _queued = false;
                if (!_header.IsLoaded || _header.Handler?.MauiContext is null
                    || _header.Window is not { Handler.MauiContext: not null } window
                    || WindowUiLifetime.IsClosing(window)) return;
                Activate();
                UpdateCompactTitles();
                Update();
            })) _queued = false;
        }

        private void Activate()
        {
            if (_active || _initializing || _header.Parent is not Layout root) return;
            _initializing = true;
            try
            {
                _root = root;
                _scroll = root.Children.OfType<ScrollView>().FirstOrDefault();
                _body = _scroll?.Content as VerticalStackLayout;
                if (!_hasFullPadding) { _fullPadding = _header.Padding; _hasFullPadding = true; }
                if (_symbols.Count == 0 && _descriptions.Count == 0 && _titles.Count == 0)
                {
                    Discover(_header);
                    foreach (var (original, _, compact, parent) in _titles)
                        parent.Children.Insert(parent.Children.IndexOf(original) + 1, compact);
                }
                _active = true;
                _root.SizeChanged += OnSizeChanged;
                _header.SizeChanged += OnSizeChanged;
                if (_scroll is not null) _scroll.SizeChanged += OnSizeChanged;
                DisplayDensityManager.Changed += OnSizeChanged;
            }
            finally { _initializing = false; }
        }

        private void Discover(Layout layout)
        {
            for (int index = 0; index < layout.Children.Count; index++)
            {
                if (layout.Children[index] is not View view) continue;
                switch (GetRole(view))
                {
                    case SolverHeaderRole.Symbol: _symbols.Add((view, view.IsVisible)); break;
                    case SolverHeaderRole.Description: _descriptions.Add((view, layout, index)); break;
                    case SolverHeaderRole.Title when view is Label title:
                        var compact = new Label
                        {
                            IsVisible = false
                        };
                        compact.SetDynamicResource(Label.StyleProperty, "SolverCompactHeaderTitleStyle");
                        // This label follows the original localized title. The
                        // legacy tree translator must not replace its binding.
                        LocalizationService.ExcludeSubtreeFromLegacyTracking(compact);
                        compact.SetBinding(Label.TextProperty, new Binding(nameof(Label.Text), source: title));
                        compact.SetBinding(Label.FontFamilyProperty, new Binding(nameof(Label.FontFamily), source: title));
                        compact.SetBinding(Label.TextColorProperty, new Binding(nameof(Label.TextColor), source: title));
                        compact.SetBinding(Label.FontAutoScalingEnabledProperty, new Binding(nameof(Label.FontAutoScalingEnabled), source: title));
                        compact.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(Label.Text), source: title));
                        compact.SetBinding(ToolTipProperties.TextProperty, new Binding(nameof(Label.Text), source: title));
                        _titles.Add((title, title.IsVisible, compact, layout));
                        break;
                }
                if (view is Layout child) Discover(child);
            }
        }

        private void OnSizeChanged(object? sender, EventArgs e) => ScheduleUpdate();

        private void UpdateCompactTitles()
        {
            if (!CanUseUi) return;
            string text = GetCompactTitle(_header);
            if (text == _appliedCompactTitle) return;
            _appliedCompactTitle = text;
            foreach (var (original, _, compact, _) in _titles)
            {
                if (string.IsNullOrWhiteSpace(text))
                    compact.SetBinding(Label.TextProperty, new Binding(nameof(Label.Text), source: original));
                else
                {
                    compact.RemoveBinding(Label.TextProperty);
                    compact.Text = text;
                }
            }
        }

        private void Update()
        {
            if (!CanUseUi || _updating || _root is null || _root.Width <= 0 || _root.Height <= 0) return;
            double width = Math.Min(_header.Width > 0 ? _header.Width : _root.Width, _root.Width)
                - _header.Padding.HorizontalThickness;
            bool paddingChanged = false;
            if (Application.Current?.Resources.TryGetValue("Density.HeaderPadding", out var value) == true
                && value is Thickness padding && !padding.Equals(_fullPadding))
            {
                _fullPadding = padding;
                paddingChanged = true;
            }
            bool compact = ResponsiveLayoutPolicy.ForSolver(width, _root.Height, ResponsiveLayoutPolicy.TextScale).CompactHeader;
            bool modeChanged = compact != _compact;
            if (!modeChanged && !paddingChanged) return;
            _updating = true;
            try
            {
                // Keep the localized Label objects and their resource/binding values intact.
                // Without a compatible body, leave the description available in the header.
                _compact = compact;
                System.Diagnostics.Debug.WriteLine($"SolverHeader: compact={compact}");
                _header.Padding = compact
                    ? new Thickness(_fullPadding.Left, 8, _fullPadding.Right, 8) : _fullPadding;
                // Symbols identify the current topic even in a short viewport.
                // Compact the title/description without hiding the mathematical row.
                foreach (var (view, visible) in _symbols) view.IsVisible = visible;
                foreach (var (original, visible, title, _) in _titles)
                {
                    original.IsVisible = compact ? false : visible;
                    title.IsVisible = compact && visible;
                }
                if (_body is null || !modeChanged) return;
                if (compact)
                {
                    int index = 0;
                    foreach (var (view, _, _) in _descriptions)
                    {
                        if (view.Parent is not Layout parent) continue;
                        parent.Children.Remove(view);
                        (SolverWorkspace.GetDescriptionHost(_scroll!) ?? _body).Children.Insert(index++, view);
                    }
                }
                else RestoreDescriptions();
            }
            finally { _updating = false; }
        }

        private void RestoreDescriptions()
        {
            foreach (var (view, parent, index) in _descriptions)
            {
                if (ReferenceEquals(view.Parent, parent)) continue;
                if (view.Parent is Layout current) current.Children.Remove(view);
                parent.Children.Insert(Math.Min(index, parent.Children.Count), view);
            }
        }

        private void Deactivate()
        {
            _active = false;
            _queued = false;
            _generation++;
            if (_root is not null) _root.SizeChanged -= OnSizeChanged;
            _header.SizeChanged -= OnSizeChanged;
            if (_scroll is not null) _scroll.SizeChanged -= OnSizeChanged;
            DisplayDensityManager.Changed -= OnSizeChanged;
        }

        public void Dispose()
        {
            bool restore = CanUseUi;
            _disposed = true;
            Deactivate();
            _header.Loaded -= OnLoaded;
            _header.Unloaded -= OnUnloaded;
            _header.HandlerChanging -= OnHandlerChanging;
            _header.HandlerChanged -= OnHandlerChanged;
            if (!restore) return;
            foreach (var (view, visible) in _symbols) view.IsVisible = visible;
            _header.Padding = _fullPadding;
            foreach (var (original, visible, compact, parent) in _titles)
            {
                original.IsVisible = visible;
                parent.Children.Remove(compact);
            }
            RestoreDescriptions();
        }
    }
}
