using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private CancellationTokenSource? _dataCancellation;
    private SqlResultCellEventArgs? _selectedSqlCell;
    private BankQueryResult? _sqlResults;
    private string? _sqlResultQuery;
    private BankGridRow? _selectedSqlRow;
    private string? _selectedSqlColumn;
    private readonly Dictionary<string, (InputView Input, CheckBox Null)> _newSqlFields = [];
    private static readonly FilePickerFileType ExcelType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.WinUI] = [".xlsx"],
        [DevicePlatform.Android] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [DevicePlatform.iOS] = ["org.openxmlformats.spreadsheetml.sheet"],
        [DevicePlatform.MacCatalyst] = ["org.openxmlformats.spreadsheetml.sheet"]
    });

    private void UpdateDataActions()
    {
        bool idle = _dataCancellation is null;
        QueryButton.IsEnabled = QueryEditor.IsEnabled = ImportExcelButton.IsEnabled = ExportExcelButton.IsEnabled = idle;
        CancelDataButton.IsEnabled = !idle;
        QueryResultsTable.IsEnabled = ReloadSqlGridButton.IsEnabled = LoadEditableSqlButton.IsEnabled = idle;
        AddSqlRowButton.IsEnabled = idle && _sqlResults?.EditableColumns is not null;
        SaveSqlCellButton.IsEnabled = DeleteSqlRowButton.IsEnabled = idle && _selectedSqlRow is not null;
        SaveNewSqlRowButton.IsEnabled = CancelNewSqlRowButton.IsEnabled = CloseSqlCellButton.IsEnabled = idle;
        NewSqlRowFields.IsEnabled = SqlCellNullPanel.IsEnabled = idle;
        QueryCellEditor.IsEnabled = idle && !SqlCellNullCheckBox.IsChecked;
        InquiryToggleButton.Text = T(InquiryPanel.IsVisible ? "InquiryHide" : "InquiryShow");
    }

    private void OnToggleInquiryClicked(object? sender, EventArgs e)
    {
        InquiryPanel.IsVisible = !InquiryPanel.IsVisible;
        InquiryToggleButton.Text = T(InquiryPanel.IsVisible ? "InquiryHide" : "InquiryShow");
    }

    private void OnCancelDataClicked(object? sender, EventArgs e) => _dataCancellation?.Cancel();

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
        CloseNewSqlRow();
        CloseSqlCell();
    }

    private async void OnSqlCellSelected(object? sender, SqlResultCellEventArgs cell)
    {
        if (_dataCancellation is not null) return;
        CloseSqlCell();
        CloseNewSqlRow();
        _selectedSqlCell = cell;
        RefreshSqlCellLabels();
        // Preserve literal whitespace and JSON instead of copying the truncated preview.
        QueryCellEditor.Text = cell.Value;
        QueryCellDetailPanel.IsVisible = true;
        if (_sqlResults?.EditableColumns is not { } columns) return;
        string hash = _sqlResults.Rows[cell.RowNumber - 1][Array.IndexOf(columns, "Hash")];
        await RunDataOperationAsync(false, async cancellation =>
        {
            var row = await _bank.Store.GetGridRowAsync(hash, cancellation);
            if (row is null) { InquiryStatusLabel.Text = ErrorText("GridRowChanged"); return; }
            _selectedSqlRow = row;
            _selectedSqlColumn = columns[cell.ColumnIndex];
            object? value = row.Values[_selectedSqlColumn];
            QueryCellEditor.Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            bool editable = _selectedSqlColumn != "Hash";
            QueryCellEditor.IsReadOnly = !editable;
            QueryCellReadOnlyLabel.IsVisible = !editable;
            SqlCellNullPanel.IsVisible = SaveSqlCellButton.IsVisible = editable;
            SqlCellNullCheckBox.IsChecked = value is null;
            DeleteSqlRowButton.IsVisible = true;
            InquiryStatusLabel.Text = T(editable ? "GridCellLoaded" : "GridKeyReadOnly");
        });
    }

    private void RefreshSqlCellLabels()
    {
        if (_selectedSqlCell is { } cell)
            QueryCellTitleLabel.Text = string.Format(CultureInfo.CurrentCulture, T("SqlCellTitle"), cell.RowNumber, cell.ColumnName);
        CopySqlCellButton.Text = T("CopySqlCell");
        if (_sqlResults is not null)
            QueryEditHintLabel.Text = T(_sqlResults.EditableColumns is not null ? "GridEditableHint" : "GridReadOnlyHint");
        foreach (var field in _newSqlFields.Values)
            if (field.Null.Parent is HorizontalStackLayout layout && layout.Children.LastOrDefault() is Label label)
                label.Text = T("GridNull");
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
        SqlCellNullPanel.IsVisible = SaveSqlCellButton.IsVisible = DeleteSqlRowButton.IsVisible = QueryCellReadOnlyLabel.IsVisible = false;
        SqlCellNullCheckBox.IsChecked = false;
        QueryCellEditor.IsReadOnly = true;
        QueryCellEditor.Text = "";
        QueryCellTitleLabel.Text = "";
    }

    private void OnSqlCellNullChanged(object? sender, CheckedChangedEventArgs e) =>
        QueryCellEditor.IsEnabled = !e.Value && _dataCancellation is null;

    private async void OnSaveSqlCellClicked(object? sender, EventArgs e)
    {
        if (_selectedSqlRow is not { } row || _selectedSqlColumn is not { } column) return;
        string text = QueryCellEditor.Text ?? "";
        bool isNull = SqlCellNullCheckBox.IsChecked;
        await RunDataOperationAsync(false, async cancellation =>
        {
            var saved = await _bank.Store.UpdateGridCellAsync(row, column, text, isNull, cancellation);
            if (!saved.IsSuccess) { InquiryStatusLabel.Text = ErrorText(saved.ErrorCode!); return; }
            await RefreshSqlGridAsync(CancellationToken.None, T("GridCellSaved"));
        });
    }

    private async void OnDeleteSqlRowClicked(object? sender, EventArgs e)
    {
        if (_selectedSqlRow is not { } row) return;
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
        if (_dataCancellation is not null || _sqlResults?.EditableColumns is null) return;
        CloseSqlCell();
        CloseNewSqlRow();
        foreach (var column in QuestionBankStore.GridColumns)
        {
            var label = new Label { Text = column.Name, FontAttributes = FontAttributes.Bold, FontSize = 14 };
            InputView input = column.Name.EndsWith("Json", StringComparison.Ordinal)
                ? new Editor { HeightRequest = 100, AutoSize = EditorAutoSizeOption.Disabled }
                : new Entry { Keyboard = column.IsInteger ? Keyboard.Numeric : Keyboard.Default };
            input.Text = column.Name == "CreatedUtc" ? DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) : column.DefaultValue;
            input.SetDynamicResource(InputView.FontFamilyProperty, "AppFontFamily");
            input.SetDynamicResource(InputView.TextColorProperty, "TextPrimaryColor");
            input.FontSize = 14;
            var isNull = new CheckBox();
            isNull.CheckedChanged += (_, change) => input.IsEnabled = !change.Value;
            NewSqlRowFields.Add(new VerticalStackLayout
            {
                Spacing = 4,
                Children = { label, input, new HorizontalStackLayout { Spacing = 8, IsVisible = !column.IsKey,
                    Children = { isNull, new Label { Text = T("GridNull"), VerticalOptions = LayoutOptions.Center, FontSize = 13 } } } }
            });
            _newSqlFields[column.Name] = (input, isNull);
        }
        NewSqlRowPanel.IsVisible = true;
    }

    private async void OnSaveNewSqlRowClicked(object? sender, EventArgs e)
    {
        if (_sqlResults?.EditableColumns is null || _newSqlFields.Count == 0) return;
        var values = _newSqlFields.ToDictionary(field => field.Key, field => field.Value.Null.IsChecked ? null : field.Value.Input.Text ?? "");
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
        NewSqlRowPanel.IsVisible = false;
        NewSqlRowFields.Clear();
        _newSqlFields.Clear();
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
