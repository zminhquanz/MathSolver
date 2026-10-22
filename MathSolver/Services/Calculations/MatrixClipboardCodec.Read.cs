namespace MathSolver.Services.Core;

public sealed class MatrixClipboardBlock
{
    internal MatrixClipboardBlock(int rows, int columns, long?[] values)
    { Rows = rows; Columns = columns; Values = values; }
    public int Rows { get; }
    public int Columns { get; }
    internal long?[] Values { get; }
}

public static partial class MatrixClipboardCodec
{
    /// <summary>Reads Excel-style TSV, retaining empty cells and rejecting non-Int64 input.</summary>
    public static MatrixClipboardBlock Read(string text, CancellationToken token = default,
        int maximumDimension = MatrixLimits.MaximumDimension)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDimension, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumDimension, MatrixLimits.MaximumDimension);
        token.ThrowIfCancellationRequested();
        if (text.Length > MatrixLimits.MaximumClipboardCharacters) throw new MatrixInputException(MatrixInputError.ClipboardTooLarge, 1, 1);
        using var reader = new StringReader(text);
        var values = new List<long?>();
        int rows = 0, columns = 0;
        while (reader.ReadLine() is { } line)
        {
            token.ThrowIfCancellationRequested();
            if (rows == maximumDimension) throw new MatrixInputException(MatrixInputError.TooLarge, rows + 1, 1);
            int count = 0;
            var remaining = line.AsSpan();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (count == maximumDimension) throw new MatrixInputException(MatrixInputError.TooLarge, rows + 1, count + 1);
                int separator = remaining.IndexOf('\t');
                var cell = separator < 0 ? remaining : remaining[..separator];
                if (!MatrixDraft.TryParseCell(cell, out long? value))
                    throw new MatrixInputException(MatrixInputError.InvalidInteger, rows + 1, count + 1);
                if (values.Count == values.Capacity)
                {
                    int capacity = Math.Max(4, values.Capacity * 2);
                    MatrixMemoryGuard.EnsureAvailable((long)capacity * 16);
                    values.EnsureCapacity(capacity);
                }
                values.Add(value); count++;
                if (separator < 0) break;
                remaining = remaining[(separator + 1)..];
            }
            if (rows > 0 && columns != count) throw new MatrixInputException(MatrixInputError.InvalidShape, rows + 1, count);
            columns = count; rows++;
        }
        token.ThrowIfCancellationRequested();
        if (rows == 0) throw new MatrixInputException(MatrixInputError.Empty, 1, 1);
        MatrixMemoryGuard.EnsureAvailable((long)values.Count * 16);
        return new(rows, columns, values.ToArray());
    }
}
