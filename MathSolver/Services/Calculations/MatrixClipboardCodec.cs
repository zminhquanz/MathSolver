using System.Net;
using System.Text;

namespace MathSolver.Services.Core;

public sealed record MatrixClipboardContent(string Text, string? Html);

/// <summary>Clipboard tables without display ellipses, row labels or locale-specific number formatting.</summary>
public static partial class MatrixClipboardCodec
{
    /// <returns>Null when the table exceeds clipboard capacity; no partial content is returned.</returns>
    public static MatrixClipboardContent? Create(int rows, int columns, Func<int, int, string> readCell,
        bool includeHtml = false, CancellationToken token = default, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(readCell);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, MatrixLimits.MaximumDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columns, MatrixLimits.MaximumDimension);
        token.ThrowIfCancellationRequested();
        const string htmlStart = "<html><head><meta charset=\"utf-8\"></head><body><table>";
        const string htmlEnd = "</table></body></html>";
        long minimumText = (long)rows * (columns + 1);
        long minimumHtml = includeHtml ? (long)rows * (9L * columns + 9) + htmlStart.Length + htmlEnd.Length : 0;
        if (minimumText > MatrixLimits.MaximumClipboardCharacters || minimumHtml > MatrixLimits.MaximumClipboardCharacters)
            return null;
        MatrixMemoryGuard.EnsureAvailable((minimumText + minimumHtml) * sizeof(char) * 2);
        var text = new StringBuilder();
        var html = includeHtml ? new StringBuilder(htmlStart) : null;
        for (int row = 0; row < rows; row++)
        {
            html?.Append("<tr>");
            for (int column = 0; column < columns; column++)
            {
                token.ThrowIfCancellationRequested();
                string value = readCell(row, column);
                if ((long)text.Length + value.Length + (column > 0 ? 1 : 0) + 2 > MatrixLimits.MaximumClipboardCharacters)
                    return null;
                if (column > 0) text.Append('\t');
                text.Append(value);
                if (html is not null)
                {
                    var digits = value.AsSpan();
                    if (!digits.IsEmpty && digits[0] is '-' or '+') digits = digits[1..];
                    // Excel stores numeric cells with only 15 significant digits.
                    // Keep longer integer values as text in the rich clipboard format.
                    string tag = digits.Length > 15 ? "<td style='mso-number-format:\"\\@\"'>" : "<td>";
                    // Bound encoding growth before allocating an escaped copy of the cell.
                    long encodedLength = 0;
                    foreach (char character in value)
                        encodedLength += character switch { '&' => 5, '<' or '>' => 4, '"' => 6, '\'' => 5, _ => 1 };
                    if ((long)html.Length + tag.Length + encodedLength + 10 + htmlEnd.Length > MatrixLimits.MaximumClipboardCharacters)
                        return null;
                    string encoded = WebUtility.HtmlEncode(value);
                    if ((long)html.Length + tag.Length + encoded.Length + 10 + htmlEnd.Length > MatrixLimits.MaximumClipboardCharacters)
                        return null;
                    html.Append(tag).Append(encoded).Append("</td>");
                }
            }
            text.Append("\r\n");
            html?.Append("</tr>");
            progress?.Report((row + 1d) / rows);
        }
        token.ThrowIfCancellationRequested();
        html?.Append(htmlEnd);
        MatrixMemoryGuard.EnsureAvailable(((long)text.Length + (html?.Length ?? 0)) * sizeof(char));
        return new(text.ToString(), html?.ToString());
    }
}
