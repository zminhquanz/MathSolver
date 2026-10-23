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
        if (!HasInputs)
        {
            _viewportResult = null; _editorMatrix = -1; _selected = 0;
            // Geometry-only empty reader; no 1x1 MatrixDraft is created or shown.
            _viewport.SetSource(1, 1, static (_, _) => string.Empty);
            _updatingEditor = true;
            try { _value.Text = string.Empty; }
            finally { _updatingEditor = false; }
            _selection.Text = string.Empty;
            RefreshSelection(); RefreshSolution(); RefreshRange();
            return;
        }
        if (_selected == 2 && _result is { } result)
        {
            _viewportResult = result;
            _viewport.SetSource(result.Rows, result.Columns, ReadViewportResultCell);
        }
        else
        {
            _viewportResult = null;
            if (_selected == 2) _selected = 0;
            MatrixDraft draft = CurrentDraft;
            _failedReadDraft = null;
            _viewport.SetSource(draft.Rows, draft.Columns, (r, c) => ReadInputCell(draft, r, c));
        }
        RefreshSelection(); RefreshCell(); RefreshRange();
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
        MatrixStorageErrors.IsFailure(error);

    private string ReadInputCell(MatrixDraft draft, int row, int column)
    {
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

    private bool TryUpdateInput(Func<bool> edit)
    {
        try { return edit(); }
        catch (Exception error) when (IsResultStorageFailure(error))
        { Debug.WriteLine(error); ReportError("StorageUnavailable"); return false; }
        catch (Exception error) when (MatrixMemoryGuard.IsMemoryFailure(error))
        { ReportError("MemoryUnavailable"); return false; }
    }

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
        _inputStatus.Text = _a is { } a && _b is { } b
            ? F("DraftStatus", "A", a.Rows, a.Columns, a.CellCount - a.MissingCount, a.CellCount)
                + "\n" + F("DraftStatus", "B", b.Rows, b.Columns, b.CellCount - b.MissingCount, b.CellCount)
            : T("CreateRequired");
        SetBusyState();
    }

    private void SetBusyState()
    {
        if (_calculate is null) return;
        if (_editorCard is not null) _editorCard.IsVisible = HasInputs;
        foreach (var button in _lockDuringWork) button.IsEnabled = !_busy;
        _dimensions.IsEnabled = !_busy;
        _calculate.IsEnabled = !_busy && HasInputs;
        _value.IsEnabled = !_busy;
        _value.IsReadOnly = _selected == 2;
        _stop.IsEnabled = _busy && !_confirmingStop && _work is { IsCancellationRequested: false };
        _export.IsEnabled = !_busy && _result is not null;
        _matrices[2].IsEnabled = !_busy && _result is not null;
        _apply.IsEnabled = _fill.IsEnabled = _paste.IsEnabled = _import.IsEnabled = !_busy && HasInputs && _selected != 2;
        _pasteCells.IsEnabled = !_busy && HasInputs && _selected != 2;
        _progress.IsVisible = _progressText.IsVisible = _busy;
        _activity.IsVisible = _activity.IsRunning = _busy;
    }

    private void RefreshCell()
    {
        if (!HasInputs) { _editorMatrix = -1; return; }
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
                ? ReadViewportResultCell(row, column) : ReadInputCell(CurrentDraft, row, column);
            bool failed = _selected != 2 && ReferenceEquals(_failedReadDraft, CurrentDraft);
            if (failed) _value.Text = string.Empty;
            _editorMatrix = failed ? -1 : _selected; _editorRow = row; _editorColumn = column;
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
    private void OnDeleteSelection(MatrixCellRange range)
    {
        if (_busy || !CanTouchUi || !_active || _selected == 2) return;
        // Discard the old editor binding before refreshing its now-empty value.
        // RefreshSource would reset the selected rectangle, so retain this source.
        _editorMatrix = -1;
        // A disk-backed clear can change earlier blocks before a later I/O error.
        // C stops being current as soon as any in-place mutation may begin.
        InvalidateResult();
        bool cleared = TryUpdateInput(() => CurrentDraft.TryClearCells(range.Row, range.Column, range.Rows, range.Columns));
        RefreshCell(); RefreshSelection(); _viewport.Refresh();
        if (cleared) Status("ClearedCells");
    }
    private bool CommitPendingCell()
    {
        if (_busy || _editorMatrix is < 0 or > 1 || !HasInputs) return true;
        MatrixDraft draft = (_editorMatrix == 0 ? _a : _b)!;
        bool invalid = false;
        if (!TryUpdateInput(() =>
            { invalid = !draft.TrySetCell(_editorRow, _editorColumn, _value.Text); return !invalid; }))
        {
            if (invalid) ReportError("InvalidCell", "AB"[_editorMatrix], _editorRow + 1, _editorColumn + 1);
            return false;
        }
        RefreshSelection(); _viewport.Refresh();
        return true;
    }
    private void MoveSelection(int delta)
    {
        if (!HasInputs) return;
        int columns = _selected == 2 ? _result!.Columns : CurrentDraft.Columns;
        int rows = _selected == 2 ? _result!.Rows : CurrentDraft.Rows;
        int index = Math.Clamp(_viewport.SelectedRow * columns + _viewport.SelectedColumn + delta, 0, rows * columns - 1);
        _viewport.Select(index / columns, index % columns);
    }
    private void OnGo(object? sender, EventArgs e)
    {
        if (!HasInputs) return;
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
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value is >= 1 and <= MatrixLimits.MaximumDimension;

    private async void OnCreate(object? sender, EventArgs e)
    {
        if (_busy) return;
        if (!TryDimension(_aRows.Text, out int ar) || !TryDimension(_aColumns.Text, out int ac)
            || !TryDimension(_bRows.Text, out int br) || !TryDimension(_bColumns.Text, out int bc))
        { ReportError("InvalidDimensions"); return; }
        int version = _version;
        if ((_a?.HasAnyValues == true || _b?.HasAnyValues == true || _result is not null) && !await ConfirmAsync(T("ReplaceAll"))) return;
        if (!CanPublish(version) || _busy) return;
        await RunWorkAsync((token, progress) =>
        {
            // This callback starts on the UI thread after the operation is busy.
            // Drop the old grid delegates too, before a worker measures/allocates RAM.
            long released = ReleaseMatricesForCreation();
            string directory = Path.Combine(FileSystem.CacheDirectory, "matrix-inputs");
            return Task.Run(() =>
            {
                Directory.CreateDirectory(directory);
                return MatrixRecreation.TryCreate(ar, ac, br, bc, released, token, progress, temporaryDirectory: directory);
            });
        }, drafts =>
        {
            if (drafts.IsCanceled) { Status("Cancelled"); return Task.CompletedTask; }
            if (drafts.A is not { } a || drafts.B is not { } b)
            {
                const double gib = 1024d * 1024 * 1024;
                ReportError("CreateMemoryDetails", drafts.Memory.RequiredBytes / gib, drafts.Memory.AvailableBytes / gib);
                return Task.CompletedTask;
            }
            if (!drafts.TryPublish(ref _a, ref _b, ar, ac, br, bc))
                throw new InvalidOperationException("Created matrix dimensions do not match the requested dimensions.");
            _editorMatrix = -1; _selected = 0;
            InvalidateResult(); RefreshSource();
            if (_a?.Rows != ar || _a.Columns != ac || _b?.Rows != br || _b.Columns != bc)
                throw new InvalidOperationException("Published matrix dimensions changed while updating the view.");
            Status("Created");
            return Task.CompletedTask;
        }, "Creating", memoryErrorKey: "CreateMemoryUnavailable");
    }

    private long ReleaseMatricesForCreation()
    {
        _editorMatrix = -1;
        _failedReadDraft = null;
        _viewportResult = null; _failedReadResult = null;
        try { return MatrixRecreation.Release(ref _a, ref _b, ref _result); }
        finally
        {
            _selected = 0;
            InvalidateResult();
            RefreshSource();
            Status("Creating");
        }
    }
    private async Task<bool> ConfirmAsync(string message, string acceptKey = "Confirm", string cancelKey = "Cancel", string titleKey = "Title")
    {
        Element? parent = this;
        while (parent is not null && parent is not Page) parent = parent.Parent;
        return parent is Page page && CanTouchUi && await page.DisplayAlertAsync(T(titleKey), message, T(acceptKey), T(cancelKey));
    }

    private async void OnStop(object? sender, EventArgs e)
    {
        if (_confirmingStop || !_busy || !CanTouchUi || _work is not { IsCancellationRequested: false } cancellation) return;
        int version = _version;
        _confirmingStop = true;
        SetBusyState();
        try
        {
            bool confirmed = await ConfirmAsync(T("StopConfirmationMessage"), "Stop", "Continue", "StopConfirmationTitle");
            // Work can finish, a new operation can start, or the tab/window can
            // disappear while the dialog is open. Never cancel a different job.
            if (!confirmed || !CanPublish(version) || !_busy || !ReferenceEquals(_work, cancellation)
                || cancellation.IsCancellationRequested) return;
            Status("Stopping");
            cancellation.Cancel();
        }
        catch (Exception error) when (!CanTouchUi) { Debug.WriteLine(error); }
        finally
        {
            _confirmingStop = false;
            if (CanTouchUi) SetBusyState();
        }
    }
    private string FormatError() => F(_errorKey!, _errorArguments.Select(argument =>
        argument is MatrixInputError code ? T("Error." + code) : argument).ToArray());
    private void ReportError(string key, params object[] arguments)
    {
        _statusKey = null; _errorKey = key; _errorArguments = arguments;
        _message.SetDynamicResource(Label.TextColorProperty, "DangerColor");
        _message.Text = FormatError();
        RefreshCreationNotice();
    }
    private void Status(string key)
    {
        _statusKey = key; _errorKey = null;
        _message.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        _message.Text = FormatStatus(key);
        // A completed worker can still be cancelled before its outcome is accepted.
        // Reserve 100% for successful publication, including clipboard/file actions.
        if (_busy && _work is { IsCancellationRequested: false }
            && key is ("Completed" or "CopiedGrid" or "PastedCells" or "Imported" or "Exported" or "Created"))
        {
            _progress.Progress = 1;
            if (_calculationStage is not null) _calculationStage = MatrixCalculationStage.Completed;
        }
        RefreshProgress();
    }

    private void RefreshProgress()
    {
        _progressText.Text = F("EstimatedProgress", Math.Floor(_progress.Progress * 100));
        if (_calculationStage is { } stage)
        {
            string activity = _statusKey is "Stopping" or "Cancelled" ? T(_statusKey)
                : T("Activity." + stage);
            _progressText.Text += "\n" + F("CurrentActivity", activity);
        }
        RefreshCreationNotice();
    }

    private void RefreshCreationNotice()
    {
        _creationNotice.IsVisible = !HasInputs || _statusKey is "Creating" or "Created";
        _creationNotice.Text = _errorKey is not null ? FormatError()
            : _statusKey is not null ? FormatStatus(_statusKey) : T("CreateRequired");
        if (_busy && _statusKey == "Creating")
            _creationNotice.Text += "\n" + F("EstimatedProgress", Math.Floor(_progress.Progress * 100));
        _creationNotice.SetDynamicResource(Label.TextColorProperty,
            _errorKey is null ? "WallpaperTextSecondaryColor" : "DangerColor");
    }

    private string FormatStatus(string key)
    {
        if (key != "Created" || _a is not { } a || _b is not { } b) return T(key);
        string value = F("CreatedShape", a.Rows, a.Columns, b.Rows, b.Columns);
        if (a.Matrix.IsFileBacked || b.Matrix.IsFileBacked)
            value += "\n" + F("InputDiskStorage", ((a.Matrix.InputStore?.CacheLimitBytes ?? 0)
                + (b.Matrix.InputStore?.CacheLimitBytes ?? 0)) / (1024 * 1024));
        return value;
    }

    private async void OnCalculate(object? sender, EventArgs e)
    {
        if (_busy || !CanTouchUi) return;
        if (_a is not { } draftA || _b is not { } draftB) { ReportError("CreateRequired"); return; }
        if (_selected != 2 && !ApplyCell(advance: false)) return;
        if (!draftA.IsComplete || !draftB.IsComplete) { ReportError("MissingCells", draftA.MissingCount, draftB.MissingCount); return; }
        if (_operation == MatrixOperation.Multiply ? draftA.Columns != draftB.Rows : draftA.Rows != draftB.Rows || draftA.Columns != draftB.Columns)
        { ReportError(_operation == MatrixOperation.Multiply ? "MultiplyShape" : "AddShape"); return; }
        IntegerMatrix a = draftA.Matrix, b = draftB.Matrix;
        MatrixOperation operation = _operation;
        MatrixBackend backend = CalculationAccelerationManager.EffectiveMatrixBackend;
        var timer = new Stopwatch();
        await RunWorkAsync((token, progress, calculationProgress) => Task.Run(() =>
        {
            timer.Start();
            try
            {
                var options = new MatrixExecutionOptions
                {
                    TemporaryDirectory = Path.Combine(FileSystem.CacheDirectory, "matrix-results")
                };
                return MatrixEngine.TryCalculate(a, b, operation, backend, token, progress, executionOptions: options,
                    calculationProgress: calculationProgress);
            }
            finally { timer.Stop(); }
        }), attempt =>
        {
            if (attempt.IsCanceled) { Status("Cancelled"); return Task.CompletedTask; }
            if (attempt.Result is not { } result)
            {
                const double bytesPerGiB = 1024d * 1024 * 1024;
                ReportError("CalculationMemoryUnavailable", attempt.Memory.RequiredBytes / bytesPerGiB,
                    attempt.Memory.AvailableBytes / bytesPerGiB);
                return Task.CompletedTask;
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
            return Task.CompletedTask;
        }, "Calculating", showCalculationProgress: true);
    }

}
