using MathSolver.Controls;
using MathSolver.Services.Core;
using MathSolver.Testing;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixSelectAll()
    {
        var state = new MatrixViewportState();
        state.SetSource(1024, 1024);
        state.Arrange(600, 340, 96, 36, 20);
        state.Select(900, 900);
        double x = state.OffsetX, y = state.OffsetY;
        TestAssert.That(state.SelectAll() && state.AllSelected && !state.IsEditing,
            "Select all must include off-screen cells without opening an editor.");
        TestAssert.That(state.OffsetX == x && state.OffsetY == y, "Selecting all must retain the visible viewport.");
        TestAssert.That(state.TapCell(900, 900, true) == MatrixCellAction.Selected && !state.AllSelected && !state.IsEditing,
            "Clicking the active cell after selecting all must first return to single selection.");
        state.BeginEdit(true);
        TestAssert.That(!state.SelectAll(() => false) && state.IsEditing && !state.AllSelected,
            "An invalid edit must prevent select-all and retain its text/selection.");
        int commits = 0;
        TestAssert.That(state.SelectAll(() => { commits++; return true; }) && commits == 1 && !state.IsEditing,
            "Selecting all must commit a valid pending edit exactly once.");
        state.Navigate(MatrixCellMove.Right);
        TestAssert.That(!state.AllSelected, "Arrow navigation must return to a single selected cell.");
        state.SelectAll();
        state.BeginEdit(true);
        TestAssert.That(!state.AllSelected && state.IsEditing, "Editing must collapse the whole-table selection.");
        state.CancelEdit(); state.SelectAll(); state.SetSource(2, 3);
        TestAssert.That(!state.AllSelected, "Switching matrices must reset the whole-table selection.");
        Console.WriteLine("Checked select-all, off-screen cells, edit rejection, single-click collapse, navigation and matrix switching.");
    }

    public static void CheckMatrixClipboard()
    {
        var draft = new MatrixDraft(2, 3);
        draft.TrySetCell(0, 0, "1"); draft.TrySetCell(0, 2, long.MaxValue.ToString(CultureInfo.InvariantCulture));
        draft.TrySetCell(1, 0, "-2"); draft.TrySetCell(1, 1, "0");
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var content = MatrixClipboardCodec.Create(2, 3, draft.GetCellText, includeHtml: true);
            TestAssert.That(content!.Text == "1\t\t9223372036854775807\r\n-2\t0\t\r\n",
                "Excel text must preserve blank cells, negatives, zeros and complete long values without headers.");
            TestAssert.That(content.Html!.Contains("mso-number-format") && content.Html.Contains("9223372036854775807"),
                "Rich clipboard must protect long integers against Excel numeric rounding.");
            TestAssert.That(!MatrixClipboardCodec.Create(1, 1, (_, _) => "42")!.Text.Contains('#'), "Single cell copies must not include grid headers.");
            TestAssert.That(MatrixClipboardCodec.Create(1, 1, (_, _) => "")!.Text == "\r\n",
                "Copying an empty cell must retain a blank table cell rather than a display placeholder.");
            var escaped = MatrixClipboardCodec.Create(1, 1, (_, _) => "<cell>&", includeHtml: true);
            TestAssert.That(escaped!.Html!.Contains("&lt;cell&gt;&amp;"), "HTML clipboard cell contents must be encoded.");
            var a = new IntegerMatrix(1, 1, [long.MaxValue]);
            var sum = MatrixEngine.Calculate(a, a, MatrixOperation.Add);
            var product = MatrixEngine.Calculate(a, a, MatrixOperation.Multiply);
            foreach (var result in new[] { sum, product })
            {
                string expected = result.GetValue(0, 0).ToString(CultureInfo.InvariantCulture);
                TestAssert.That(MatrixClipboardCodec.Create(1, 1, (r, c) => result.GetValue(r, c).ToString(CultureInfo.InvariantCulture))!.Text == expected + "\r\n",
                    "Int128 and BigInteger results must copy every digit.");
            }
            var full = MatrixClipboardCodec.Create(1024, 1024, (_, _) => "1");
            TestAssert.That(full!.Text.Count(c => c == '\n') == 1024 && full!.Text.Count(c => c == '\t') == 1024 * 1023,
                "Copying must cover the entire 1024x1024 matrix, not only rendered cells.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool cancelled = false;
            try { MatrixClipboardCodec.Create(1, 1, (_, _) => throw new Exception("Must not read cancelled data"), token: cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            TestAssert.That(cancelled, "Cancelled copies must not read or publish matrix data.");
            using var interrupted = new CancellationTokenSource();
            int reads = 0;
            TestAssert.Throws<OperationCanceledException>(() => MatrixClipboardCodec.Create(1024, 1024, (_, _) =>
            { reads++; interrupted.Cancel(); return "1"; }, token: interrupted.Token), "An in-progress large copy must be cancellable.");
            TestAssert.That(reads == 1, "Cancellation must stop serialization before reading the rest of a large matrix.");
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Console.WriteLine("Checked TSV dimensions, empty cells, signed integers, Int128/BigInteger, Excel precision, cancellation and a million cells.");
    }
}
