using System.Globalization;

namespace MathSolver.Services.Core;

internal enum MatrixCellTextError { None, InvalidFormat, OutOfRange }

/// <summary>Nonthrowing validation of an unfinished Int64 cell edit.</summary>
internal static class MatrixCellInput
{
    internal static MatrixCellTextError Validate(string? text)
    {
        var token = text.AsSpan().Trim();
        if (token.IsEmpty) return MatrixCellTextError.None;
        var digits = token;
        if (digits[0] is '+' or '-') digits = digits[1..];
        if (digits.IsEmpty) return MatrixCellTextError.None; // User is still entering a sign.
        foreach(char digit in digits)
            if (digit is < '0' or > '9') return MatrixCellTextError.InvalidFormat;
        return long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
            ? MatrixCellTextError.None : MatrixCellTextError.OutOfRange;
    }
}
