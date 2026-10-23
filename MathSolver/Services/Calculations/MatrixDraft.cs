using System.Globalization;

namespace MathSolver.Services.Core;

public sealed class MatrixDraft : IDisposable
{
    public const int FileBackedThresholdCells = 1024 * 1024;
    private bool[]? _filled;
    private int _missingCount;
    private int _disposed;

    public MatrixDraft(int rows, int columns, bool? fileBacked = null, string? temporaryDirectory = null,
        long cacheLimitBytes = MatrixInputStore.DefaultCacheBytes)
    {
        ValidateShape(rows, columns);
        int cells = rows * columns;
        if (fileBacked ?? cells >= FileBackedThresholdCells)
        {
            MatrixMemoryGuard.EnsureAvailable(MatrixInputStore.EstimateResidentBytes(cells, cacheLimitBytes));
            Matrix = new IntegerMatrix(rows, columns, new MatrixInputStore(cells, temporaryDirectory, cacheLimitBytes));
        }
        else
        {
            MatrixMemoryGuard.EnsureAvailable((long)cells * 9);
            Matrix = new IntegerMatrix(rows, columns);
            _filled = new bool[cells];
            _missingCount = cells;
        }
    }

    public MatrixDraft(IntegerMatrix imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        Matrix = imported;
        try
        {
            if (imported.InputStore is { } store) store.FillMissingWithZero();
            else
            {
                MatrixMemoryGuard.EnsureAvailable(imported.CellCount);
                _filled = new bool[imported.CellCount];
                Array.Fill(_filled, true);
            }
        }
        catch { imported.Dispose(); throw; }
    }

    private MatrixDraft(IntegerMatrix matrix, bool[]? filled, int missingCount)
    { Matrix = matrix; _filled = filled; _missingCount = missingCount; }

    public static long EstimateResidentBytes(int rows, int columns, bool? fileBacked = null,
        long cacheLimitBytes = MatrixInputStore.DefaultCacheBytes)
    {
        ValidateShape(rows, columns);
        int cells = rows * columns;
        return (fileBacked ?? cells >= FileBackedThresholdCells)
            ? MatrixInputStore.EstimateResidentBytes(cells, cacheLimitBytes) : (long)cells * 9;
    }

    private static void ValidateShape(int rows, int columns)
    {
        if (rows is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > MatrixLimits.MaximumDimension) throw new ArgumentOutOfRangeException(nameof(columns));
    }

    public IntegerMatrix Matrix { get; }
    public int Rows => Matrix.Rows;
    public int Columns => Matrix.Columns;
    public int CellCount => Matrix.CellCount;
    public long ResidentBytes => Matrix.ResidentBytes + (_filled?.LongLength ?? 0);
    public int MissingCount => Matrix.InputStore?.MissingCount ?? _missingCount;
    public bool HasAnyValues => MissingCount < CellCount;
    public bool IsComplete => MissingCount == 0;

    public bool TrySetCell(int row, int column, string? text)
    {
        ThrowIfDisposed();
        if ((uint)row >= (uint)Rows || (uint)column >= (uint)Columns) return false;
        if (!TryParseCell(text.AsSpan(), out long? value)) return false;
        SetValue(row * Columns + column, value);
        return true;
    }

    /// <summary>Clears a validated rectangle in place without copying the matrix or changing its dimensions.</summary>
    public bool TryClearCells(int row, int column, int rows, int columns)
    {
        ThrowIfDisposed();
        if (row < 0 || column < 0 || rows < 1 || columns < 1 || row > Rows - rows || column > Columns - columns)
            return false;
        if (columns == Columns) ClearCells(row * Columns, rows * Columns);
        else for (int r = row; r < row + rows; r++) ClearCells(r * Columns + column, columns);
        return true;
    }

    private void ClearCells(int start, int count)
    {
        if (Matrix.InputStore is { } store) { store.Fill(start, count, null); return; }
        _missingCount += _filled.AsSpan(start, count).Count(true);
        Array.Clear(Matrix.Data, start, count);
        Array.Clear(_filled!, start, count);
    }

    internal static bool TryParseCell(ReadOnlySpan<char> token, out long? value)
    {
        value = null;
        token = token.Trim();
        if (token.IsEmpty) return true;
        var digits = token;
        if (digits[0] is '+' or '-') digits = digits[1..];
        if (digits.IsEmpty) return false;
        foreach (var character in digits)
            if (character is < '0' or > '9') return false;
        if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)) return false;
        value = number;
        return true;
    }

    private void SetValue(int index, long? value)
    {
        if (Matrix.InputStore is { } store) { store.Set(index, value); return; }
        bool filled = value.HasValue;
        if (_filled![index] != filled) _missingCount += filled ? -1 : 1;
        _filled[index] = filled;
        Matrix.Data[index] = value ?? 0;
    }

    /// <summary>Stages a paste atomically, retaining cells outside the destination rectangle.</summary>
    public MatrixDraft WithPastedCells(int row, int column, MatrixClipboardBlock block, CancellationToken token = default,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        return WithPastedCells(row, column, block.Rows, block.Columns, block, token, progress);
    }

    /// <summary>A single clipboard cell fills the destination rectangle; multi-cell blocks retain their shape.</summary>
    public MatrixDraft WithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block, CancellationToken token = default,
        IProgress<double>? progress = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(block);
        token.ThrowIfCancellationRequested();
        bool singleCell = block.Rows == 1 && block.Columns == 1;
        if (!singleCell && (rows != block.Rows || columns != block.Columns))
            throw new MatrixInputException(MatrixInputError.InvalidShape, row + 1, column + 1);
        if (rows < 1 || columns < 1 || row < 0 || column < 0 || row > Rows - rows || column > Columns - columns)
            throw new MatrixInputException(MatrixInputError.OutsideBounds, row + 1, column + 1);
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
            if (singleCell && columns == Columns && pasted.Matrix.InputStore is { } entireRows)
            {
                token.ThrowIfCancellationRequested();
                entireRows.Fill(row * Columns, rows * Columns, block.Values[0]);
            }
            else for (int r = 0; r < rows; r++)
            {
                token.ThrowIfCancellationRequested();
                if (singleCell && pasted.Matrix.InputStore is { } target)
                    target.Fill((row + r) * Columns + column, columns, block.Values[0]);
                else for (int c = 0; c < columns; c++)
                    pasted.SetValue((row + r) * Columns + column + c, block.Values[singleCell ? 0 : r * block.Columns + c]);
            }
            token.ThrowIfCancellationRequested();
            return pasted;
        }
        catch { pasted?.Dispose(); throw; }
    }

    public string GetCellText(int row, int column)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
        int index = row * Columns + column;
        bool filled = Matrix.InputStore?.IsFilled(index) ?? _filled![index];
        return filled ? Matrix.GetValue(row, column).ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public void FillMissingWithZero()
    {
        ThrowIfDisposed();
        if (Matrix.InputStore is { } store) { store.FillMissingWithZero(); return; }
        for (var index = 0; index < CellCount; index++)
        {
            if (_filled![index]) continue;
            Matrix.Data[index] = 0;
            _filled[index] = true;
        }
        _missingCount = 0;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { Matrix.Dispose(); }
        finally { _filled = null; }
    }
}
