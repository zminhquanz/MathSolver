using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private async void OnCopyGrid(object? sender, EventArgs e)
    {
        if (_busy || !CanTouchUi || !_active || !_viewport.SelectAll()) return;
        await CopySelectionAsync();
    }

    private async void OnCopySelection(object? sender, EventArgs e) => await CopySelectionAsync();

    private async Task CopySelectionAsync()
    {
        if (_busy || !CanTouchUi || !_active) return;
        bool all = _viewport.AllSelected;
        int row = _viewport.SelectedRow, column = _viewport.SelectedColumn;
        int rows, columns;
        Func<int, int, string> read;
        if (_selected == 2 && _result is { } result)
        {
            rows = result.Rows; columns = result.Columns;
            read = (r, c) => result.GetValue(r, c).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var draft = CurrentDraft;
            rows = draft.Rows; columns = draft.Columns;
            read = draft.GetCellText;
        }
        var source = read;
        if (!all) { rows = columns = 1; read = (_, _) => source(row, column); }
        // Input is locked during serialization. Off-screen cells come from the model,
        // and cancellation/tab changes prevent publishing a stale copy.
        await RunWorkAsync((token, progress) => Task.Run(() => MatrixClipboardCodec.Create(rows, columns, read,
#if WINDOWS
            includeHtml: true,
#endif
            token: token, progress: progress), token), async content =>
        {
            int version = _version;
            if (_work?.IsCancellationRequested != false || !CanPublish(version)) return;
            await MatrixClipboardService.SetAsync(content);
            if (CanPublish(version)) Status("CopiedGrid");
        }, "CopyingGrid");
    }
}
