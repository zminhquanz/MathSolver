using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

/// <summary>One scroll owner, fitted vector content, and a frozen, answer-safe description.</summary>
internal sealed class DiagramPreviewPage : ContentPage
{
    private readonly IDrawable _source;
    private readonly double _sourceWidth, _sourceHeight;
    private readonly Grid _root, _body, _canvas;
    private readonly Border _panel;
    private readonly GraphicsView _image;
    private readonly ScrollView _viewport, _details;
    private readonly Button _close, _minus, _plus, _percentage, _info;
    private readonly Label _help;
    private double _zoom = 1;
    private DiagramPreviewViewport _geometry;
    private bool _visible, _closing, _resizing;
    private int _layoutVersion;
    private bool _scrollPending;
    private double _targetX, _targetY;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _keys, _pointerSurface;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _keyHandler;
    private Microsoft.UI.Xaml.Input.PointerEventHandler? _wheelHandler;
    private global::Windows.Foundation.Point? _dragStart;
    private double _dragX, _dragY;
#endif

    internal DiagramPreviewPage(IDrawable source, double width, double height,
        string description, View? details)
    {
        _source = source;
        _details = new ScrollView { Content = details, IsVisible = false, MaximumHeightRequest = 160 };
        _sourceWidth = width > 0 ? width : 360;
        _sourceHeight = height > 0 ? height : 260;
        Title = Text("Preview.Title");
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;
        Shell.SetNavBarIsVisible(this, false);
        SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
        var title = new Label { Text = Title, FontSize = 20, FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        _close = CreateButton("×", IllustratedQuizPicker.Text("Choice.Close"));
        _close.FontSize = 24;
        _close.FontAutoScalingEnabled = false;
        _close.WidthRequest = 48;
        _close.Clicked += async (_, _) => await CloseAsync();
        var header = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        header.Add(title);
        header.Add(_close, 1);

        _minus = CreateButton("−", Text("Preview.ZoomOut"));
        _plus = CreateButton("+", Text("Preview.ZoomIn"));
        _minus.FontSize = _plus.FontSize = 22;
        _minus.FontAutoScalingEnabled = _plus.FontAutoScalingEnabled = false;
        _percentage = CreateButton("100%", Text("Preview.Fit"));
        _percentage.SetDynamicResource(Button.TextColorProperty, "PrimaryColor");
        _percentage.SetDynamicResource(Button.BackgroundColorProperty, "PrimarySoftColor");
        _minus.Clicked += (_, _) => SetZoom(_zoom / 1.25);
        _plus.Clicked += (_, _) => SetZoom(_zoom * 1.25);
        _percentage.Clicked += (_, _) => SetZoom(1);
        var zoom = new Grid { ColumnDefinitions = { new(new GridLength(48)), new(GridLength.Star), new(new GridLength(48)) },
            ColumnSpacing = 6, MaximumWidthRequest = 240 };
        zoom.Add(_minus);
        zoom.Add(_percentage, 1);
        zoom.Add(_plus, 2);
        _info = CreateButton(Text("Preview.Details"), Text("Preview.Details"));
        _info.IsVisible = details is not null;
        _info.Clicked += (_, _) =>
        {
            _details.IsVisible = !_details.IsVisible;
            _info.Text = Text(_details.IsVisible ? "Preview.HideDetails" : "Preview.Details");
            SemanticProperties.SetDescription(_info, _info.Text);
        };
        var tools = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        zoom.HorizontalOptions = LayoutOptions.Start;
        tools.Add(zoom);
        tools.Add(_info, 1);
        // At enlarged text sizes the controls remain reachable through horizontal
        // scrolling, rather than forcing the diagram out of a phone viewport.
        var toolbar = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = tools,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default };
        toolbar.SizeChanged += (_, _) => tools.MinimumWidthRequest = Math.Max(0, toolbar.Width);
        _help = new Label { Text = Text(DeviceInfo.Idiom == DeviceIdiom.Desktop ? "Preview.HelpDesktop" : "Preview.HelpTouch"),
            FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };
        _help.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");

        _image = new GraphicsView();
        SemanticProperties.SetDescription(_image, description);
        SemanticProperties.SetHint(_image, _help.Text);
        AutomationProperties.SetIsInAccessibleTree(_image, true);
        _canvas = new Grid();
        _image.HorizontalOptions = _image.VerticalOptions = LayoutOptions.Center;
        _canvas.Add(_image);
        _viewport = new ScrollView { Orientation = ScrollOrientation.Both, Content = _canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default, VerticalScrollBarVisibility = ScrollBarVisibility.Default };
        _viewport.SizeChanged += (_, _) => UpdateCanvas();
        var plot = new Border { Content = _viewport, StrokeThickness = 1, Padding = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 } };
        plot.SetDynamicResource(BackgroundColorProperty, "SurfaceAltColor");
        plot.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += (_, args) =>
        {
            if (!_visible) return;
            if (args.Status == GestureStatus.Running)
            {
                double x = (_geometry.Width - _geometry.ImageWidth) / 2 + args.ScaleOrigin.X * _geometry.ImageWidth - _viewport.ScrollX;
                double y = (_geometry.Height - _geometry.ImageHeight) / 2 + args.ScaleOrigin.Y * _geometry.ImageHeight - _viewport.ScrollY;
                SetZoom(_zoom * args.Scale, x, y);
            }
        };
        _image.GestureRecognizers.Add(pinch);
        _body = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star),
            new(GridLength.Auto), new(GridLength.Auto) }, RowSpacing = 8 };
        _body.Add(header);
        _body.Add(toolbar, 0, 1);
        _body.Add(plot, 0, 2);
        _body.Add(_help, 0, 3);
        _body.Add(_details, 0, 4);
        _panel = new Border { Content = _body, StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _panel.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        _panel.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        _root = new Grid { SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.None };
        _root.Add(_panel);
        _root.SizeChanged += (_, _) => Resize();
        Content = _root;
        Unloaded += (_, _) => EndLifetime();
#if WINDOWS
        _image.Loaded += (_, _) => AttachWindowsInput();
        _root.Loaded += (_, _) => AttachWindowsInput();
#endif
    }

    private static string Text(string key) => QuizContentCatalog.Text(AppLanguageManager.CurrentLanguage, key);
    private static Button CreateButton(string text, string description)
    {
        var button = new Button { Text = text, MinimumHeightRequest = 48, MinimumWidthRequest = 48,
            Padding = new Thickness(10, 6), CornerRadius = 10, FontSize = 14,
            FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.NoWrap };
        InteractiveButtonAnimation.SetIsPressAnimationEnabled(button, false);
        button.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceAltColor");
        button.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
        SemanticProperties.SetDescription(button, description);
        ToolTipProperties.SetText(button, description);
        return button;
    }

    private void Resize()
    {
        if (!_visible || _closing || _resizing || _root.Width <= 0 || _root.Height <= 0) return;
        _resizing = true;
        try
        {
            var layout = DiagramPreviewViewport.Panel(_root.Width, _root.Height);
            _panel.WidthRequest = layout.Width;
            _panel.HeightRequest = layout.Height;
            _panel.Padding = layout.Padding;
            _help.IsVisible = !layout.Short;
            _details.MaximumHeightRequest = Math.Max(48, Math.Min(160, _root.Height * .22));
            if (_root.Height < 320)
            {
                _details.IsVisible = false;
                _info.Text = Text("Preview.Details");
                SemanticProperties.SetDescription(_info, _info.Text);
            }
            _info.IsVisible = _details.Content is not null && _root.Height >= 320;
        }
        finally { _resizing = false; }
    }

    private void SetZoom(double zoom, double? x = null, double? y = null)
    {
        if (!_visible || _closing) return;
        _zoom = DiagramPreviewViewport.ClampZoom(zoom);
        UpdateCanvas(x, y);
    }

    private void UpdateCanvas(double? x = null, double? y = null)
    {
        if (!_visible || _closing || _viewport.Width <= 0 || _viewport.Height <= 0) return;
        var previous = _geometry;
        var geometry = DiagramPreviewViewport.Fit(_viewport.Width, _viewport.Height, _sourceWidth, _sourceHeight, _zoom);
        _geometry = geometry;
        _canvas.WidthRequest = geometry.Width;
        _canvas.HeightRequest = geometry.Height;
        _image.WidthRequest = geometry.ImageWidth;
        _image.HeightRequest = geometry.ImageHeight;
        _image.Drawable = new ScaledDiagram(_source, (float)geometry.Scale);
        _image.Invalidate();
        _percentage.Text = $"{Math.Round(_zoom * 100)}%";
        SemanticProperties.SetDescription(_percentage, _percentage.Text + ", " + Text("Preview.Fit"));
        _minus.IsEnabled = _zoom > 1.001;
        _plus.IsEnabled = _zoom < DiagramPreviewViewport.MaximumZoom - .001;
        int version = ++_layoutVersion;
        double scrollX = previous.Scale > 0 ? DiagramPreviewViewport.AnchorOffset(_scrollPending ? _targetX : _viewport.ScrollX, x ?? previous.ViewportWidth / 2, previous.Width,
            previous.ImageWidth, geometry.Width, geometry.ImageWidth, _viewport.Width, x ?? _viewport.Width / 2) : 0;
        double scrollY = previous.Scale > 0 ? DiagramPreviewViewport.AnchorOffset(_scrollPending ? _targetY : _viewport.ScrollY, y ?? previous.ViewportHeight / 2, previous.Height,
            previous.ImageHeight, geometry.Height, geometry.ImageHeight, _viewport.Height, y ?? _viewport.Height / 2) : 0;
        _scrollPending = true;
        _targetX = scrollX;
        _targetY = scrollY;
        // Native scroll extents update on the next layout pass. Coalesce fast wheel/pinch events.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(32), async () =>
        {
            if (!_visible || _closing || version != _layoutVersion || _viewport.Handler?.PlatformView is null) return;
            await _viewport.ScrollToAsync(scrollX, scrollY, false);
            if (version == _layoutVersion) _scrollPending = false;
        });
    }

    private async Task CloseAsync()
    {
        if (_closing || !_visible) return;
        _closing = true;
        try { await Navigation.PopModalAsync(); }
        finally { _closing = false; }
    }

    protected override bool OnBackButtonPressed() { _ = CloseAsync(); return true; }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        if (Application.Current is { } app) app.RequestedThemeChanged += OnThemeChanged;
        Resize();
        UpdateCanvas();
#if WINDOWS
        AttachWindowsInput();
        Dispatcher.Dispatch(() => { if (_visible && _close.Handler?.PlatformView is not null) _close.Focus(); });
#endif
    }

    protected override void OnDisappearing() { EndLifetime(); base.OnDisappearing(); }
    private void EndLifetime()
    {
        _visible = false;
        if (Application.Current is { } app) app.RequestedThemeChanged -= OnThemeChanged;
        _layoutVersion++;
        _scrollPending = false;
#if WINDOWS
        try
        {
            if (_keyHandler is not null) _keys?.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyHandler);
            if (_wheelHandler is not null) _pointerSurface?.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, _wheelHandler);
            if (_pointerSurface is not null)
            {
                _pointerSurface.PointerPressed -= OnPointerPressed;
                _pointerSurface.PointerMoved -= OnPointerMoved;
                _pointerSurface.PointerReleased -= OnPointerReleased;
                _pointerSurface.PointerCaptureLost -= OnPointerReleased;
                _pointerSurface.ReleasePointerCaptures();
            }
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine($"Diagram preview surface already disconnected: {error.Message}"); }
        _keys = _pointerSurface = null;
        _keyHandler = null;
        _wheelHandler = null;
        _dragStart = null;
#endif
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_visible && _image.Handler?.PlatformView is not null) _image.Invalidate();
    }

#if WINDOWS
    private void AttachWindowsInput()
    {
        if (!_visible || _closing) return;
        if (_keys is null && Content.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement keys)
        {
            _keys = keys;
            _keyHandler = OnKeyDown;
            _keys.AddHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, _keyHandler, true);
        }
        if (_pointerSurface is null && _image.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement image)
        {
            _pointerSurface = image;
            _wheelHandler = OnWheel;
            image.AddHandler(Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, _wheelHandler, true);
            image.PointerPressed += OnPointerPressed;
            image.PointerMoved += OnPointerMoved;
            image.PointerReleased += OnPointerReleased;
            image.PointerCaptureLost += OnPointerReleased;
        }
    }

    private async void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!_visible || _closing) return;
        switch (e.Key)
        {
            case global::Windows.System.VirtualKey.Escape: e.Handled = true; await CloseAsync(); break;
            case global::Windows.System.VirtualKey.Add: e.Handled = true; SetZoom(_zoom * 1.25); break;
            case (global::Windows.System.VirtualKey)187: e.Handled = true; SetZoom(_zoom * 1.25); break;
            case global::Windows.System.VirtualKey.Subtract: e.Handled = true; SetZoom(_zoom / 1.25); break;
            case (global::Windows.System.VirtualKey)189: e.Handled = true; SetZoom(_zoom / 1.25); break;
            case global::Windows.System.VirtualKey.Number0:
            case global::Windows.System.VirtualKey.NumberPad0: e.Handled = true; SetZoom(1); break;
        }
    }

    private void OnWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_visible || _closing || !e.KeyModifiers.HasFlag(global::Windows.System.VirtualKeyModifiers.Control)) return;
        var point = e.GetCurrentPoint(_viewport.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement);
        SetZoom(_zoom * Math.Pow(1.25, point.Properties.MouseWheelDelta / 120d), point.Position.X, point.Position.Y);
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_visible || _closing || _zoom <= 1 || e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;
        var point = e.GetCurrentPoint(_viewport.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement);
        if (!point.Properties.IsLeftButtonPressed || _pointerSurface?.CapturePointer(e.Pointer) != true) return;
        _dragStart = point.Position;
        _dragX = _viewport.ScrollX;
        _dragY = _viewport.ScrollY;
        e.Handled = true;
    }

    private async void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_dragStart is not { } start || !_visible || _closing) return;
        var point = e.GetCurrentPoint(_viewport.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement).Position;
        e.Handled = true;
        await _viewport.ScrollToAsync(Math.Clamp(_dragX - (point.X - start.X), 0, Math.Max(0, _geometry.Width - _viewport.Width)),
            Math.Clamp(_dragY - (point.Y - start.Y), 0, Math.Max(0, _geometry.Height - _viewport.Height)), false);
    }

    private void OnPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_dragStart is null) return;
        _dragStart = null;
        _pointerSurface?.ReleasePointerCapture(e.Pointer);
    }
#endif

    private sealed class ScaledDiagram(IDrawable source, float scale) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            try
            {
                canvas.Scale(scale, scale);
                source.Draw(canvas, new RectF(0, 0, dirtyRect.Width / scale, dirtyRect.Height / scale));
            }
            finally { canvas.RestoreState(); }
        }
    }
}
