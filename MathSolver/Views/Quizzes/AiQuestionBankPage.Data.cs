using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private CancellationTokenSource? _dataCancellation;
    private bool _confirmingDeleteAll;
    private SqlResultCellEventArgs? _selectedSqlCell;
    private BankQueryResult? _sqlResults;
    private string? _sqlResultQuery;
    private BankGridRow? _selectedSqlRow;
    private BankGridRow? _editingSqlRow;
    private string? _selectedSqlColumn;
    private static readonly FilePickerFileType ExcelType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.WinUI] = [".xlsx"],
        [DevicePlatform.Android] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [DevicePlatform.iOS] = ["org.openxmlformats.spreadsheetml.sheet"],
        [DevicePlatform.MacCatalyst] = ["org.openxmlformats.spreadsheetml.sheet"]
    });

    private void UpdateDataActions()
    {
        bool idle = _dataCancellation is null && !_confirmingDeleteAll && !_bank.Generation.IsDeletingAll;
        bool inserting = QueryResultsTable.IsInserting;
        bool editing = QueryResultsTable.IsEditing;
        bool draft = inserting || editing;
        DeleteAllButton.IsEnabled = idle && !draft && !_saving && !_bank.Generation.IsRunning;
        QueryButton.IsEnabled = ImportExcelButton.IsEnabled = idle && !draft;
        QueryEditor.IsEnabled = ExportExcelButton.IsEnabled = idle;
        CancelDataButton.IsEnabled = _dataCancellation is not null;
        QueryResultsTable.IsEnabled = idle;
        ReloadSqlGridButton.IsEnabled = LoadEditableSqlButton.IsEnabled = idle && !draft;
        AddSqlRowButton.IsEnabled = idle && !draft && _sqlResults?.EditableColumns is not null;
        EditSqlRowButton.IsVisible = !draft && _sqlResults?.EditableColumns is not null;
        EditSqlRowButton.IsEnabled = idle && !draft && _selectedSqlRow is not null && _selectedSqlCell is not null
            && _sqlResults?.EditableColumns?.Any(column => column != "Hash") == true;
        SaveSqlRowButton.IsEnabled = CancelSqlRowButton.IsEnabled = idle && editing;
        SaveSqlRowButton.IsVisible = CancelSqlRowButton.IsVisible = editing;
        bool canDelete = !inserting && _selectedSqlRow is not null
            && (!editing || _selectedSqlRow.Hash == _editingSqlRow?.Hash);
        DeleteSqlRowButton.IsEnabled = idle && canDelete;
        DeleteSqlRowButton.IsVisible = canDelete;
        SaveNewSqlRowButton.IsEnabled = CancelNewSqlRowButton.IsEnabled = CloseSqlCellButton.IsEnabled = idle;
        SaveNewSqlRowButton.IsVisible = CancelNewSqlRowButton.IsVisible = NewSqlRowHintLabel.IsVisible = inserting;
        QueryCellEditor.IsEnabled = idle;
        InquiryToggleButton.Text = T(InquiryPanel.IsVisible ? "InquiryHide" : "InquiryShow");
    }

    private void OnToggleInquiryClicked(object? sender, EventArgs e)
    {
        InquiryPanel.IsVisible = !InquiryPanel.IsVisible;
        InquiryToggleButton.Text = T(InquiryPanel.IsVisible ? "InquiryHide" : "InquiryShow");
    }

    private void OnCancelDataClicked(object? sender, EventArgs e) => _dataCancellation?.Cancel();

    private async void OnDeleteAllClicked(object? sender, EventArgs e)
    {
        if (!DeleteAllButton.IsEnabled) return;
        _confirmingDeleteAll = true;
        Render();
        try
        {
            bool confirmed = await DisplayAlertAsync(T("DeleteAllTitle"), T("DeleteAllConfirm"),
                T("DeleteAllAccept"), T("Cancel"));
            if (!confirmed) return;
            await RunDataOperationAsync(true, async cancellation =>
            {
                int deleted = await _bank.Generation.DeleteAllAsync(cancellation);
                ClearSqlResults();
                InquiryStatusLabel.Text = "";
                DataTransferStatusLabel.Text = deleted == 0 ? T("DeleteAllEmpty")
                    : string.Format(CultureInfo.CurrentCulture, T("DeleteAllSuccess"), deleted);
            });
        }
        catch (Exception error) { await ShowErrorAsync(error); }
        finally { _confirmingDeleteAll = false; Render(); }
    }

    private async Task RunDataOperationAsync(bool transfer, Func<CancellationToken, Task> work)
    {
        if (_dataCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _dataCancellation = cancellation;
        UpdateDataActions();
        var status = transfer ? DataTransferStatusLabel : InquiryStatusLabel;
        status.Text = T("DataWorking");
        try { await work(cancellation.Token); }
        catch (OperationCanceledException) { status.Text = T("DataCancelled"); }
        catch (Exception error) { status.Text = T("DataFailed") + ": " + ErrorText(error.Message); }
        finally { _dataCancellation = null; UpdateDataActions(); }
    }

    private async void OnRunInquiryClicked(object? sender, EventArgs e)
    {
        string query = QueryEditor.Text ?? "";
        await RunDataOperationAsync(false, async cancellation =>
        {
            ClearSqlResults();
            var result = await _bank.Store.QueryAsync(query, cancellation);
            if (!result.IsSuccess)
            {
                string detail = result.SqliteErrorCode is int sqliteCode
                    ? $"SQLite ({sqliteCode}): {result.ErrorMessage}"
                    : ErrorText(result.ErrorCode!);
                InquiryStatusLabel.Text = T("DataFailed") + ": " + detail;
                QueryResultsPanel.IsVisible = QueryResultMessageLabel.IsVisible = true;
                QueryResultMessageLabel.Text = detail;
                return;
            }
            DisplaySqlResults(result, query);
        });
    }

    private void DisplaySqlResults(BankQueryResult result, string query)
    {
        _sqlResults = result;
        _sqlResultQuery = result.IsWrite ? null : query;
        string summary = result.IsWrite
            ? string.Format(CultureInfo.CurrentCulture, T("InquiryChanged"), result.AffectedRows)
            : string.Format(CultureInfo.CurrentCulture, T("InquiryResult"), result.Rows.Count);
        if (result.Columns.Length > 0)
        {
            QueryResultsTable.SetData(result.Columns, result.Rows);
            QueryResultsPanel.IsVisible = true;
            QueryGridHintLabel.IsVisible = result.Rows.Count > 0;
            bool editable = result.EditableColumns is not null;
            QueryEditHintLabel.Text = T(editable ? "GridEditableHint" : "GridReadOnlyHint");
            AddSqlRowButton.IsVisible = editable;
            LoadEditableSqlButton.IsVisible = !editable;
            ReloadSqlGridButton.IsVisible = !result.IsWrite;
        }
        InquiryStatusLabel.Text = summary
            + (result.IsWrite && result.Columns.Length > 0 ? " " + string.Format(CultureInfo.CurrentCulture, T("InquiryResult"), result.Rows.Count) : "")
            + (result.Truncated ? " " + T("InquiryTruncated") : "");
    }

    private void ClearSqlResults()
    {
        QueryResultsTable.Clear();
        QueryResultsPanel.IsVisible = QueryResultMessageLabel.IsVisible = QueryGridHintLabel.IsVisible = false;
        QueryResultMessageLabel.Text = "";
        QueryEditHintLabel.Text = "";
        AddSqlRowButton.IsVisible = LoadEditableSqlButton.IsVisible = ReloadSqlGridButton.IsVisible = false;
        _sqlResults = null;
        _sqlResultQuery = null;
        _editingSqlRow = null;
        CloseNewSqlRow();
        CloseSqlCell();
    }

    private async void OnSqlCellSelected(object? sender, SqlResultCellEventArgs cell)
    {
        if (_dataCancellation is not null || QueryResultsTable.IsInserting) return;
        if (QueryResultsTable.IsEditing && cell.RowNumber == QueryResultsTable.EditingRowNumber)
        {
            _selectedSqlCell = cell;
            _selectedSqlRow = _editingSqlRow;
            _selectedSqlColumn = _sqlResults!.EditableColumns![cell.ColumnIndex];
            RefreshSqlCellLabels();
            ShowSqlCellInfo(QueryResultsTable.GetEditValues()[_selectedSqlColumn]);
            UpdateDataActions();
            return;
        }
        CloseSqlCell();
        _selectedSqlCell = cell;
        RefreshSqlCellLabels();
        ShowSqlCellInfo(cell.Value);
        if (_sqlResults?.EditableColumns is not { } columns) return;
        string hash = _sqlResults.Rows[cell.RowNumber - 1][Array.IndexOf(columns, "Hash")];
        await RunDataOperationAsync(false, async cancellation =>
        {
            var row = await _bank.Store.GetGridRowAsync(hash, cancellation);
            if (row is null) { InquiryStatusLabel.Text = ErrorText("GridRowChanged"); return; }
            _selectedSqlRow = row;
            _selectedSqlColumn = columns[cell.ColumnIndex];
            object? value = row.Values[_selectedSqlColumn];
            ShowSqlCellInfo(value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture));
            InquiryStatusLabel.Text = T("GridCellLoaded");
        });
    }

    private async void OnEditSqlRowClicked(object? sender, EventArgs e)
    {
        if (!EditSqlRowButton.IsEnabled || _selectedSqlRow is not { } selected || _selectedSqlCell is not { } cell
            || _sqlResults?.EditableColumns is not { } columns) return;
        await RunDataOperationAsync(false, async cancellation =>
        {
            // Refresh the original snapshot only when explicitly entering edit mode.
            var row = await _bank.Store.GetGridRowAsync(selected.Hash, cancellation);
            if (row is null)
            {
                _selectedSqlRow = null;
                InquiryStatusLabel.Text = ErrorText("GridRowChanged");
                return;
            }
            _selectedSqlRow = _editingSqlRow = row;
            QueryResultsTable.BeginEdit(cell.RowNumber, columns.Select(name => new SqlInsertColumn(name, "", name == "Hash")).ToArray(),
                row.Values, T("GridNull"), T("GridAutoKey"));
            _selectedSqlColumn = columns[cell.ColumnIndex];
            ShowSqlCellInfo(QueryResultsTable.GetEditValues()[_selectedSqlColumn]);
            InquiryStatusLabel.Text = T("GridRowEditing");
        });
        int focusColumn = columns[cell.ColumnIndex] == "Hash" ? Array.FindIndex(columns, name => name != "Hash") : cell.ColumnIndex;
        QueryResultsTable.FocusEditCell(focusColumn);
    }

    private void OnSqlRowDraftChanged(object? sender, EventArgs e)
    {
        if (_selectedSqlCell?.RowNumber == QueryResultsTable.EditingRowNumber && _selectedSqlColumn is not null && QueryCellDetailPanel.IsVisible
            && QueryResultsTable.GetEditValues().TryGetValue(_selectedSqlColumn, out var value))
            ShowSqlCellInfo(value);
    }

    private void ShowSqlCellInfo(string? value)
    {
        // The detail panel is read-only; full whitespace/JSON is retained for copying.
        QueryCellEditor.Text = value ?? "NULL";
        QueryCellDetailPanel.IsVisible = true;
        QueryCellReadOnlyLabel.IsVisible = _selectedSqlColumn == "Hash";
        ResizeSqlCellInfo();
    }

    private void OnSqlCellInfoSizeChanged(object? sender, EventArgs e) => ResizeSqlCellInfo();
    private void ResizeSqlCellInfo()
    {
        int charactersPerLine = Math.Max(12, (int)((QueryCellDetailPanel.Width > 0 ? QueryCellDetailPanel.Width - 32 : 560) / 8));
        int lines = 0;
        foreach (string line in (QueryCellEditor.Text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            lines += Math.Max(1, (line.Length + charactersPerLine - 1) / charactersPerLine);
            if (lines >= 5) break;
        }
        QueryCellEditor.HeightRequest = Math.Clamp(lines * 22 + 14, 48, 128);
    }

    private void RefreshSqlCellLabels()
    {
        if (_selectedSqlCell is { } cell)
            QueryCellTitleLabel.Text = string.Format(CultureInfo.CurrentCulture, T("SqlCellTitle"), cell.RowNumber, cell.ColumnName);
        CopySqlCellButton.Text = T("CopySqlCell");
        QueryResultsTable.UpdateInsertLabels(T("GridNull"), T("GridAutoKey"));
        if (_sqlResults is not null)
            QueryEditHintLabel.Text = T(_sqlResults.EditableColumns is not null ? "GridEditableHint" : "GridReadOnlyHint");
    }

    private async void OnCopySqlCellClicked(object? sender, EventArgs e)
    {
        if (_selectedSqlCell is not { } cell) return;
        try
        {
            await Clipboard.Default.SetTextAsync(QueryCellEditor.Text ?? "");
            if (ReferenceEquals(cell, _selectedSqlCell)) CopySqlCellButton.Text = T("SqlCellCopied");
        }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private void OnCloseSqlCellClicked(object? sender, EventArgs e) => CloseSqlCell();
    private void CloseSqlCell()
    {
        _selectedSqlCell = null;
        _selectedSqlRow = null;
        _selectedSqlColumn = null;
        QueryCellDetailPanel.IsVisible = false;
        QueryCellReadOnlyLabel.IsVisible = false;
        QueryCellEditor.Text = "";
        QueryCellTitleLabel.Text = "";
        UpdateDataActions();
    }

    private async void OnSaveSqlRowClicked(object? sender, EventArgs e)
    {
        if (_dataCancellation is not null || !QueryResultsTable.IsEditing || _editingSqlRow is not { } row) return;
        var values = QueryResultsTable.GetEditValues().Where(field => field.Key != "Hash").ToDictionary(field => field.Key, field => field.Value);
        await RunDataOperationAsync(false, async cancellation =>
        {
            var saved = await _bank.Store.UpdateGridRowAsync(row, values, cancellation);
            if (!saved.IsSuccess) { InquiryStatusLabel.Text = ErrorText(saved.ErrorCode!); return; }
            await RefreshSqlGridAsync(CancellationToken.None, T("GridRowSaved"));
        });
    }

    private void OnCancelSqlRowClicked(object? sender, EventArgs e)
    {
        if (_dataCancellation is not null) return;
        QueryResultsTable.CancelEdit();
        _editingSqlRow = null;
        CloseSqlCell();
        UpdateDataActions();
    }

    private async void OnDeleteSqlRowClicked(object? sender, EventArgs e)
    {
        if (!DeleteSqlRowButton.IsEnabled || _selectedSqlRow is not { } row) return;
        await RunDataOperationAsync(false, async cancellation =>
        {
            var deleted = await _bank.Store.DeleteGridRowAsync(row, cancellation);
            if (!deleted.IsSuccess) { InquiryStatusLabel.Text = ErrorText(deleted.ErrorCode!); return; }
            await RefreshSqlGridAsync(CancellationToken.None, T("GridRowDeleted"));
        });
    }

    private async Task RefreshSqlGridAsync(CancellationToken cancellation, string? message = null)
    {
        string? query = _sqlResultQuery;
        if (query is null) return;
        var result = await _bank.Store.QueryAsync(query, cancellation);
        ClearSqlResults();
        if (!result.IsSuccess)
        {
            InquiryStatusLabel.Text = (message is null ? "" : message + "\n") + T("DataFailed") + ": "
                + (result.ErrorMessage ?? ErrorText(result.ErrorCode!));
            return;
        }
        DisplaySqlResults(result, query);
        if (message is not null) InquiryStatusLabel.Text = message + "\n" + InquiryStatusLabel.Text;
    }

    private async void OnReloadSqlGridClicked(object? sender, EventArgs e) =>
        await RunDataOperationAsync(false, cancellation => RefreshSqlGridAsync(cancellation));

    private async void OnLoadEditableSqlClicked(object? sender, EventArgs e)
    {
        QueryEditor.Text = "SELECT *\nFROM BasicQuestionBank\nORDER BY CreatedUtc DESC\nLIMIT 50;";
        await RunDataOperationAsync(false, async cancellation =>
        {
            var result = await _bank.Store.QueryAsync(QueryEditor.Text, cancellation);
            ClearSqlResults();
            if (!result.IsSuccess) { InquiryStatusLabel.Text = result.ErrorMessage ?? ErrorText(result.ErrorCode!); return; }
            DisplaySqlResults(result, QueryEditor.Text);
        });
    }

    private void OnAddSqlRowClicked(object? sender, EventArgs e)
    {
        if (_dataCancellation is not null || QueryResultsTable.IsInserting || QueryResultsTable.IsEditing || _sqlResults?.EditableColumns is not { } columns) return;
        CloseSqlCell();
        QueryResultsTable.BeginInsert(columns.Select(name =>
        {
            var column = QuestionBankStore.GridColumns.Single(c => c.Name == name);
            return new SqlInsertColumn(name, name == "CreatedUtc"
                ? DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) : column.DefaultValue, column.IsKey);
        }).ToArray(), T("GridNull"), T("GridAutoKey"));
        UpdateDataActions();
    }

    private async void OnSaveNewSqlRowClicked(object? sender, EventArgs e)
    {
        if (_dataCancellation is not null || _sqlResults?.EditableColumns is null || !QueryResultsTable.IsInserting) return;
        var values = new Dictionary<string, string?>(QueryResultsTable.GetInsertValues());
        values.TryAdd("CreatedUtc", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        await RunDataOperationAsync(false, async cancellation =>
        {
            var inserted = await _bank.Store.InsertGridRowAsync(values, cancellation);
            if (!inserted.IsSuccess) { InquiryStatusLabel.Text = ErrorText(inserted.ErrorCode!); return; }
            await RefreshSqlGridAsync(CancellationToken.None, T("GridRowInserted"));
        });
    }

    private void OnCancelNewSqlRowClicked(object? sender, EventArgs e) => CloseNewSqlRow();
    private void CloseNewSqlRow()
    {
        QueryResultsTable.CancelInsert();
        UpdateDataActions();
    }

    private async void OnImportExcelClicked(object? sender, EventArgs e)
    {
        await RunDataOperationAsync(true, async cancellation =>
        {
            var file = await FilePicker.Default.PickAsync(new() { PickerTitle = T("ImportExcel"), FileTypes = ExcelType });
            if (file is null) { DataTransferStatusLabel.Text = ""; return; }
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("InvalidExcelFile");
            string path = Path.Combine(FileSystem.CacheDirectory, "question-import-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                await using (var source = await file.OpenReadAsync())
                await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                {
                    byte[] buffer = new byte[65536];
                    long length = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
                    {
                        length += read;
                        if (length > QuestionBankWorkbook.MaxImportBytes) throw new InvalidDataException("ExcelFileTooLarge");
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    }
                }
                await using var input = File.OpenRead(path);
                var report = await _bank.Store.ImportExcelAsync(input, cancellation);
                DataTransferStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, T("ImportResult"), report.Inserted, report.Duplicates, report.Rejected)
                    + (report.Issues.Count == 0 ? "" : "\n" + string.Join("\n", report.Issues.Select(issue =>
                        string.Format(CultureInfo.CurrentCulture, T("ImportRowError"), issue.RowNumber, ErrorText(issue.ErrorCode)))))
                    + (report.Rejected > report.Issues.Count ? "\n" + T("ImportIssuesTruncated") : "");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }

    private async void OnExportExcelClicked(object? sender, EventArgs e)
    {
        await RunDataOperationAsync(true, async cancellation =>
        {
            string path = Path.Combine(FileSystem.CacheDirectory, "question-export-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                BankExportReport report;
                await using (var output = File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    report = await _bank.Store.ExportExcelAsync(output, cancellation);
                cancellation.ThrowIfCancellationRequested();
                await using var input = File.OpenRead(path);
                var saved = await FileSaver.Default.SaveAsync($"MathSolver-question-bank-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx", input, cancellation);
                if (!saved.IsSuccessful)
                {
                    if (saved.Exception is OperationCanceledException) throw new OperationCanceledException(cancellation);
                    throw saved.Exception ?? new IOException(T("ExportFailed"));
                }
                DataTransferStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, T("ExportResult"), report.Exported, report.Skipped);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
