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
        var previous = _result;
        _viewportResult = null; _failedReadResult = null;
        _result = null; _elapsed = null; _statusKey = null; _errorKey = null;
        DisposeOutcome(previous);
        _message.Text = "";
        RefreshExecution();
        if (wasResult) { _selected = 0; RefreshSource(); }
        else SetBusyState();
    }

    private void RefreshSource()
    {
        if (_selected == 2 && _result is { } result)
        {
            _viewportDraft = null;
            _viewportResult = result;
            _viewport.SetSource(result.Rows, result.Columns, ReadViewportResultCell);
        }
        else
        {
            _viewportResult = null;
            if (_selected == 2) _selected = 0;
            MatrixDraft draft = CurrentDraft;
            _viewportDraft = draft;
            _viewport.SetSource(draft.Rows, draft.Columns, ReadViewportDraftCell);
        }
        RefreshSelection(); RefreshCell(); RefreshRange();
    }

    private string ReadViewportDraftCell(int row, int column)
    {
        if (_viewportDraft is not { } draft) return string.Empty;
        try { return draft.GetCellText(row, column); }
        catch (Exception error) when (IsResultStorageFailure(error))
        {
            if (!ReferenceEquals(_failedReadDraft, draft))
            {
                _failedReadDraft = draft;
                Debug.WriteLine(error);
                if (CanTouchUi) ReportError("StorageUnavailable");
            }
            return T("UnavailableCell");
        }
    }

    private string ReadViewportResultCell(int row, int column)
    {
        if (_viewportResult is not { } result) return string.Empty;
        try { return result.GetValue(row, column).ToString(CultureInfo.InvariantCulture); }
        catch (Exception error) when (IsResultStorageFailure(error))
        {
            ReportResultReadFailure(result, error);
            return T("UnavailableCell");
        }
    }

    private static bool IsResultStorageFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException or ObjectDisposedException;

    private void ReportResultReadFailure(MatrixCalculation result, Exception error)
    {
        if (ReferenceEquals(_failedReadResult, result)) return;
        _failedReadResult = result;
        Debug.WriteLine(error);
        if (CanTouchUi) ReportError("StorageUnavailable");
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
        int aCells = _a.Rows * _a.Columns, bCells = _b.Rows * _b.Columns;
        _inputStatus.Text = F("DraftStatus", "A", _a.Rows, _a.Columns, aCells - _a.MissingCount, aCells)
            + "\n" + F("DraftStatus", "B", _b.Rows, _b.Columns, bCells - _b.MissingCount, bCells);
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
        _pasteCells.IsEnabled = !_busy && _selected != 2;
        _progress.IsVisible = _busy;
        _activity.IsVisible = _activity.IsRunning = _busy;
    }

    private void RefreshCell()
    {
        int row = _viewport.SelectedRow, column = _viewport.SelectedColumn;
        _row.Text = (row + 1).ToString(CultureInfo.InvariantCulture);
        _column.Text = (column + 1).ToString(CultureInfo.InvariantCulture);
        var range = _viewport.Selection;
        _selection.Text = _viewport.AllSelected ? F("AllSelected", "ABC"[_selected])
            : _viewport.HasRangeSelection ? F("RangeSelected", "ABC"[_selected], range.Row + 1, range.Column + 1,
                range.Row + range.Rows, range.Column + range.Columns, range.Rows, range.Columns)
            : F("Selected", "ABC"[_selected], row + 1, column + 1);
        SemanticProperties.SetDescription(_value, _selection.Text + ". " + T("CellValue"));
        _updatingEditor = true;
        try
        {
            _value.Text = _selected == 2 && _result is not null
                ? ReadViewportResultCell(row, column) : ReadViewportDraftCell(row, column);
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
        try
        {
            if (!draft.TrySetCell(_editorRow, _editorColumn, _value.Text))
            { ReportError("InvalidCell", "AB"[_editorMatrix], _editorRow + 1, _editorColumn + 1); return false; }
        }
        catch (Exception error) when (MatrixMemoryGuard.IsMemoryFailure(error) || IsResultStorageFailure(error))
        {
            Debug.WriteLine(error);
            ReportError(MatrixMemoryGuard.IsMemoryFailure(error) ? "MemoryUnavailable" : "StorageUnavailable");
            return false;
        }
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
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1 && value <= InputDimensionLimit;

    private async void OnCreate(object? sender, EventArgs e)
    {
        if (_busy) return;
        if (!TryDimension(_aRows.Text, out int ar) || !TryDimension(_aColumns.Text, out int ac)
            || !TryDimension(_bRows.Text, out int br) || !TryDimension(_bColumns.Text, out int bc))
        { ReportError("InvalidDimensions", InputDimensionLimit); return; }
        int version = _version;
        if ((_a.HasAnyValues || _b.HasAnyValues) && !await ConfirmAsync(T("ReplaceAll"))) return;
        if (!CanPublish(version) || _busy) return;
        await RunWorkAsync((token, progress) => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var a = new MatrixDraft(ar, ac);
            MatrixDraft? b = null;
            try
            {
                token.ThrowIfCancellationRequested();
                progress.Report(0.5);
                b = new MatrixDraft(br, bc);
                token.ThrowIfCancellationRequested();
                progress.Report(1);
                return new DraftPair(a, b);
            }
            catch { a.Dispose(); b?.Dispose(); throw; }
        }, token), drafts =>
        {
            PublishDrafts(drafts.A, drafts.B, 0, "Created");
            return Task.FromResult(true);
        }, "Creating");
    }

    private sealed record DraftPair(MatrixDraft A, MatrixDraft B) : IDisposable
    {
        public void Dispose() { A.Dispose(); B.Dispose(); }
    }

    // Publication transfers ownership only after the new inputs can be displayed.
    // A failure leaves the old inputs alive; RunWorkAsync releases the staged ones.
    private void PublishDrafts(MatrixDraft a, MatrixDraft b, int selection, string status, Action? afterRefresh = null)
    {
        var oldA = _a; var oldB = _b; int oldSelection = _selected;
        var oldResult = _result; double? oldElapsed = _elapsed;
        var oldViewportDraft = _viewportDraft; var oldViewportResult = _viewportResult;
        var oldFailedReadDraft = _failedReadDraft; var oldFailedReadResult = _failedReadResult;
        var oldDimensions = (_aRows.Text, _aColumns.Text, _bRows.Text, _bColumns.Text);
        _editorMatrix = -1; _a = a; _b = b; _selected = selection;
        _result = null; _elapsed = null;
        _viewportResult = null; _failedReadDraft = null; _failedReadResult = null;
        try
        {
            // Keep the old result alive until the staged inputs and UI have been
            // published, just as result publication retains its previous result.
            RefreshExecution(); RefreshSource();
            afterRefresh?.Invoke();
            if (ReferenceEquals(_failedReadDraft, a) || ReferenceEquals(_failedReadDraft, b))
                throw new IOException("The staged matrix input could not be read during publication.");
            _aRows.Text = a.Rows.ToString(CultureInfo.InvariantCulture);
            _aColumns.Text = a.Columns.ToString(CultureInfo.InvariantCulture);
            _bRows.Text = b.Rows.ToString(CultureInfo.InvariantCulture);
            _bColumns.Text = b.Columns.ToString(CultureInfo.InvariantCulture);
            Status(status);
        }
        catch
        {
            _a = oldA; _b = oldB; _selected = oldSelection;
            _result = oldResult; _elapsed = oldElapsed;
            _viewportDraft = oldViewportDraft; _viewportResult = oldViewportResult;
            _failedReadDraft = oldFailedReadDraft; _failedReadResult = oldFailedReadResult;
            try
            {
                (_aRows.Text, _aColumns.Text, _bRows.Text, _bColumns.Text) = oldDimensions;
                RefreshSource(); RefreshExecution();
            }
            catch (Exception error) { Debug.WriteLine(error); }
            throw;
        }
        DisposeOutcome(oldResult);
        if (!ReferenceEquals(oldA, a)) DisposeOutcome(oldA);
        if (!ReferenceEquals(oldB, b)) DisposeOutcome(oldB);
    }

    private async void OnFillMissing(object? sender, EventArgs e)
    {
        if (_busy || _selected == 2 || !CanTouchUi || !_active || !CommitPendingCell()) return;
        int target = _selected;
        var original = CurrentDraft;
        await RunWorkAsync((token, _) => Task.Run(() => original.WithMissingFilledWithZero(token)), draft =>
        {
            PublishDrafts(target == 0 ? draft : _a, target == 1 ? draft : _b, target, "Created");
            return Task.FromResult(true);
        }, "Creating");
    }
    private async Task<bool> ConfirmAsync(string message, string acceptKey = "Confirm")
    {
        Element? parent = this;
        while (parent is not null && parent is not Page) parent = parent.Parent;
        return parent is Page page && CanTouchUi && await page.DisplayAlertAsync(T("Title"), message, T(acceptKey), T("Cancel"));
    }
    private string FormatError() => F(_errorKey!, _errorArguments.Select(argument =>
        argument is MatrixInputError code
            ? code == MatrixInputError.TooLarge ? F("Error.TooLarge", InputDimensionLimit) : T("Error." + code)
            : argument).ToArray());
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
            try
            {
                var options = new MatrixExecutionOptions
                {
                    TemporaryDirectory = Path.Combine(FileSystem.CacheDirectory, "matrix-results")
                };
                return MatrixCalculationRunner.TryCalculate(a, b, operation, backend, token, progress, executionOptions: options);
            }
            finally { timer.Stop(); }
        }), attempt =>
        {
            if (attempt.IsCancelled)
            {
                Status("Cancelled");
                return Task.FromResult(false);
            }
            if (attempt.Result is not { } result)
            {
                const double bytesPerGiB = 1024d * 1024 * 1024;
                ReportError("CalculationMemoryUnavailable", attempt.Memory.RequiredBytes / bytesPerGiB,
                    attempt.Memory.AvailableBytes / bytesPerGiB);
                return Task.FromResult(false);
            }
            var previous = _result;
            double? previousElapsed = _elapsed;
            int previousSelection = _selected;
            _viewportResult = null; _failedReadResult = null;
            _result = result; _elapsed = timer.Elapsed.TotalSeconds; _selected = 2;
            try
            {
                RefreshSource(); RefreshExecution();
                if (ReferenceEquals(_failedReadResult, result))
                    throw new IOException("The new matrix result could not be read during publication.");
                Status("Completed");
            }
            catch
            {
                // Publication has not transferred ownership yet. Keep the old result
                // readable, and let RunWorkAsync dispose the unpublished attempt.
                _viewportResult = previousSelection == 2 ? previous : null;
                _failedReadResult = null;
                _result = previous; _elapsed = previousElapsed; _selected = previousSelection;
                try { RefreshSource(); RefreshExecution(); }
                catch (Exception error) { Debug.WriteLine(error); }
                throw;
            }
            DisposeOutcome(previous);
            return Task.FromResult(true);
        }, "Calculating");
    }

}
