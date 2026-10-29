using MathSolver.Services.Core;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private void OnClearAll(object? sender, EventArgs e)
    {
        if (_busy || !CanTouchUi || !_active || _a is not { } currentA || _b is not { } currentB) return;
        MatrixDraft? emptyA = null, emptyB = null;
        try
        {
            // Empty drafts use bounded sparse storage even for 16384 x 16384.
            // Stage both before publication so a failed allocation keeps the old inputs.
            emptyA = new MatrixDraft(currentA.Rows, currentA.Columns);
            emptyB = new MatrixDraft(currentB.Rows, currentB.Columns);
            _editorMatrix = -1; // Explicit Clear discards incomplete or invalid pending text.
            CloseMatrixTools();
            _detailsRequested = _technicalRequested = false;
            _detailsDismissed = true;
            _setupPanel.IsVisible = false;
            PublishDrafts(emptyA, emptyB, 0, "ClearedAll");
            emptyA = emptyB = null; // Publication owns the new drafts and disposes old A/B/C.
        }
        catch (Exception error) when (MatrixMemoryGuard.IsMemoryFailure(error))
        { ReportError("MemoryUnavailable"); }
        catch (Exception error) when (IsResultStorageFailure(error))
        { ReportError("StorageUnavailable"); }
        finally { DisposeOutcome(emptyA); DisposeOutcome(emptyB); }
    }
}
