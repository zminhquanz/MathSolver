using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Diagnostics;
using System.Globalization;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private void InvalidateResult()
    {
        bool wasResult = _selected == 2;
        _result = null; _elapsed = null; _statusKey = null; _errorKey = null;
        _message.Text = "";
        RefreshExecution();
        if (wasResult) { _selected = 0; RefreshSource(); }
        else SetBusyState();
    }

    private void RefreshSource()
    {
        if (_selected == 2 && _result is { } result)
            _viewport.SetSource(result.Rows, result.Columns, (r, c) => result.GetValue(r, c).ToString(CultureInfo.InvariantCulture));
        else
        {
            if (_selected == 2) _selected = 0;
            MatrixDraft draft = CurrentDraft;
            _viewport.SetSource(draft.Rows, draft.Columns, draft.GetCellText);
        }
        RefreshSelection(); RefreshCell(); RefreshRange();
    }

    private void RefreshSelection()
    {
        _heroSymbol.Text = _operation switch
        {
            MatrixOperation.Subtract => "[ A ] − [ B ]",
            MatrixOperation.Multiply => "[ A ] × [ B ]",
            _ => "[ A ] + [ B ]"
        };
        SelectionButtonStyler.Select(_operations[(int)_operation], _operations);
        SelectionButtonStyler.Select(_matrices[_selected], _matrices);
        _inputStatus.Text = F("DraftStatus", "A", _a.Rows, _a.Columns, _a.Matrix.Data.Length - _a.MissingCount, _a.Matrix.Data.Length)
            + "\n" + F("DraftStatus", "B", _b.Rows, _b.Columns, _b.Matrix.Data.Length - _b.MissingCount, _b.Matrix.Data.Length);
        SetBusyState();
    }

    private void SetBusyState()
    {
        if (_calculate is null) return;
        foreach (var button in _lockDuringWork) button.IsEnabled = !_busy;
        _dimensions.IsEnabled = !_busy;
        _value.IsEnabled = !_busy;
        _value.IsReadOnly = _selected == 2;
        _stop.IsEnabled = _busy;
        _export.IsEnabled = !_busy && _result is not null;
        _matrices[2].IsEnabled = !_busy && _result is not null;
        _apply.IsEnabled = _fill.IsEnabled = _paste.IsEnabled = _import.IsEnabled = !_busy && _selected != 2;
        _progress.IsVisible = _busy;
        _activity.IsVisible = _activity.IsRunning = _busy;
    }

    private void RefreshCell()
    {
        int row = _viewport.SelectedRow, column = _viewport.SelectedColumn;
        _row.Text = (row + 1).ToString(CultureInfo.InvariantCulture);
        _column.Text = (column + 1).ToString(CultureInfo.InvariantCulture);
        _selection.Text = F("Selected", "ABC"[_selected], row + 1, column + 1);
        SemanticProperties.SetDescription(_value, _selection.Text + ". " + T("CellValue"));
        _updatingEditor = true;
        try
        {
            _value.Text = _selected == 2 && _result is { } result
                ? result.GetValue(row, column).ToString(CultureInfo.InvariantCulture) : CurrentDraft.GetCellText(row, column);
            _editorMatrix = _selected; _editorRow = row; _editorColumn = column;
        }
        finally { _updatingEditor = false; }
        RefreshSolution();
        RefreshSizeControls();
    }
    private void RefreshRange()
    {
        _range.Text = F("VisibleFrom", _viewport.FirstRow + 1, _viewport.FirstColumn + 1);
        RefreshSizeControls();
    }

    private bool ApplyCell(bool advance)
    {
        if (_busy || _selected == 2) return false;
        return advance ? _viewport.Navigate(MatrixCellMove.Next) : CommitPendingCell();
    }
    private bool CommitPendingCell()
    {
        if (_busy || _editorMatrix is < 0 or > 1) return true;
        MatrixDraft draft = _editorMatrix == 0 ? _a : _b;
        if (!draft.TrySetCell(_editorRow, _editorColumn, _value.Text))
        { ReportError("InvalidCell", "AB"[_editorMatrix], _editorRow + 1, _editorColumn + 1); return false; }
        RefreshSelection(); _viewport.Refresh();
        return true;
    }
    private void MoveSelection(int delta)
    {
        int columns = _selected == 2 ? _result!.Columns : CurrentDraft.Columns;
        int rows = _selected == 2 ? _result!.Rows : CurrentDraft.Rows;
        int index = Math.Clamp(_viewport.SelectedRow * columns + _viewport.SelectedColumn + delta, 0, rows * columns - 1);
        _viewport.Select(index / columns, index % columns);
    }
    private void OnGo(object? sender, EventArgs e)
    {
        int rows = _selected == 2 ? _result!.Rows : CurrentDraft.Rows;
        int columns = _selected == 2 ? _result!.Columns : CurrentDraft.Columns;
        if (!TryDimension(_row.Text, out int row) || !TryDimension(_column.Text, out int column) || row > rows || column > columns)
        { ReportError("InvalidCoordinate", rows, columns); return; }
        if (!_viewport.Select(row - 1, column - 1))
        {
            _row.Text = (_viewport.SelectedRow + 1).ToString(CultureInfo.InvariantCulture);
            _column.Text = (_viewport.SelectedColumn + 1).ToString(CultureInfo.InvariantCulture);
        }
    }
    private static bool TryDimension(string? text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value is >= 1 and <= 1024;

    private async void OnCreate(object? sender, EventArgs e)
    {
        if (_busy) return;
        if (!TryDimension(_aRows.Text, out int ar) || !TryDimension(_aColumns.Text, out int ac)
            || !TryDimension(_bRows.Text, out int br) || !TryDimension(_bColumns.Text, out int bc))
        { ReportError("InvalidDimensions"); return; }
        int version = _version;
        if ((_a.HasAnyValues || _b.HasAnyValues) && !await ConfirmAsync(T("ReplaceAll"))) return;
        if (!CanPublish(version) || _busy) return;
        _editorMatrix = -1; _a = new(ar, ac); _b = new(br, bc); _selected = 0;
        InvalidateResult(); RefreshSource();
    }
    private async Task<bool> ConfirmAsync(string message)
    {
        Element? parent = this;
        while (parent is not null && parent is not Page) parent = parent.Parent;
        return parent is Page page && CanTouchUi && await page.DisplayAlertAsync(T("Title"), message, T("Confirm"), T("Cancel"));
    }
    private string FormatError() => F(_errorKey!, _errorArguments.Select(argument =>
        argument is MatrixInputError code ? T("Error." + code) : argument).ToArray());
    private void ReportError(string key, params object[] arguments)
    {
        _statusKey = null; _errorKey = key; _errorArguments = arguments;
        _message.SetDynamicResource(Label.TextColorProperty, "DangerColor");
        _message.Text = FormatError();
    }
    private void Status(string key)
    {
        _statusKey = key; _errorKey = null;
        _message.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        _message.Text = T(key);
    }

    private async void OnCalculate(object? sender, EventArgs e)
    {
        if (_busy || !CanTouchUi) return;
        if (_selected != 2 && !ApplyCell(advance: false)) return;
        if (!_a.IsComplete || !_b.IsComplete) { ReportError("MissingCells", _a.MissingCount, _b.MissingCount); return; }
        if (_operation == MatrixOperation.Multiply ? _a.Columns != _b.Rows : _a.Rows != _b.Rows || _a.Columns != _b.Columns)
        { ReportError(_operation == MatrixOperation.Multiply ? "MultiplyShape" : "AddShape"); return; }
        IntegerMatrix a = _a.Matrix, b = _b.Matrix;
        MatrixOperation operation = _operation;
        MatrixBackend backend = CalculationAccelerationManager.EffectiveMatrixBackend;
        var timer = new Stopwatch();
        await RunWorkAsync((token, progress) => Task.Run(() =>
        {
            timer.Start();
            try { return MatrixEngine.Calculate(a, b, operation, backend, token, progress); }
            finally { timer.Stop(); }
        }, token), result =>
        {
            _result = result; _elapsed = timer.Elapsed.TotalSeconds; _selected = 2;
            RefreshSource(); RefreshExecution(); Status("Completed");
            return Task.CompletedTask;
        }, "Calculating");
    }

}
