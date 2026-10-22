using System.Globalization;

namespace MathSolver.Services.Core;

public enum MatrixInputError { InvalidInteger, InvalidShape, TooLarge, Empty, OutsideBounds, ClipboardTooLarge }

public sealed class MatrixInputException(MatrixInputError code, int row, int column)
    : FormatException($"{code} at row {row}, column {column}.")
{
    public MatrixInputError Code { get; } = code;
    public int Row { get; } = row;
    public int Column { get; } = column;
}

public static class MatrixTextCodec
{
    private const int MaximumDimension = MatrixLimits.MaximumDimension;

    /// <summary>Stages a complete, strictly rectangular integer table before returning it.</summary>
    public static IntegerMatrix Read(TextReader reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        cancellationToken.ThrowIfCancellationRequested();
        var values = new List<long>();
        var rowValues = new List<long>();
        var row = 1;
        var columns = 0;
        var rows = 0;
        var firstBlankRow = 0;
        char? delimiter = null;
        var hasContent = false;
        var skipLineFeed = false;
        var cell = new IntegerCell();

        MatrixInputException Error(MatrixInputError code, int? errorColumn = null) =>
            new(code, row, errorColumn ?? rowValues.Count + 1);

        void BeginContent()
        {
            if (hasContent) return;
            if (firstBlankRow != 0)
                throw new MatrixInputException(MatrixInputError.InvalidShape, firstBlankRow, 1);
            if (rows == MaximumDimension)
                throw Error(MatrixInputError.TooLarge, 1);
            hasContent = true;
        }

        void FinishCell()
        {
            if (!cell.TryGetValue(out var value)) throw Error(MatrixInputError.InvalidInteger);
            rowValues.Add(value);
            cell = new IntegerCell();
        }

        void FinishRow()
        {
            if (!hasContent)
            {
                if (firstBlankRow == 0) firstBlankRow = row;
            }
            else
            {
                FinishCell();
                if (columns == 0) columns = rowValues.Count;
                else if (columns != rowValues.Count)
                    throw Error(MatrixInputError.InvalidShape, rowValues.Count + 1);
                int needed = checked(values.Count + rowValues.Count);
                if (needed > values.Capacity)
                {
                    int capacity = Math.Min(MaximumDimension * MaximumDimension,
                        Math.Max(needed, Math.Max(4, values.Capacity * 2)));
                    MatrixMemoryGuard.EnsureAvailable((long)capacity * sizeof(long));
                    values.Capacity = capacity;
                }
                values.AddRange(rowValues);
                rows++;
            }
            rowValues.Clear();
            cell = new IntegerCell();
            hasContent = false;
            if (row < int.MaxValue) row++;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = reader.Read();
            if (next < 0) break;
            var character = (char)next;
            if (skipLineFeed && character == '\n') { skipLineFeed = false; continue; }
            skipLineFeed = false;

            if (character is '\r' or '\n')
            {
                FinishRow();
                skipLineFeed = character == '\r';
                continue;
            }

            if (character is ',' or ';' or '\t')
            {
                BeginContent();
                if (cell.InQuotes || (delimiter.HasValue && delimiter != character))
                    throw Error(MatrixInputError.InvalidInteger);
                delimiter ??= character;
                FinishCell();
                if (rowValues.Count == MaximumDimension) throw Error(MatrixInputError.TooLarge);
                if (columns > 0 && rowValues.Count == columns) throw Error(MatrixInputError.InvalidShape);
                continue;
            }

            if (!char.IsWhiteSpace(character)) BeginContent();
            if (!cell.Append(character)) throw Error(MatrixInputError.InvalidInteger);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (hasContent) FinishRow();
        if (rows == 0) throw new MatrixInputException(MatrixInputError.Empty, 1, 1);
        MatrixMemoryGuard.EnsureAvailable((long)values.Count * sizeof(long));
        return new IntegerMatrix(rows, columns, values.ToArray());
    }

    /// <summary>Writes exact invariant decimal values, one comma-separated record per row.</summary>
    public static void Write(TextWriter writer, MatrixCalculation result, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);
        Write(writer, result.Rows, result.Columns,
            (row, column) => result.GetValue(row, column).ToString(CultureInfo.InvariantCulture), cancellationToken, progress);
    }

    /// <summary>Streams an integer selection, including empty input cells, without staging a clipboard string.</summary>
    public static void Write(TextWriter writer, int rows, int columns, Func<int, int, string> readCell,
        CancellationToken cancellationToken = default, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(readCell);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MaximumDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columns, MaximumDimension);
        cancellationToken.ThrowIfCancellationRequested();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (column > 0) writer.Write(',');
                writer.Write(readCell(row, column));
            }
            writer.WriteLine();
            progress?.Report((row + 1d) / rows);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    // Constant memory per field, including arbitrarily long runs of whitespace or leading zeros.
    private struct IntegerCell
    {
        private ulong _magnitude;
        private bool _started;
        private bool _hasDigits;
        private bool _negative;
        private bool _trailingWhitespace;
        private bool _quoted;
        private bool _closed;
        public readonly bool InQuotes => _quoted && !_closed;

        public bool Append(char character)
        {
            if (character == '"')
            {
                if (InQuotes) { _closed = true; return true; }
                if (_started || _quoted) return false;
                _quoted = true;
                return true;
            }
            if (char.IsWhiteSpace(character))
            {
                if (_started) _trailingWhitespace = true;
                return true;
            }
            if (_closed || _trailingWhitespace) return false;
            if (!_started && character is '+' or '-')
            {
                _started = true;
                _negative = character == '-';
                return true;
            }
            if (character is < '0' or > '9') return false;
            _started = true;
            _hasDigits = true;
            var digit = (uint)(character - '0');
            var limit = _negative ? 9223372036854775808UL : 9223372036854775807UL;
            if (_magnitude > (limit - digit) / 10) return false;
            _magnitude = _magnitude * 10 + digit;
            return true;
        }

        public readonly bool TryGetValue(out long value)
        {
            value = _negative
                ? _magnitude == 9223372036854775808UL ? long.MinValue : -(long)_magnitude
                : (long)_magnitude;
            return _hasDigits && !InQuotes;
        }
    }
}
