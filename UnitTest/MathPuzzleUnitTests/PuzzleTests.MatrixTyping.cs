using MathSolver.Controls;
using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixTypingKeys()
    {
        for (int digit = 0; digit <= 9; digit++)
        foreach (int key in new[] { 48 + digit, 96 + digit })
            TestAssert.That(MatrixTypingInput.TryGetText(key, false, false, out var text) && text == digit.ToString(),
                "Both the main number row and numpad must seed the exact digit once.");
        foreach (var sign in new[] { (189, false, "-"), (109, false, "-"), (187, true, "+"), (107, false, "+") })
            TestAssert.That(MatrixTypingInput.TryGetText(sign.Item1, sign.Item2, false, out var text) && text == sign.Item3,
                "Main-keyboard and numpad signs must start signed integer entry.");
        foreach (int key in new[] { 48, 57, 96, 109, 187 })
            TestAssert.That(!MatrixTypingInput.TryGetText(key, false, true, out _), "Ctrl/Alt/Windows shortcuts must not begin editing.");
        foreach (int key in new[] { 46, 8, 13, 37, 65, 190, 188, 110, 187 })
            TestAssert.That(!MatrixTypingInput.TryGetText(key, false, false, out _), "Navigation, Delete, letters, decimals and '=' must not seed a numeric edit.");
        TestAssert.That(!MatrixTypingInput.TryGetText(49, true, false, out _) && !MatrixTypingInput.TryGetText(189, true, false, out _),
            "Shift punctuation must not become a digit or minus sign.");
        string pending = "-"; int cursor = 1;
        foreach (string digit in new[] { "1", "2", "3" })
        {
            TestAssert.That(MatrixTypingInput.TryInsert(pending, cursor, 0, digit, 64, out var next, out int nextCursor),
                "Queued digits before native focus must be forwarded without loss.");
            pending = next; cursor = nextCursor;
        }
        TestAssert.That(pending == "-123" && cursor == 4, "Rapid typing must retain order and place the caret after the final digit.");
        TestAssert.That(MatrixTypingInput.TryInsert("12345", 1, 3, "9", 64, out var replaced, out cursor)
            && replaced == "195" && cursor == 2, "Pending focus must preserve the editor's caret/selection insertion semantics.");
        TestAssert.That(!MatrixTypingInput.TryInsert(new string('1', 64), 64, 0, "2", 64, out _, out _),
            "Forwarded input must respect the same length cap as the native editor.");
    }

    public static void CheckMatrixTypingInteraction()
    {
        var draft = new MatrixDraft(new IntegerMatrix(2, 3, [11, 22, 33, 44, 55, 66]));
        var state = new MatrixViewportState(); state.SetSource(2, 3); state.Select(0, 1);
        TestAssert.That(state.TryBeginTyping("7", true) && state.IsEditing && state.Selection == new MatrixCellRange(0, 1, 1, 1),
            "Typing on a selected cell must begin editing that cell immediately.");
        TestAssert.That(draft.GetCellText(0, 1) == "22" && !state.TryBeginTyping("8", true),
            "Type-to-edit must not commit before Enter/Tab, or restart and replace text on each following key.");
        TestAssert.That(state.Navigate(MatrixCellMove.Next, () => draft.TrySetCell(state.SelectedRow, state.SelectedColumn, "78"))
            && draft.GetCellText(0, 1) == "78" && state.SelectedColumn == 2 && !state.IsEditing,
            "Tab must save the new complete text and move to the next cell.");
        state.TryBeginTyping("-", true); state.CancelEdit();
        TestAssert.That(draft.GetCellText(0, 2) == "33" && !state.IsEditing, "Esc must preserve the original stored value.");
        state.SelectAll();
        TestAssert.That(state.TryBeginTyping("0", true) && !state.AllSelected && !state.HasRangeSelection
            && state.Selection == new MatrixCellRange(0, 2, 1, 1), "Typing on a range must edit only its active cell.");
        state.CancelEdit(); state.Select(1, 2); state.ExtendSelection(0, 0);
        var range = state.Selection;
        TestAssert.That(!state.TryBeginTyping("9", false) && !state.IsEditing && state.Selection == range,
            "Result C and busy inputs must reject typing without disturbing their selection.");
        foreach (string invalid in new[] { "", "12", "a", ".", "_", "١" })
            TestAssert.That(!state.TryBeginTyping(invalid, true) && state.Selection == range,
                "Only supported integer characters may start editing.");
        state.TryBeginTyping("-", true);
        TestAssert.That(!state.Navigate(MatrixCellMove.Down, () => draft.TrySetCell(1, 2, "-")) && state.IsEditing
            && draft.GetCellText(1, 2) == "66", "An unfinished sign must keep the editor open without replacing the old value.");
    }
}
