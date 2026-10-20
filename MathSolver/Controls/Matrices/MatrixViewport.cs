using MathSolver.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

/// <summary>Bounded canvas with one reusable inline editor and pixel scrolling on both axes.</summary>
public sealed partial class MatrixViewport : ContentView, IDrawable
{
    private readonly MatrixViewportState _state = new();
    private readonly GraphicsView _canvas = new() { HorizontalOptions = LayoutOptions.Fill };
    private readonly AbsoluteLayout _surface = new() { HeightRequest = 340, HorizontalOptions = LayoutOptions.Fill };
    private Entry? _editor;
    private Border? _editorHost;
    private Func<int, int, string>? _readCell;
    private PointF _panStart;
    private double _panX, _panY;
    private bool _dragged, _everLoaded;
    private int _focusGeneration;
    public Func<bool>? CommitEdit { get; set; }
    public Func<bool>? CanInteract { get; set; }
    public event Action? EditCancelled;
    public int SelectedRow => _state.SelectedRow;
    public int SelectedColumn => _state.SelectedColumn;
    public int FirstRow => _state.FirstRow;
    public int FirstColumn => _state.FirstColumn;
    public event Action<int, int>? CellSelected;
    public event Action? ViewportChanged;
    private bool CanUseInput => IsLoaded && Handler?.MauiContext is not null
        && Window is { Handler.MauiContext: not null } window && !WindowUiLifetime.IsClosing(window)
        && CanInteract?.Invoke() != false;

    public static readonly BindableProperty InkProperty = PaintProperty(nameof(Ink), Colors.Black);
    public static readonly BindableProperty PaperProperty = PaintProperty(nameof(Paper), Colors.White);
    public static readonly BindableProperty LineProperty = PaintProperty(nameof(Line), Colors.Gray);
    public static readonly BindableProperty AccentProperty = PaintProperty(nameof(Accent), Colors.Orange);
    public Color Ink { get => (Color)GetValue(InkProperty); set => SetValue(InkProperty, value); }
    public Color Paper { get => (Color)GetValue(PaperProperty); set => SetValue(PaperProperty, value); }
    public Color Line { get => (Color)GetValue(LineProperty); set => SetValue(LineProperty, value); }
    public Color Accent { get => (Color)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    private static BindableProperty PaintProperty(string name, Color fallback) => BindableProperty.Create(
        name, typeof(Color), typeof(MatrixViewport), fallback,
        propertyChanged: (view, _, _) => ((MatrixViewport)view).Refresh());

    public MatrixViewport()
    {
        SetDynamicResource(InkProperty, "WallpaperTextPrimaryColor");
        SetDynamicResource(PaperProperty, "WallpaperSurfaceColor");
        SetDynamicResource(LineProperty, "WallpaperDividerColor");
        SetDynamicResource(AccentProperty, "PrimaryColor");
        _canvas.Drawable = this;
        _surface.Add(_canvas);
        AbsoluteLayout.SetLayoutFlags(_canvas, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_canvas, new Rect(0, 0, 1, 1));
        Content = _surface;
        _canvas.SizeChanged += (_, _) =>
        {
            if (_everLoaded && !CanUseInput) return;
            Refresh(); ViewportChanged?.Invoke();
        };
        _canvas.StartInteraction += (_, e) =>
        {
            if (!CanUseInput || e.Touches.Length == 0) return;
            SetParentPanLock(_state.MaxX > 0 || _state.MaxY > 0);
            _panStart = e.Touches[0]; _panX = _state.OffsetX; _panY = _state.OffsetY; _dragged = false;
        };
        _canvas.DragInteraction += (_, e) =>
        {
            if (!CanUseInput || e.Touches.Length == 0) return;
            var point = e.Touches[0];
            if (Math.Abs(point.X - _panStart.X) + Math.Abs(point.Y - _panStart.Y) < 6) return;
            _dragged = true;
            if (_state.ScrollTo(_panX + _panStart.X - point.X, _panY + _panStart.Y - point.Y)) Scrolled();
        };
        _canvas.EndInteraction += (_, e) =>
        {
            SetParentPanLock(false);
            if (!CanUseInput || _dragged || e.Touches.Length == 0) return;
            var point = e.Touches[0];
            if (_state.TryHitCell(point.X, point.Y, out int row, out int column)) Select(row, column);
        };
        _canvas.CancelInteraction += (_, _) => SetParentPanLock(false);
        Loaded += (_, _) => { _everLoaded = true; AttachNativeInput(); Refresh(); };
        Unloaded += (_, _) => { _focusGeneration++; SetParentPanLock(false); DetachNativeInput(); };
        HandlerChanging += (_, _) => { _focusGeneration++; DetachNativeInput(); };
        HandlerChanged += (_, _) => { if (IsLoaded) AttachNativeInput(); };
    }

    public void SetInlineEditor(Entry editor)
    {
        if (_editor is not null) throw new InvalidOperationException("The matrix already has an editor.");
        _editor = editor;
        editor.MinimumHeightRequest = 0;
        _editorHost = MatrixInputField.Wrap(editor);
        _editorHost.Padding = new Thickness(4, 0);
        _editorHost.StrokeThickness = 2;
        _editorHost.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) };
        _editorHost.SetDynamicResource(Border.StrokeProperty, "PrimaryColor");
        _editorHost.IsVisible = false;
        _surface.Add(_editorHost);
        editor.Focused += (_, _) => FocusEditor();
    }
    public void SetAccessibility(string table) => SemanticProperties.SetDescription(_canvas, table);

    public void SetSource(int rows, int columns, Func<int, int, string> readCell)
    {
        _focusGeneration++;
        _state.SetSource(rows, columns); _readCell = readCell;
        Refresh(); ViewportChanged?.Invoke();
    }
    public bool Select(int row, int column)
    {
        if (!_state.Select(row, column, CommitEdit)) return RestoreInvalidEditor();
        Scrolled(); CellSelected?.Invoke(SelectedRow, SelectedColumn);
        return true;
    }
    internal bool Navigate(MatrixCellMove move)
    {
        if (!_state.Navigate(move, CommitEdit)) return RestoreInvalidEditor();
        Scrolled(); CellSelected?.Invoke(SelectedRow, SelectedColumn);
        return true;
    }

    private bool RestoreInvalidEditor()
    {
        // A wheel/drag may have hidden an unfinished edit; reveal it without
        // overwriting its text or attempting the rejected commit a second time.
        _state.Select(SelectedRow, SelectedColumn);
        Scrolled(); FocusEditor(false);
        return false;
    }

    private void CancelInlineEdit()
    {
        if (!CanUseInput) return;
        EditCancelled?.Invoke();
        FocusEditor();
    }

    public void FocusEditor(bool selectAll = true)
    {
        if (!CanUseInput || _editor is null || _editorHost?.IsVisible != true) return;
        int generation = ++_focusGeneration;
        Dispatcher.Dispatch(() =>
        {
            if (generation != _focusGeneration || !CanUseInput || _editorHost?.IsVisible != true || !_editor.IsEnabled) return;
            _editor.Focus();
            if (selectAll) { _editor.CursorPosition = 0; _editor.SelectionLength = _editor.Text?.Length ?? 0; }
        });
    }

    public void Refresh()
    {
        if ((_everLoaded && !CanUseInput) || WindowUiLifetime.IsClosing(Window)) return;
        double scale = Math.Clamp(ResponsiveLayoutPolicy.TextScale, 1, 1.6);
        // Keep a complete editable cell available on narrow, text-scaled screens.
        double cellWidth = Math.Min(184 * scale, Math.Max(80, _canvas.Width - MatrixViewportState.HeaderWidth));
        _state.Resize(_canvas.Width, _canvas.Height, cellWidth, 52 * scale);
        PositionEditor(); _canvas.Invalidate();
    }
    private void Scrolled() { Refresh(); ViewportChanged?.Invoke(); }
    private void PositionEditor()
    {
        if (_editorHost is null) return;
        var bounds = _state.Bounds(SelectedRow, SelectedColumn);
        // Hide off-screen editors instead of moving focus into another page section.
        bool visible = _readCell is not null && bounds.X >= MatrixViewportState.HeaderWidth - 0.1
            && bounds.Y >= _state.CellHeight - 0.1 && bounds.X + bounds.Width <= _state.Width + 0.1
            && bounds.Y + bounds.Height <= _state.Height + 0.1;
        _editorHost.IsVisible = visible;
        if (visible) AbsoluteLayout.SetLayoutBounds(_editorHost, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Paper; canvas.FillRectangle(dirtyRect);
        if (_readCell is null) return;
        canvas.FontSize = (float)(14 * Math.Clamp(ResponsiveLayoutPolicy.TextScale, 1, 1.6));
        canvas.StrokeColor = Line; canvas.StrokeSize = 1;
        float width = (float)_state.Width, height = (float)_state.Height;
        float headerWidth = (float)MatrixViewportState.HeaderWidth, cellHeight = (float)_state.CellHeight;
        canvas.SaveState();
        canvas.ClipRectangle(headerWidth, cellHeight, Math.Max(0, width - headerWidth), Math.Max(0, height - cellHeight));
        for (int row = FirstRow; row < _state.Rows; row++)
        {
            var rowBounds = _state.Bounds(row, FirstColumn);
            if (rowBounds.Y >= height) break;
            for (int column = FirstColumn; column < _state.Columns; column++)
            {
                var bounds = _state.Bounds(row, column);
                if (bounds.X >= width) break;
                DrawCell(canvas, bounds, _readCell(row, column), row == SelectedRow && column == SelectedColumn, false);
            }
        }
        canvas.RestoreState();
        canvas.SaveState();
        canvas.ClipRectangle(headerWidth, 0, Math.Max(0, width - headerWidth), cellHeight);
        for (int column = FirstColumn; column < _state.Columns; column++)
        {
            var bounds = _state.Bounds(FirstRow, column);
            if (bounds.X >= width) break;
            DrawCell(canvas, new(bounds.X, 0, bounds.Width, cellHeight), (column + 1).ToString(), false, true);
        }
        canvas.RestoreState();
        canvas.SaveState();
        canvas.ClipRectangle(0, cellHeight, headerWidth, Math.Max(0, height - cellHeight));
        for (int row = FirstRow; row < _state.Rows; row++)
        {
            var bounds = _state.Bounds(row, FirstColumn);
            if (bounds.Y >= height) break;
            DrawCell(canvas, new(0, bounds.Y, headerWidth, cellHeight), (row + 1).ToString(), false, true);
        }
        canvas.RestoreState();
        DrawCell(canvas, new(0, 0, headerWidth, cellHeight), "#", false, true);
    }
    private void DrawCell(ICanvas canvas, MatrixCellBounds bounds, string text, bool selected, bool header)
    {
        float x = (float)bounds.X, y = (float)bounds.Y, width = (float)bounds.Width, height = (float)bounds.Height;
        canvas.FillColor = selected ? Accent.WithAlpha(0.18f) : header ? Line.WithAlpha(0.22f) : Paper;
        canvas.FillRectangle(x, y, width, height);
        if (text.Length > 20) text = text[..17] + "…";
        canvas.FontColor = selected ? Accent : Ink;
        canvas.DrawString(text.Length == 0 ? "·" : text, x + 6, y, width - 12, height, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawRectangle(x, y, width, height);
    }
}

