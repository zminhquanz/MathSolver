using MathSolver.Services;

namespace MathSolver.Controls;

public sealed partial class MatrixViewport
{
#if ANDROID
    private int _holdGeneration, _contextRow, _contextColumn;
    private bool _holdMenuOpened;
    private global::Android.Views.ActionMode? _selectionActionMode;
    private global::Android.Widget.PopupMenu? _selectionPopup;
    private SelectionActionMode? _selectionCallback;
    private bool SelectionHoldOpened => _holdMenuOpened;

    private void CancelSelectionHold() => _holdGeneration++;

    private void BeginSelectionHold(PointF point)
    {
        if (_readCell is null || _state.IsRangeSelectionMode
            || !_state.TryHitCell(point.X, point.Y, out int row, out int column)) return;
        int generation = ++_holdGeneration;
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(global::Android.Views.ViewConfiguration.LongPressTimeout), () =>
        {
            if (generation != _holdGeneration || !CanUseInput || _dragged || _readCell is null || _state.IsRangeSelectionMode) return;
            // Holding inside a selected range keeps the range for Copy. Holding
            // elsewhere selects that cell without invoking the second-tap editor.
            if (_state.IsSelected(row, column)) { if (!FinishEdit()) return; }
            else if (!Select(row, column)) return;
            _contextRow = row; _contextColumn = column;
            _dragged = _holdMenuOpened = true; // Consume Up: it must not enter editing.
            SetParentPanLock(false);
            if (_canvas.Handler?.PlatformView is not global::Android.Views.View native) return;
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                _selectionCallback = new SelectionActionMode(this);
                _selectionActionMode = native.StartActionMode(_selectionCallback, global::Android.Views.ActionModeType.Floating);
            }
            if (_selectionActionMode is null)
            {
                _selectionCallback = null;
                var popup = _selectionPopup = new global::Android.Widget.PopupMenu(native.Context!, native);
                AddSelectionMenuItems(popup.Menu!);
                popup.MenuItemClick += (_, e) =>
                {
                    if (!CanUseInput) return;
                    if (e.Item?.ItemId == global::Android.Resource.Id.Copy) RequestCopy();
                    else if (e.Item?.ItemId == global::Android.Resource.Id.SelectAll) SelectAll();
                };
                popup.DismissEvent += (_, _) =>
                { if (ReferenceEquals(_selectionPopup, popup)) { _selectionPopup = null; _holdMenuOpened = false; } };
                popup.Show();
            }
        });
    }

    private static void AddSelectionMenuItems(global::Android.Views.IMenu menu)
    {
        using var copy = new Java.Lang.String(LocalizationService.TranslateKey("Matrix.Workspace.Copy"));
        using var all = new Java.Lang.String(LocalizationService.TranslateKey("Matrix.SelectAll"));
        menu.Add(0, global::Android.Resource.Id.Copy, 0, copy);
        menu.Add(0, global::Android.Resource.Id.SelectAll, 1, all);
    }

    private void CloseSelectionMenu()
    {
        CancelSelectionHold();
        _holdMenuOpened = false;
        var mode = _selectionActionMode; _selectionActionMode = null;
        mode?.Finish();
        _selectionCallback = null;
        var popup = _selectionPopup; _selectionPopup = null;
        popup?.Dismiss();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android23.0")]
    private sealed class SelectionActionMode(MatrixViewport owner) : global::Android.Views.ActionMode.Callback2
    {
        public override bool OnCreateActionMode(global::Android.Views.ActionMode? mode, global::Android.Views.IMenu? menu)
        {
            if (menu is null || !owner.CanUseInput) return false;
            AddSelectionMenuItems(menu);
            return true;
        }
        public override bool OnPrepareActionMode(global::Android.Views.ActionMode? mode, global::Android.Views.IMenu? menu) => false;
        public override bool OnActionItemClicked(global::Android.Views.ActionMode? mode, global::Android.Views.IMenuItem? item)
        {
            if (!owner.CanUseInput) { mode?.Finish(); return false; }
            if (item?.ItemId == global::Android.Resource.Id.Copy)
            { mode?.Finish(); owner.RequestCopy(); return true; }
            if (item?.ItemId == global::Android.Resource.Id.SelectAll)
            { owner.SelectAll(); mode?.InvalidateContentRect(); return true; }
            return false;
        }
        public override void OnDestroyActionMode(global::Android.Views.ActionMode? mode)
        { owner._selectionActionMode = null; owner._selectionCallback = null; owner._holdMenuOpened = false; }
        public override void OnGetContentRect(global::Android.Views.ActionMode? mode, global::Android.Views.View? view, global::Android.Graphics.Rect? outRect)
        {
            var cell = owner._state.Bounds(owner._contextRow, owner._contextColumn);
            double density = view?.Resources?.DisplayMetrics?.Density ?? 1;
            outRect?.Set((int)(Math.Max(MatrixViewportState.HeaderWidth, cell.X) * density),
                (int)(Math.Max(owner._state.CellHeight, cell.Y) * density),
                (int)(Math.Min(owner._state.Width, cell.X + cell.Width) * density),
                (int)(Math.Min(owner._state.Height, cell.Y + cell.Height) * density));
        }
    }
#else
    private bool SelectionHoldOpened => false;
    private void BeginSelectionHold(PointF point) { }
    private void CancelSelectionHold() { }
    private void CloseSelectionMenu() { }
#endif
}
