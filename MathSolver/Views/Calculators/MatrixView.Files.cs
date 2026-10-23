using CommunityToolkit.Maui.Storage;
using MathSolver.Services.Core;
using System.Text;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private async void OnPaste(object? sender, EventArgs e)
    {
        if (_busy || !HasInputs || _selected == 2) return;
        int target = _selected, version = _version;
        try
        {
            string? text = await Clipboard.Default.GetTextAsync();
            if (!CanPublish(version) || _busy || _selected != target) return;
            if (string.IsNullOrWhiteSpace(text)) { ReportError("ClipboardEmpty"); return; }
            // Clipboard size is bounded separately; file imports use a streaming reader.
            if (text.Length > MatrixLimits.MaximumClipboardCharacters) { ReportError("Error.ClipboardTooLarge"); return; }
            await RunWorkAsync((token, _) => Task.Run(() =>
            {
                using var reader = new StringReader(text);
                return new MatrixDraftTransfer(ReadImportedDraft(reader, token));
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
        if (_busy || !HasInputs || _selected == 2) return;
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
                return new MatrixDraftTransfer(ReadImportedDraft(reader, token));
            }, token), matrix => AcceptImportAsync(target, matrix), "Importing");
        }
        catch (OperationCanceledException) { /* Native picker cancelled; existing drafts are retained. */ }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
            if (CanPublish(version)) ReportError("Failed");
        }
    }

    private async Task AcceptImportAsync(int target, MatrixDraftTransfer transfer)
    {
        using var owned = transfer;
        var matrix = transfer.Draft ?? throw new InvalidOperationException("Import ownership has already transferred.");
        int version = _version;
        if (!await ConfirmAsync(F("AcceptImport", "AB"[target], matrix.Rows, matrix.Columns)))
        { if (CanPublish(version)) Status("Cancelled"); return; }
        if (!CanPublish(version)) return;
        if (_work?.IsCancellationRequested != false) { Status("Cancelled"); return; }
        PublishDrafts(target == 0 ? matrix : _a, target == 1 ? matrix : _b, target, "Imported");
        transfer.Detach();
    }

    private static MatrixDraft ReadImportedDraft(TextReader reader, CancellationToken token)
    {
        string directory = Path.Combine(FileSystem.CacheDirectory, "matrix-inputs");
        Directory.CreateDirectory(directory);
        return new MatrixDraft(MatrixTextCodec.Read(reader, token, directory, maximumDimension: InputDimensionLimit));
    }

    private async void OnExport(object? sender, EventArgs e)
    {
        if (_busy || _result is not { } result) return;
        await ExportCsvAsync(result.Rows, result.Columns,
            (r, c) => result.GetValue(r, c).ToString(System.Globalization.CultureInfo.InvariantCulture), "Matrix-C.csv");
    }

    private async Task ExportCsvAsync(int rows, int columns, Func<int, int, string> readCell, string fileName)
    {
        // Stream through a unique temporary file rather than duplicating a large CSV in memory.
        string path = Path.Combine(FileSystem.CacheDirectory, "matrix-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            await RunWorkAsync(async (token, progress) =>
            {
                await Task.Run(() =>
                {
                    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
                    MatrixTextCodec.Write(writer, rows, columns, readCell, token, progress);
                }, token);
                return path;
            }, async temporary =>
            {
                int version = _version;
                await using var stream = File.OpenRead(temporary);
                var saved = await FileSaver.Default.SaveAsync(fileName, stream, _work!.Token);
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
