using System.ComponentModel;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Real status text plus a quiet, optional acknowledgement of committed work.</summary>
[ContentProperty(nameof(Message))]
public sealed class CompletionNotice : ContentView
{
    private readonly Grid _row = new() { ColumnDefinitions = [new(new GridLength(24)), new(GridLength.Star)], ColumnSpacing = 8 };
    private readonly CompletionStamp _stamp = new();
    private Label? _message;
    public Label? Message
    {
        get => _message;
        set
        {
            if (_message == value) return;
            if (_message is { } old) { old.PropertyChanged -= OnMessageChanged; _row.Remove(old); }
            _message = value;
            if (value is not null) { _row.Add(value, 1); value.PropertyChanged += OnMessageChanged; }
            Reset(); RefreshVisibility();
        }
    }
    public CompletionNotice()
    {
        _row.Add(_stamp, 0); Content = _row; IsVisible = false;
    }
    public CompletionNotice(Label message) : this() => Message = message;
    public void Complete(int committedCount = 1)
    {
        if (committedCount <= 0 || !IsVisible || _message?.IsVisible != true) return;
        // Repeated notifications of the same terminal render cannot restart a pass.
        if (_stamp.Acknowledged) return;
        try { _stamp.Acknowledge(_stamp.IsLoaded && IsOnVisiblePage()); }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine($"Completion feedback skipped: {error}"); }
    }
    public void Reset() => _stamp.ResetAcknowledgement();
    public void StopMotion() => _stamp.Stop();
    private bool IsOnVisiblePage()
    {
        for (Element? node = this; node is not null; node = node.Parent)
        {
            if (node is VisualElement { IsVisible: false }) return false;
            if (node is Page page) return page.IsLoaded && page.Window is not null;
        }
        return false;
    }
    private void OnMessageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(Label.Text) or nameof(IsVisible))) return;
        Reset(); RefreshVisibility();
    }
    private void RefreshVisibility() => IsVisible = _message is { IsVisible: true } label && !string.IsNullOrWhiteSpace(label.Text);

    private sealed class CompletionStamp : GuideMotionView
    {
        private readonly ActionIconView _icon = new() { Key = Icons8ActionCatalog.IsAvailable ? "Save" : "ApproveAction", IconSize = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start };
        public bool Acknowledged { get; private set; }
        protected override uint Duration => 160;
        public CompletionStamp()
        {
            WidthRequest = 24; HeightRequest = 20; VerticalOptions = LayoutOptions.Start;
            _icon.SetDynamicResource(ActionIconView.ColorProperty, "ContentSuccessTextColor");
            Children.Add(_icon); ResetFrame();
        }
        public void Acknowledge(bool animate) { Acknowledged = true; ResetFrame(); if (animate) RequestPlay(); }
        public void ResetAcknowledgement() { Acknowledged = false; Stop(); }
        protected override void Apply(double progress)
        {
            // Icons8 Motion ease-out cubic-bezier(.23,1,.32,1), not a bounce or loop.
            double x = Math.Clamp(progress, 0, 1), low = 0, high = 1;
            for (int i = 0; i < 24; i++)
            {
                double t = (low + high) / 2, u = 1 - t;
                if (3 * u * u * t * .23 + 3 * u * t * t * .32 + t * t * t < x) low = t; else high = t;
            }
            double eased = 1 - Math.Pow(1 - (low + high) / 2, 3);
            _icon.Opacity = eased; _icon.Scale = .96 + .04 * eased;
        }
        protected override void ResetFrame() { _icon.Opacity = Acknowledged ? 1 : 0; _icon.Scale = 1; }
    }
}
