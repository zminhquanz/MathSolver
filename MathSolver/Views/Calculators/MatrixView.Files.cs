using CommunityToolkit.Maui.Storage;
using MathSolver.Services.Core;
using System.Text;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private async void OnPaste(object? sender, EventArgs e)
    {
        if (_busy || _selected == 2) return;
        int target = _selected, version = _version;
        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (!CanPublish(version) || _busy || _selected != target) return;
            if (string.IsNullOrWhiteSpace(text)) { ReportError("ClipboardEmpty"); return; }
            // More than enough for 1024² quoted signed Int64 cells and CRLF rows.
            if (text.Length > 32 * 1024 * 1024) { ReportError("Error.TooLarge"); return; }
            await RunWorkAsync((token, _) => Task.Run(() =>
            {
                using var reader = new StringReader(text);
                return MatrixTextCodec.Read(reader, token);
            }, token), matrix => AcceptImportAsync(target, matrix), "Importing");
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
            if (CanPublish(version)) ReportError("Failed");
        }
    }

    private async void OnImport(object? sender, EventArgs e)
    {
        if (_busy || _selected == 2) return;
        int target = _selected, version = _version;
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = T("Import"),
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = [".csv", ".tsv", ".txt"],
                    [DevicePlatform.Android] = ["text/*", "application/csv", "application/octet-stream"],
                    [DevicePlatform.iOS] = ["public.plain-text", "public.comma-separated-values-text"],
                    [DevicePlatform.MacCatalyst] = ["public.plain-text", "public.comma-separated-values-text"]
                })
            });
            if (file is null || !CanPublish(version) || _busy || _selected != target) return;
            await RunWorkAsync((token, _) => Task.Run(async () =>
            {
                await using Stream stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return MatrixTextCodec.Read(reader, token);
            }, token), matrix => AcceptImportAsync(target, matrix), "Importing");
        }
        catch (OperationCanceledException) { /* Native picker cancelled; existing drafts are retained. */ }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
            if (CanPublish(version)) ReportError("Failed");
        }
    }

    private async Task AcceptImportAsync(int target, IntegerMatrix matrix)
    {
        int version = _version;
        if (!await ConfirmAsync(F("AcceptImport", "AB"[target], matrix.Rows, matrix.Columns)))
        { if (CanPublish(version)) Status("Cancelled"); return; }
        if (!CanPublish(version)) return;
        if (_work?.IsCancellationRequested != false) { Status("Cancelled"); return; }
        _editorMatrix = -1;
        if (target == 0) _a = new(matrix); else _b = new(matrix);
        _aRows.Text = _a.Rows.ToString(); _aColumns.Text = _a.Columns.ToString();
        _bRows.Text = _b.Rows.ToString(); _bColumns.Text = _b.Columns.ToString();
        _selected = target; InvalidateResult(); RefreshSource(); Status("Imported");
    }

    private async void OnExport(object? sender, EventArgs e)
    {
        if (_busy || _result is not { } result) return;
        // Stream through a unique temporary file rather than duplicating a large CSV in memory.
        string path = Path.Combine(FileSystem.CacheDirectory, "matrix-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            await RunWorkAsync(async (token, progress) =>
            {
                await Task.Run(() =>
                {
                    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
                    MatrixTextCodec.Write(writer, result, token, progress);
                }, token);
                return path;
            }, async temporary =>
            {
                int version = _version;
                await using var stream = File.OpenRead(temporary);
                var saved = await FileSaver.Default.SaveAsync("Matrix-C.csv", stream, _work!.Token);
                if (!CanPublish(version)) return;
                if (saved.IsSuccessful) Status("Exported");
                else if (saved.Exception is OperationCanceledException) Status("Cancelled");
                else ReportError("Failed");
            }, "Exporting");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* Cache can be reclaimed later if a native picker still holds the file. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
