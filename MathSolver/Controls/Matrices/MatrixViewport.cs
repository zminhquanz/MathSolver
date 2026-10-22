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
    private bool _dragged, _everLoaded, _extendSelectionGesture;
    private int _focusGeneration;
    private float _drawFontSize = 14;
    public Func<bool>? CommitEdit { get; set; }
    public Func<bool>? CanInteract { get; set; }
    public Func<bool>? CanEdit { get; set; }
    public event Action? EditCancelled;
    public event EventHandler? CopyRequested;
    public event EventHandler? PasteRequested;
    public bool AllSelected => _state.AllSelected;
    public bool HasRangeSelection => _state.HasRangeSelection;
    public MatrixCellRange Selection => _state.Selection;
    private void RequestCopy()
    {
        if (CanUseInput && !_state.IsEditing) CopyRequested?.Invoke(this, EventArgs.Empty);
    }
    private void RequestPaste()
    {
        if (CanUseInput && !_state.IsEditing && CanEdit?.Invoke() != false) PasteRequested?.Invoke(this, EventArgs.Empty);
    }
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
            if (!CanUseInput || HasPointerCapture || e.Touches.Length == 0) return;
            SetParentPanLock(_state.MaxX > 0 || _state.MaxY > 0);
            _panStart = e.Touches[0]; _panX = _state.OffsetX; _panY = _state.OffsetY; _dragged = false;
            _panBlocked = false;
            _extendSelectionGesture = IsRangeExtensionPressed();
            _scrollAxis = ScrollAxis.None;
            _resizeInteraction = StartResize(_panStart);
            _barInteraction = !_resizeInteraction && StartScrollBar(_panStart);
            if (_barInteraction || _resizeInteraction) _dragged = true;
        };
        _canvas.DragInteraction += (_, e) =>
        {
            if (!CanUseInput || HasPointerCapture || e.Touches.Length == 0) return;
            var point = e.Touches[0];
            if (_resizeInteraction) { DragResize(point); return; }
            if (_barInteraction) { DragScrollBar(point); return; }
            if (_panBlocked) return;
            if (_extendSelectionGesture)
            {
                if (_state.TryHitCell(point.X, point.Y, out int r, out int c))
                { _dragged = true; if (!ExtendSelection(r, c)) _panBlocked = true; }
                return;
            }
            if (Math.Abs(point.X - _panStart.X) + Math.Abs(point.Y - _panStart.Y) < 6) return;
            if (!_dragged && !_state.EndEdit(CommitEdit))
            { _dragged = _panBlocked = true; RestoreInvalidEditor(); return; }
            _dragged = true;
            if (_state.ScrollTo(_panX + _panStart.X - point.X, _panY + _panStart.Y - point.Y)) Scrolled();
            else Refresh();
        };
        _canvas.EndInteraction += (_, e) =>
        {
            // Captured Windows scrollbar drags are completed by the native
            // release/capture-lost events, including releases outside the canvas.
            if (HasPointerCapture) return;
            SetParentPanLock(false);
            _scrollAxis = ScrollAxis.None;
            _resizeAxis = MatrixResizeAxis.None;
            if (!CanUseInput) return;
            Refresh();
            if (_dragged) { if (!_state.IsEditing) FocusSelection(); return; }
            if (e.Touches.Length == 0) return;
            var point = e.Touches[0];
            if (point.X >= 0 && point.X < MatrixViewportState.HeaderWidth && point.Y >= 0 && point.Y < _state.CellHeight)
            { SelectAll(); return; }
            if (_state.TryHitCell(point.X, point.Y, out int row, out int column))
            {
                if (_extendSelectionGesture) ExtendSelection(row, column);
                else TapCell(row, column);
            }
        };
        _canvas.CancelInteraction += (_, _) =>
        {
            if (HasPointerCapture) return;
            _scrollAxis = ScrollAxis.None; _resizeAxis = MatrixResizeAxis.None; SetParentPanLock(false);
            if (CanUseInput) Refresh();
        };
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
        CancelPointerGesture();
        _state.SetSource(rows, columns); _readCell = readCell;
        Refresh(); ViewportChanged?.Invoke();
    }
    public bool Select(int row, int column)
    {
        if (!_state.Select(row, column, CommitEdit)) return RestoreInvalidEditor();
        Scrolled(); CellSelected?.Invoke(SelectedRow, SelectedColumn);
        FocusSelection();
        return true;
    }
    public bool SelectAll()
    {
        if (!CanUseInput) return false;
        if (!_state.SelectAll(CommitEdit)) return RestoreInvalidEditor();
        Refresh(); CellSelected?.Invoke(SelectedRow, SelectedColumn);
        FocusSelection();
        return true;
    }
    internal bool FinishEdit()
    {
        if (!CanUseInput) return false;
        if (!_state.EndEdit(CommitEdit)) return RestoreInvalidEditor();
        Refresh();
        return true;
    }
    public bool ExtendSelection(int row, int column)
    {
        if (!CanUseInput) return false;
        if (!_state.ExtendSelection(row, column, CommitEdit)) return RestoreInvalidEditor();
        Scrolled(); CellSelected?.Invoke(SelectedRow, SelectedColumn); FocusSelection();
        return true;
    }
    public bool SelectRange(int row, int column, int rows, int columns)
    {
        if (!CanUseInput || row < 0 || column < 0 || rows < 1 || columns < 1
            || row > _state.Rows - rows || column > _state.Columns - columns) return false;
        if (!_state.Select(row, column, CommitEdit)) return RestoreInvalidEditor();
        _state.ExtendSelection(row + rows - 1, column + columns - 1);
        _state.RevealSelection();
        Scrolled(); CellSelected?.Invoke(SelectedRow, SelectedColumn); FocusSelection();
        return true;
    }
    internal bool Navigate(MatrixCellMove move)
    {
        if (!_state.Navigate(move, CommitEdit)) return RestoreInvalidEditor();
        Scrolled(); CellSelected?.Invoke(SelectedRow, SelectedColumn);
        FocusSelection();
        return true;
    }

    private void TapCell(int row, int column)
    {
        var action = _state.TapCell(row, column, CanEdit?.Invoke() != false, CommitEdit);
        if (action == MatrixCellAction.Rejected) { RestoreInvalidEditor(); return; }
        if (action == MatrixCellAction.Selected) CellSelected?.Invoke(SelectedRow, SelectedColumn);
        Scrolled();
        if (action == MatrixCellAction.Editing) FocusEditor();
        else FocusSelection();
    }

    private void BeginInlineEdit()
    {
        bool wasAllSelected = _state.HasRangeSelection;
        if (!CanUseInput || !_state.BeginEdit(CanEdit?.Invoke() != false)) return;
        if (wasAllSelected) CellSelected?.Invoke(SelectedRow, SelectedColumn);
        Scrolled(); FocusEditor();
    }

    private bool RestoreInvalidEditor()
    {
        // Reveal an unfinished edit without overwriting its text or committing again.
        _state.RevealSelection();
        Scrolled(); FocusEditor(false);
        return false;
    }

    private void CancelInlineEdit()
    {
        if (!CanUseInput) return;
        _state.CancelEdit();
        EditCancelled?.Invoke();
        Refresh(); FocusSelection();
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
        double width = Math.Max(1, _canvas.Width), height = Math.Max(1, _canvas.Height);
        double cellWidth = Math.Min(MatrixViewportState.DefaultColumnWidth * scale,
            Math.Max(MatrixViewportState.MinColumnWidth, width - MatrixViewportState.HeaderWidth - ScrollBarThickness));
        _state.Arrange(width, height, cellWidth, MatrixViewportState.DefaultRowHeight * scale, ScrollBarThickness);
        // Small matrices occupy only the space they need; larger ones keep a bounded viewport.
        double desiredHeight = Math.Min(340, _state.CellHeight + _state.ContentHeight
            + (_state.ShowHorizontalScrollBar ? ScrollBarThickness : 0));
        if (Math.Abs(_surface.HeightRequest - desiredHeight) > 0.01) _surface.HeightRequest = desiredHeight;
        PositionEditor(); _canvas.Invalidate();
    }
    private void Scrolled() { Refresh(); ViewportChanged?.Invoke(); }
    private void PositionEditor()
    {
        if (_editorHost is null) return;
        var bounds = _state.Bounds(SelectedRow, SelectedColumn);
        // An intentionally oversized cell can still be edited in the visible
        // part of the body, without placing the Entry over headers or rails.
        double left = Math.Max(MatrixViewportState.HeaderWidth, bounds.X), top = Math.Max(_state.CellHeight, bounds.Y);
        double right = Math.Min(_state.Width, bounds.X + bounds.Width), bottom = Math.Min(_state.Height, bounds.Y + bounds.Height);
        bool visible = _state.IsEditing && CanEdit?.Invoke() != false && _readCell is not null && right - left >= 24 && bottom - top >= 24;
        _editorHost.IsVisible = visible;
        if (visible) AbsoluteLayout.SetLayoutBounds(_editorHost, new Rect(left, top, right - left, bottom - top));
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Paper; canvas.FillRectangle(dirtyRect);
        if (_readCell is null) return;
        _drawFontSize = (float)(14 * Math.Clamp(ResponsiveLayoutPolicy.TextScale, 1, 1.6));
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = _drawFontSize;
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
                bool active = row == SelectedRow && column == SelectedColumn;
                DrawCell(canvas, bounds, _readCell(row, column), _state.IsSelected(row, column), false, active);
            }
        }
        canvas.RestoreState();
        canvas.SaveState();
        canvas.ClipRectangle(headerWidth, 0, Math.Max(0, width - headerWidth), cellHeight);
        for (int column = FirstColumn; column < _state.Columns; column++)
        {
            var bounds = _state.Bounds(FirstRow, column);
            if (bounds.X >= width) break;
            DrawCell(canvas, new(bounds.X, 0, bounds.Width, cellHeight), (column + 1).ToString(), _state.AllSelected, true, false);
            DrawColumnGrip(canvas, bounds.X + bounds.Width);
        }
        canvas.RestoreState();
        canvas.SaveState();
        canvas.ClipRectangle(0, cellHeight, headerWidth, Math.Max(0, height - cellHeight));
        for (int row = FirstRow; row < _state.Rows; row++)
        {
            var bounds = _state.Bounds(row, FirstColumn);
            if (bounds.Y >= height) break;
            DrawCell(canvas, new(0, bounds.Y, headerWidth, bounds.Height), (row + 1).ToString(), _state.AllSelected, true, false);
            DrawRowGrip(canvas, bounds.Y + bounds.Height);
        }
        canvas.RestoreState();
        DrawCell(canvas, new(0, 0, headerWidth, cellHeight), "#", _state.AllSelected, true, false);
        DrawResizeGuide(canvas);
        DrawScrollBars(canvas);
    }
    private void DrawCell(ICanvas canvas, MatrixCellBounds bounds, string text, bool selected, bool header, bool outline = true)
    {
        float x = (float)bounds.X, y = (float)bounds.Y, width = (float)bounds.Width, height = (float)bounds.Height;
        canvas.FillColor = selected ? Accent.WithAlpha(0.18f) : header ? Line.WithAlpha(0.22f) : Paper;
        canvas.FillRectangle(x, y, width, height);
        text = FitCellText(canvas, text.Length == 0 ? "·" : text, Math.Max(0, width - 12), _drawFontSize);
        canvas.FontColor = selected ? Accent : Ink;
        canvas.DrawString(text, x + 6, y, width - 12, height, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawRectangle(x, y, width, height);
        if (selected && outline)
        {
            canvas.StrokeColor = Accent; canvas.StrokeSize = 2;
            canvas.DrawRectangle(x + 1, y + 1, width - 2, height - 2);
            canvas.StrokeColor = Line; canvas.StrokeSize = 1;
        }
    }

    private static string FitCellText(ICanvas canvas, string text, double width, float fontSize)
    {
        if (canvas.GetStringSize(text, Microsoft.Maui.Graphics.Font.Default, fontSize).Width <= width) return text;
        int low = 0, high = text.Length;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (canvas.GetStringSize(text[..middle] + "…", Microsoft.Maui.Graphics.Font.Default, fontSize).Width <= width) low = middle;
            else high = middle - 1;
        }
        return text[..low] + "…";
    }
}

