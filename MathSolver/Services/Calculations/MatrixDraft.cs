using System.Globalization;

namespace MathSolver.Services.Core;

/// <summary>Owns a staged draft until the caller retains it; disposing an unpublished attempt releases its files.</summary>
public readonly record struct MatrixPasteAttempt(MatrixDraft? Draft, MatrixMemoryCheck Memory) : IDisposable
{
    public void Dispose() => Draft?.Dispose();
}

public sealed class MatrixDraft : IDisposable
{
    private MatrixInputStorage Storage => Matrix.Storage!;
    private int _disposed;
    public MatrixDraft(int rows, int columns, string? temporaryDirectory = null, long cacheLimitBytes = 2 * 1024 * 1024)
    {
        if (rows is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(columns));
        Matrix = new IntegerMatrix(new MatrixInputStorage(rows, columns, temporaryDirectory, cacheLimitBytes));
    }

    /// <summary>Takes ownership of the imported matrix, including on construction failure.</summary>
    public MatrixDraft(IntegerMatrix imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        try
        {
            if (imported.Storage is not null) { Matrix = imported; return; }
            using var builder = new MatrixInputBuilder(imported.Columns);
            for (int row = 0; row < imported.Rows; row++)
                builder.AddRow(imported.DenseData!.AsSpan(row * imported.Columns, imported.Columns));
            Matrix = builder.Complete();
            imported.Dispose();
        }
        catch { imported.Dispose(); throw; }
    }
    private MatrixDraft(MatrixInputStorage source)
    {
        var snapshot = source.Snapshot();
        try { Matrix = new IntegerMatrix(snapshot); }
        catch { snapshot.Dispose(); throw; }
    }
    public IntegerMatrix Matrix { get; }
    public int Rows => Matrix.Rows;
    public int Columns => Matrix.Columns;
    public int MissingCount => Storage.MissingCount;
    public bool HasAnyValues => MissingCount < Rows * Columns;
    public bool IsComplete => MissingCount == 0;

    public bool TrySetCell(int row, int column, string? text)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows || (uint)column >= (uint)Columns) return false;
        if (!TryParseCell(text.AsSpan(), out long? value)) return false;
        Storage.SetCell(row * Columns + column, value);
        return true;
    }
    internal static bool TryParseCell(ReadOnlySpan<char> token, out long? value)
    {
        value = null; token = token.Trim();
        if (token.IsEmpty) return true;
        var digits = token;
        if (digits[0] is '+' or '-') digits = digits[1..];
        if (digits.IsEmpty) return false;
        foreach (var character in digits) if (character is < '0' or > '9') return false;
        if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) return false;
        value = number; return true;
    }
    public MatrixDraft WithPastedCells(int row, int column, MatrixClipboardBlock block, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(block);
        return WithPastedCells(row, column, block.Rows, block.Columns, block, token);
    }
    public MatrixDraft WithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block, CancellationToken token = default)
    {
        var attempt = TryWithPastedCells(row, column, rows, columns, block, token);
        return attempt.Draft ?? throw new MatrixMemoryException(attempt.Memory.RequiredBytes, attempt.Memory.AvailableBytes);
    }
    public MatrixPasteAttempt TryWithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block, CancellationToken token = default) =>
        TryWithPastedCells(row, column, rows, columns, block, token, MatrixMemoryGuard.CheckAvailable);

    internal MatrixPasteAttempt TryWithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block,
        CancellationToken token, Func<long, MatrixMemoryCheck> checkMemory)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(block);
        token.ThrowIfCancellationRequested();
        bool scalar = block.Rows == 1 && block.Columns == 1;
        if (!scalar && (rows != block.Rows || columns != block.Columns))
            throw new MatrixInputException(MatrixInputError.InvalidShape, row + 1, column + 1);
        if (rows < 1 || columns < 1 || row < 0 || column < 0 || row > Rows - rows || column > Columns - columns)
            throw new MatrixInputException(MatrixInputError.OutsideBounds, row + 1, column + 1);
        bool whole = scalar && row == 0 && column == 0 && rows == Rows && columns == Columns;
        int touched = 0, last = -1;
        if (!whole)
            for (int r = row; r < row + rows; r++)
            {
                int first = (r * Columns + column) / MatrixInputStorage.BlockCells;
                int end = (r * Columns + column + columns - 1) / MatrixInputStorage.BlockCells;
                touched += end - Math.Max(first, last + 1) + 1; last = end;
            }
        long required = whole ? 512 : 512 + (long)touched * 256 + 2L * MatrixInputStorage.BlockBytes
            + Math.Min((long)touched * MatrixInputStorage.BlockBytes, Matrix.CacheLimitBytes);
        var memory = checkMemory(required);
        token.ThrowIfCancellationRequested();
        if (!memory.CanAllocate) return new(null, memory);
        var pasted = new MatrixDraft(Storage);
        try
        {
            if (whole) pasted.Storage.Fill(block.Values[0]);
            else
            {
                last = -1;
                for (int r = row; r < row + rows; r++)
                {
                    int first = (r * Columns + column) / MatrixInputStorage.BlockCells;
                    int end = (r * Columns + column + columns - 1) / MatrixInputStorage.BlockCells;
                    for (int key = Math.Max(first, last + 1); key <= end; key++)
                    {
                        token.ThrowIfCancellationRequested();
                        int start = key * MatrixInputStorage.BlockCells;
                        int stop = Math.Min(start + MatrixInputStorage.BlockCells, Rows * Columns);
                        bool fullyCovered = start / Columns >= row && (stop - 1) / Columns < row + rows
                            && (columns == Columns || start / Columns == (stop - 1) / Columns
                                && start % Columns >= column && (stop - 1) % Columns < column + columns);
                        if (scalar && fullyCovered) pasted.Storage.SetConstantBlock(key, block.Values[0]);
                        else pasted.Storage.EditBlock(key, (values, bits, blockStart, count) =>
                        {
                            int firstRow = Math.Max(row, blockStart / Columns);
                            int finalRow = Math.Min(row + rows - 1, (blockStart + count - 1) / Columns);
                            for (int targetRow = firstRow; targetRow <= finalRow; targetRow++)
                            {
                                int from = Math.Max(blockStart, targetRow * Columns + column);
                                int to = Math.Min(blockStart + count, targetRow * Columns + column + columns);
                                for (int index = from; index < to; index++)
                                {
                                    long? value = block.Values[scalar ? 0 : (targetRow - row) * columns + index % Columns - column];
                                    values[index - blockStart] = value ?? 0;
                                    MatrixInputStorage.SetBit(bits, index - blockStart, value.HasValue);
                                }
                            }
                        });
                    }
                    last = end;
                }
            }
            token.ThrowIfCancellationRequested();
            return new(pasted, memory);
        }
        catch { pasted.Dispose(); throw; }
    }
    public string GetCellText(int row, int column)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
        int index = row * Columns + column;
        return Storage.IsFilled(index) ? Storage.GetValue(index).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }
    public void FillMissingWithZero() => Storage.FillMissing(default);
    public MatrixDraft WithMissingFilledWithZero(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var filled = new MatrixDraft(Storage);
        try { filled.Storage.FillMissing(token); return filled; }
        catch { filled.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) Matrix.Dispose();
    }
}
