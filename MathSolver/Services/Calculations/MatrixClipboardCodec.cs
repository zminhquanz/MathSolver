using System.Net;
using System.Text;

namespace MathSolver.Services.Core;

public sealed record MatrixClipboardContent(string Text, string? Html);

/// <summary>Clipboard tables without display ellipses, row labels or locale-specific number formatting.</summary>
public static class MatrixClipboardCodec
{
    public static MatrixClipboardContent Create(int rows, int columns, Func<int, int, string> readCell,
        bool includeHtml = false, CancellationToken token = default, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(readCell);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columns, 1024);
        token.ThrowIfCancellationRequested();
        var text = new StringBuilder();
        var html = includeHtml ? new StringBuilder("<html><head><meta charset=\"utf-8\"></head><body><table>") : null;
        for (int row = 0; row < rows; row++)
        {
            html?.Append("<tr>");
            for (int column = 0; column < columns; column++)
            {
                token.ThrowIfCancellationRequested();
                string value = readCell(row, column);
                if (column > 0) text.Append('\t');
                text.Append(value);
                if (html is not null)
                {
                    var digits = value.AsSpan();
                    if (!digits.IsEmpty && digits[0] is '-' or '+') digits = digits[1..];
                    // Excel stores numeric cells with only 15 significant digits.
                    // Keep longer integer values as text in the rich clipboard format.
                    html.Append(digits.Length > 15 ? "<td style='mso-number-format:\"\\@\"'>" : "<td>");
                    html.Append(WebUtility.HtmlEncode(value)).Append("</td>");
                }
            }
            text.Append("\r\n");
            html?.Append("</tr>");
            progress?.Report((row + 1d) / rows);
        }
        token.ThrowIfCancellationRequested();
        html?.Append("</table></body></html>");
        return new(text.ToString(), html?.ToString());
    }
}
