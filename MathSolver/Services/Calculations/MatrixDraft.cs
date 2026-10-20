using System.Globalization;

namespace MathSolver.Services.Core;

public sealed class MatrixDraft
{
    private readonly bool[] _filled;

    public MatrixDraft(int rows, int columns)
    {
        Matrix = new IntegerMatrix(rows, columns);
        _filled = new bool[Matrix.Data.Length];
        MissingCount = _filled.Length;
    }

    public MatrixDraft(IntegerMatrix imported)
    {
        ArgumentNullException.ThrowIfNull(imported);
        Matrix = imported;
        _filled = new bool[imported.Data.Length];
        Array.Fill(_filled, true);
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
        var token = text.AsSpan().Trim();
        if (token.IsEmpty)
        {
            if (_filled[index]) { _filled[index] = false; MissingCount++; }
            Matrix.Data[index] = 0;
            return true;
        }

        var digits = token;
        if (digits[0] is '+' or '-') digits = digits[1..];
        if (digits.IsEmpty) return false;
        foreach (var character in digits)
            if (character is < '0' or > '9') return false;
        if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            return false;

        Matrix.Data[index] = value;
        if (!_filled[index]) { _filled[index] = true; MissingCount--; }
        return true;
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
