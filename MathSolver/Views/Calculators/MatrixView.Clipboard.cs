using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private async void OnCopySelection(object? sender, EventArgs e)
    {
        if (_busy || !CanTouchUi || !_active || !_viewport.FinishEdit()) return;
        await CopySelectionAsync();
    }

    private async Task CopySelectionAsync()
    {
        if (_busy || !CanTouchUi || !_active) return;
        var selection = _viewport.Selection;
        char matrixName = "ABC"[_selected];
        int rows = selection.Rows, columns = selection.Columns;
        Func<int, int, string> read;
        if (_selected == 2 && _result is { } result)
        {
            read = (r, c) => result.GetValue(r, c).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var draft = CurrentDraft;
            read = draft.GetCellText;
        }
        var source = read;
        read = (r, c) => source(selection.Row + r, selection.Column + c);
        bool exportCsv = false;
        int sourceVersion = _version;
        // Input is locked during serialization. Off-screen cells come from the model,
        // and cancellation/tab changes prevent publishing a stale copy.
        await RunWorkAsync((token, progress) => Task.Run(() => MatrixClipboardCodec.Create(rows, columns, read,
#if WINDOWS
            includeHtml: true,
#endif
            token: token, progress: progress), token), async content =>
        {
            int version = _version;
            if (_work?.IsCancellationRequested != false || !CanPublish(version)) return false;
            if (content is null)
            {
                // Capacity is an expected outcome, not an input error at row 1 / column 1.
                // Leave the current clipboard intact and offer a streaming export of A, B or C.
                Status("CopyTooLarge");
                bool accepted = await ConfirmAsync(F("CopyTooLargePrompt", rows, columns), "ExportSelection");
                if (_work?.IsCancellationRequested != false || !CanPublish(version)) return false;
                exportCsv = accepted;
                sourceVersion = version;
                if (!accepted) Status("Cancelled");
                return true;
            }
            await MatrixClipboardService.SetAsync(content);
            if (CanPublish(version)) Status("CopiedGrid");
            return true;
        }, "CopyingGrid");
        if (exportCsv && CanPublish(sourceVersion) && !_busy)
            await ExportCsvAsync(rows, columns, read, $"Matrix-{matrixName}-selection.csv");
    }

    private async void OnPasteCells(object? sender, EventArgs e)
    {
        if (_busy || !CanTouchUi || !_active || _selected == 2 || !_viewport.FinishEdit()) return;
        int target = _selected;
        var destination = _viewport.Selection;
        var original = CurrentDraft;
        await RunWorkAsync(async (token, progress) =>
        {
            string? text = await Clipboard.Default.GetTextAsync();
            token.ThrowIfCancellationRequested();
            var value = await Task.Run(() =>
            {
                var block = MatrixClipboardCodec.Read(text ?? string.Empty, token, InputDimensionLimit);
                bool singleCell = block.Rows == 1 && block.Columns == 1;
                int rows = singleCell ? destination.Rows : block.Rows;
                int columns = singleCell ? destination.Columns : block.Columns;
                var attempt = original.TryWithPastedCells(destination.Row, destination.Column, rows, columns, block, token);
                return new PastedCells(attempt, rows, columns);
            }, token);
            progress.Report(1);
            return value;
        }, pasted =>
        {
            if (pasted.Attempt.Draft is not { } draft)
            {
                const double bytesPerGiB = 1024d * 1024 * 1024;
                ReportError("PasteMemoryUnavailable", pasted.Attempt.Memory.RequiredBytes / bytesPerGiB,
                    pasted.Attempt.Memory.AvailableBytes / bytesPerGiB);
                return Task.FromResult(false);
            }
            PublishDrafts(target == 0 ? draft : _a, target == 1 ? draft : _b, target, "PastedCells",
                () => _viewport.SelectRange(destination.Row, destination.Column, pasted.Rows, pasted.Columns));
            return Task.FromResult(true);
        }, "PastingCells");
    }

    private sealed record PastedCells(MatrixPasteAttempt Attempt, int Rows, int Columns) : IDisposable
    {
        public void Dispose() => Attempt.Dispose();
    }
}
