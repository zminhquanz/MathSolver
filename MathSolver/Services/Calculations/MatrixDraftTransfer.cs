namespace MathSolver.Services.Core;

/// <summary>Owns a staged paste/import until the UI accepts it or discards the outcome.</summary>
internal sealed class MatrixDraftTransfer(MatrixDraft draft, int rows = 0, int columns = 0) : IDisposable
{
    internal MatrixDraft? Draft { get; private set; } = draft;
    internal int Rows { get; } = rows;
    internal int Columns { get; } = columns;
    internal void Detach() => Draft = null;
    public void Dispose() { var draft = Draft; Draft = null; draft?.Dispose(); }
}
