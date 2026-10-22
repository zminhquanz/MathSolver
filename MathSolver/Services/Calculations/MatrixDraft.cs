using System.Globalization;

namespace MathSolver.Services.Core;

public readonly record struct MatrixPasteAttempt(MatrixDraft? Draft, MatrixMemoryCheck Memory);

public sealed class MatrixDraft
{
    private readonly bool[] _filled;

    public MatrixDraft(int rows, int columns)
    {
        if (rows is >= 1 and <= MatrixLimits.MaximumDimension && columns is >= 1 and <= MatrixLimits.MaximumDimension)
            MatrixMemoryGuard.EnsureAvailable((long)rows * columns * 9);
        Matrix = new IntegerMatrix(rows, columns);
        _filled = new bool[Matrix.Data.Length];
        MissingCount = _filled.Length;
    }

    public MatrixDraft(IntegerMatrix imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        MatrixMemoryGuard.EnsureAvailable(imported.Data.LongLength);
        Matrix = imported;
        _filled = new bool[imported.Data.Length];
        Array.Fill(_filled, true);
    }

    private MatrixDraft(IntegerMatrix matrix, bool[] filled, int missingCount)
    {
        Matrix = matrix;
        _filled = filled;
        MissingCount = missingCount;
    }

    public IntegerMatrix Matrix { get; }
    public int Rows => Matrix.Rows;
    public int Columns => Matrix.Columns;
    public int MissingCount { get; private set; }
    public bool HasAnyValues => MissingCount < _filled.Length;
    public bool IsComplete => MissingCount == 0;

    public bool TrySetCell(int row, int column, string? text)
    {
        if ((uint)row >= (uint)Rows || (uint)column >= (uint)Columns)
            return false;
        var index = row * Columns + column;
        if (!TryParseCell(text.AsSpan(), out long? value)) return false;
        SetValue(index, value);
        return true;
    }

    internal static bool TryParseCell(ReadOnlySpan<char> token, out long? value)
    {
        value = null;
        token = token.Trim();
        if (token.IsEmpty)
            return true;

        var digits = token;
        if (digits[0] is '+' or '-') digits = digits[1..];
        if (digits.IsEmpty) return false;
        foreach (var character in digits)
            if (character is < '0' or > '9') return false;
        if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            return false;
        value = number;
        return true;
    }

    private void SetValue(int index, long? value)
    {
        bool filled = value.HasValue;
        if (_filled[index] != filled) MissingCount += filled ? -1 : 1;
        _filled[index] = filled;
        Matrix.Data[index] = value ?? 0;
    }

    /// <summary>Stages a paste atomically, retaining cells outside the destination rectangle.</summary>
    public MatrixDraft WithPastedCells(int row, int column, MatrixClipboardBlock block, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(block);
        return WithPastedCells(row, column, block.Rows, block.Columns, block, token);
    }

    /// <summary>A single clipboard cell fills the destination rectangle; multi-cell blocks retain their shape.</summary>
    public MatrixDraft WithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block, CancellationToken token = default)
    {
        var attempt = TryWithPastedCells(row, column, rows, columns, block, token);
        return attempt.Draft ?? throw new MatrixMemoryException(attempt.Memory.RequiredBytes, attempt.Memory.AvailableBytes);
    }

    /// <summary>Returns an expected memory refusal before cloning; the source is unchanged on refusal or cancellation.</summary>
    public MatrixPasteAttempt TryWithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block, CancellationToken token = default) =>
        TryWithPastedCells(row, column, rows, columns, block, token, MatrixMemoryGuard.CheckAvailable);

    // Keep machine measurement at the boundary so low-memory outcomes can be
    // exercised without allocating multiple gigabytes or exhausting the test host.
    internal MatrixPasteAttempt TryWithPastedCells(int row, int column, int rows, int columns, MatrixClipboardBlock block,
        CancellationToken token, Func<long, MatrixMemoryCheck> checkMemory)
    {
        ArgumentNullException.ThrowIfNull(block);
        token.ThrowIfCancellationRequested();
        bool singleCell = block.Rows == 1 && block.Columns == 1;
        if (!singleCell && (rows != block.Rows || columns != block.Columns))
            throw new MatrixInputException(MatrixInputError.InvalidShape, row + 1, column + 1);
        if (rows < 1 || columns < 1 || row < 0 || column < 0 || row > Rows - rows || column > Columns - columns)
            throw new MatrixInputException(MatrixInputError.OutsideBounds, row + 1, column + 1);
        var memory = checkMemory(Matrix.Data.LongLength * (sizeof(long) + sizeof(bool)));
        token.ThrowIfCancellationRequested();
        if (!memory.CanAllocate) return new(null, memory);

        // The single preflight budgets both buffers. Do not re-enter the throwing
        // draft constructor after an expected refusal has already been checked.
        // Actual allocation failures still propagate to the UI's memory handler.
        var values = (long[])Matrix.Data.Clone();
        token.ThrowIfCancellationRequested();
        var filled = (bool[])_filled.Clone();
        token.ThrowIfCancellationRequested();
        var pasted = new MatrixDraft(new IntegerMatrix(Rows, Columns, values), filled, MissingCount);
        for (int r = 0; r < rows; r++)
        {
            token.ThrowIfCancellationRequested();
            for (int c = 0; c < columns; c++)
                pasted.SetValue((row + r) * Columns + column + c, block.Values[singleCell ? 0 : r * block.Columns + c]);
        }
        token.ThrowIfCancellationRequested();
        return new(pasted, memory);
    }

    public string GetCellText(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
        var index = row * Columns + column;
        return _filled[index] ? Matrix.Data[index].ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public void FillMissingWithZero()
    {
        for (var index = 0; index < _filled.Length; index++)
        {
            if (_filled[index]) continue;
            Matrix.Data[index] = 0;
            _filled[index] = true;
        }
        MissingCount = 0;
    }
}
