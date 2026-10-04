using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private CancellationTokenSource? _dataCancellation;
    private SqlResultCellEventArgs? _selectedSqlCell;
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
            string summary = result.IsWrite
                ? string.Format(CultureInfo.CurrentCulture, T("InquiryChanged"), result.AffectedRows)
                : string.Format(CultureInfo.CurrentCulture, T("InquiryResult"), result.Rows.Count);
            if (result.Columns.Length > 0)
            {
                QueryResultsTable.SetData(result.Columns, result.Rows);
                QueryResultsPanel.IsVisible = true;
                QueryGridHintLabel.IsVisible = result.Rows.Count > 0;
            }
            InquiryStatusLabel.Text = summary
                + (result.IsWrite && result.Columns.Length > 0 ? " " + string.Format(CultureInfo.CurrentCulture, T("InquiryResult"), result.Rows.Count) : "")
                + (result.Truncated ? " " + T("InquiryTruncated") : "");
        });
    }

    private void ClearSqlResults()
    {
        QueryResultsTable.Clear();
        QueryResultsPanel.IsVisible = QueryResultMessageLabel.IsVisible = QueryGridHintLabel.IsVisible = false;
        QueryResultMessageLabel.Text = "";
        CloseSqlCell();
    }

    private void OnSqlCellSelected(object? sender, SqlResultCellEventArgs cell)
    {
        _selectedSqlCell = cell;
        RefreshSqlCellLabels();
        // Preserve literal whitespace and JSON instead of copying the truncated preview.
        QueryCellEditor.Text = cell.Value;
        QueryCellDetailPanel.IsVisible = true;
    }

    private void RefreshSqlCellLabels()
    {
        if (_selectedSqlCell is { } cell)
            QueryCellTitleLabel.Text = string.Format(CultureInfo.CurrentCulture, T("SqlCellTitle"), cell.RowNumber, cell.ColumnName);
        CopySqlCellButton.Text = T("CopySqlCell");
    }

    private async void OnCopySqlCellClicked(object? sender, EventArgs e)
    {
        if (_selectedSqlCell is not { } cell) return;
        try
        {
            await Clipboard.Default.SetTextAsync(cell.Value);
            if (ReferenceEquals(cell, _selectedSqlCell)) CopySqlCellButton.Text = T("SqlCellCopied");
        }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private void OnCloseSqlCellClicked(object? sender, EventArgs e) => CloseSqlCell();
    private void CloseSqlCell()
    {
        _selectedSqlCell = null;
        QueryCellDetailPanel.IsVisible = false;
        QueryCellEditor.Text = "";
        QueryCellTitleLabel.Text = "";
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
