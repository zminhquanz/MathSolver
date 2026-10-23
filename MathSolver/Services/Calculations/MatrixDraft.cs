using System.Globalization;

namespace MathSolver.Services.Core;

/// <summary>Owns a staged draft until the caller retains it; disposing an unpublished attempt releases its files.</summary>
public readonly record struct MatrixPasteAttempt(MatrixDraft? Draft, MatrixMemoryCheck Memory) : IDisposable
{
    public void Dispose() => Draft?.Dispose();
}

public sealed class MatrixDraft : IDisposable
{
    public const int FileBackedThresholdCells = 1024 * 1024;
    private MatrixInputStorage Storage => Matrix.Storage!;
    private bool[]? _filled;
    private int _missingCount;
    private int _disposed;
    public MatrixDraft(int rows, int columns, bool? fileBacked = null, string? temporaryDirectory = null,
        long cacheLimitBytes = MatrixInputStore.DefaultCacheBytes)
    {
        ValidateShape(rows, columns);
        MatrixMemoryGuard.EnsureAvailable(EstimateResidentBytes(rows, columns, fileBacked, cacheLimitBytes));
        int cells = rows * columns;
        if (fileBacked is true)
            Matrix = new IntegerMatrix(rows, columns, new MatrixInputStore(cells, temporaryDirectory, cacheLimitBytes));
        else if (fileBacked is false)
        {
            Matrix = new IntegerMatrix(rows, columns);
            _filled = new bool[cells]; _missingCount = cells;
        }
        else Matrix = new IntegerMatrix(new MatrixInputStorage(rows, columns, temporaryDirectory, cacheLimitBytes));
    }

    public MatrixDraft(int rows, int columns, string? directory, long cacheLimitBytes = MatrixInputStore.DefaultCacheBytes)
        : this(rows, columns, null, directory, cacheLimitBytes) { }

    public static long EstimateResidentBytes(int rows, int columns, bool? fileBacked = null,
        long cacheLimitBytes = MatrixInputStore.DefaultCacheBytes)
    {
        ValidateShape(rows, columns);
        ArgumentOutOfRangeException.ThrowIfNegative(cacheLimitBytes);
        return fileBacked is true ? MatrixInputStore.EstimateResidentBytes(rows * columns, cacheLimitBytes)
            : fileBacked is false ? (long)rows * columns * 9 : MatrixInputStorage.EmptyResidentBytes;
    }

    private static void ValidateShape(int rows, int columns)
    {
        if (rows is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(columns));
    }

    /// <summary>Takes ownership of the imported matrix, including on construction failure.</summary>
    public MatrixDraft(IntegerMatrix imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        try
        {
            Matrix = imported;
            if (imported.Storage is not null) return;
            if (imported.InputStore is { } store) store.FillMissingWithZero();
            else
            {
                MatrixMemoryGuard.EnsureAvailable(imported.CellCount);
                _filled = new bool[imported.CellCount]; Array.Fill(_filled, true);
            }
        }
        catch { imported.Dispose(); throw; }
    }
    private MatrixDraft(MatrixInputStorage source)
    {
        var snapshot = source.Snapshot();
        try { Matrix = new IntegerMatrix(snapshot); }
        catch { snapshot.Dispose(); throw; }
    }
    private MatrixDraft(IntegerMatrix matrix, bool[]? filled, int missingCount)
    { Matrix = matrix; _filled = filled; _missingCount = missingCount; }
    public IntegerMatrix Matrix { get; }
    public int Rows => Matrix.Rows;
    public int Columns => Matrix.Columns;
    public int CellCount => Matrix.CellCount;
    public long ResidentBytes => Matrix.ResidentBytes + (_filled?.LongLength ?? 0);
    public int MissingCount => Matrix.InputStore?.MissingCount ?? Matrix.Storage?.MissingCount ?? _missingCount;
    public bool HasAnyValues => MissingCount < Rows * Columns;
    public bool IsComplete => MissingCount == 0;

    public bool TrySetCell(int row, int column, string? text)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows || (uint)column >= (uint)Columns) return false;
        if (!TryParseCell(text.AsSpan(), out long? value)) return false;
        SetValue(row * Columns + column, value);
        return true;
    }
    private void SetValue(int index, long? value)
    {
        if (Matrix.Storage is { } storage) { storage.SetCell(index, value); return; }
        if (Matrix.InputStore is { } store) { store.Set(index, value); return; }
        if (_filled![index] != value.HasValue) _missingCount += value.HasValue ? -1 : 1;
        _filled[index] = value.HasValue; Matrix.Data[index] = value ?? 0;
    }

    /// <summary>Clears a validated rectangle in place without replacing its draft.</summary>
    public bool TryClearCells(int row, int column, int rows, int columns)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (row < 0 || column < 0 || rows < 1 || columns < 1 || row > Rows - rows || column > Columns - columns)
            return false;
        if (Matrix.Storage is { } storage)
        {
            if (row == 0 && column == 0 && rows == Rows && columns == Columns) { storage.Fill(null); return true; }
            int last = -1;
            for (int r = row; r < row + rows; r++)
            {
                int first = (r * Columns + column) / MatrixInputStorage.BlockCells;
                int end = (r * Columns + column + columns - 1) / MatrixInputStorage.BlockCells;
                for (int key = Math.Max(first, last + 1); key <= end; key++)
                    storage.EditBlock(key, (values, bits, start, count) =>
                    {
                        int firstRow = Math.Max(row, start / Columns);
                        int finalRow = Math.Min(row + rows - 1, (start + count - 1) / Columns);
                        for (int targetRow = firstRow; targetRow <= finalRow; targetRow++)
                        {
                            int from = Math.Max(start, targetRow * Columns + column);
                            int to = Math.Min(start + count, targetRow * Columns + column + columns);
                            for (int index = from; index < to; index++)
                            { values[index - start] = 0; MatrixInputStorage.SetBit(bits, index - start, false); }
                        }
                    });
                last = end;
            }
        }
        else if (columns == Columns) ClearValues(row * Columns, rows * Columns);
        else for (int r = row; r < row + rows; r++) ClearValues(r * Columns + column, columns);
        return true;
    }

    private void ClearValues(int start, int count)
    {
        if (Matrix.InputStore is { } store) { store.Fill(start, count, null); return; }
        _missingCount += _filled.AsSpan(start, count).Count(true);
        Array.Clear(Matrix.Data, start, count); Array.Clear(_filled!, start, count);
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
    public MatrixDraft WithPastedCells(int row, int column, MatrixClipboardBlock block, CancellationToken token = default,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        return WithPastedCells(row, column, block.Rows, block.Columns, block, token, progress);
    }
    public MatrixDraft WithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block, CancellationToken token = default,
        IProgress<double>? progress = null)
    {
        var attempt = TryWithPastedCells(row, column, rows, columns, block, token, progress);
        return attempt.Draft ?? throw new MatrixMemoryException(attempt.Memory.RequiredBytes, attempt.Memory.AvailableBytes);
    }
    public MatrixPasteAttempt TryWithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block,
        CancellationToken token = default, IProgress<double>? progress = null) =>
        TryWithPastedCells(row, column, rows, columns, block, token, MatrixMemoryGuard.CheckAvailable, progress);

    internal MatrixPasteAttempt TryWithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block,
        CancellationToken token, Func<long, MatrixMemoryCheck> checkMemory, IProgress<double>? progress = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(block);
        token.ThrowIfCancellationRequested();
        bool scalar = block.Rows == 1 && block.Columns == 1;
        if (!scalar && (rows != block.Rows || columns != block.Columns))
            throw new MatrixInputException(MatrixInputError.InvalidShape, row + 1, column + 1);
        if (rows < 1 || columns < 1 || row < 0 || column < 0 || row > Rows - rows || column > Columns - columns)
            throw new MatrixInputException(MatrixInputError.OutsideBounds, row + 1, column + 1);
        if (Matrix.Storage is null)
            return PasteLegacy(row, column, rows, columns, block, scalar, token, checkMemory, progress);
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
                int completed = 0;
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
                        progress?.Report((double)++completed / touched);
                    }
                    last = end;
                }
            }
            progress?.Report(1);
            token.ThrowIfCancellationRequested();
            return new(pasted, memory);
        }
        catch { pasted.Dispose(); throw; }
    }

    private MatrixPasteAttempt PasteLegacy(int row, int column, int rows, int columns, MatrixClipboardBlock block,
        bool scalar, CancellationToken token, Func<long, MatrixMemoryCheck> checkMemory, IProgress<double>? progress)
    {
        long required = Matrix.InputStore is { } source
            ? MatrixInputStore.EstimateResidentBytes(CellCount, source.CacheLimitBytes) : (long)CellCount * 9;
        var memory = checkMemory(required);
        token.ThrowIfCancellationRequested();
        if (!memory.CanAllocate) return new(null, memory);
        MatrixDraft? pasted = null;
        try
        {
            if (Matrix.InputStore is { } store)
                pasted = new MatrixDraft(new IntegerMatrix(Rows, Columns, store.Clone(token, progress)), null, 0);
            else
            {
                pasted = new MatrixDraft(Rows, Columns, fileBacked: false);
                Array.Copy(Matrix.Data, pasted.Matrix.Data, CellCount);
                Array.Copy(_filled!, pasted._filled!, CellCount);
                pasted._missingCount = _missingCount;
            }
            if (scalar && columns == Columns && pasted.Matrix.InputStore is { } entireRows)
            {
                token.ThrowIfCancellationRequested();
                entireRows.Fill(row * Columns, rows * Columns, block.Values[0]);
            }
            else for (int r = 0; r < rows; r++)
            {
                token.ThrowIfCancellationRequested();
                if (scalar && pasted.Matrix.InputStore is { } target)
                    target.Fill((row + r) * Columns + column, columns, block.Values[0]);
                else for (int c = 0; c < columns; c++)
                    pasted.SetValue((row + r) * Columns + column + c, block.Values[scalar ? 0 : r * block.Columns + c]);
            }
            progress?.Report(1);
            token.ThrowIfCancellationRequested();
            return new(pasted, memory);
        }
        catch { pasted?.Dispose(); throw; }
    }
    public string GetCellText(int row, int column)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
        int index = row * Columns + column;
        bool filled = Matrix.Storage?.IsFilled(index) ?? Matrix.InputStore?.IsFilled(index) ?? _filled![index];
        return filled ? Matrix.GetValue(row, column).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }
    public void FillMissingWithZero()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Matrix.Storage is { } storage) { storage.FillMissing(default); return; }
        if (Matrix.InputStore is { } store) { store.FillMissingWithZero(); return; }
        for (int index = 0; index < CellCount; index++)
            if (!_filled![index]) { Matrix.Data[index] = 0; _filled[index] = true; }
        _missingCount = 0;
    }
    public MatrixDraft WithMissingFilledWithZero(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        token.ThrowIfCancellationRequested();
        MatrixDraft filled;
        if (Matrix.Storage is not null) filled = new MatrixDraft(Storage);
        else if (Matrix.InputStore is { } store)
            filled = new MatrixDraft(new IntegerMatrix(Rows, Columns, store.Clone(token)), null, 0);
        else
        {
            filled = new MatrixDraft(Rows, Columns, fileBacked: false);
            Array.Copy(Matrix.Data, filled.Matrix.Data, CellCount);
            Array.Copy(_filled!, filled._filled!, CellCount); filled._missingCount = _missingCount;
        }
        try
        {
            if (filled.Matrix.Storage is { } storage) storage.FillMissing(token);
            else filled.FillMissingWithZero();
            token.ThrowIfCancellationRequested();
            return filled;
        }
        catch { filled.Dispose(); throw; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { Matrix.Dispose(); }
        finally { _filled = null; }
    }
}
