using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixFileStreamingImport()
    {
        using var workspace = TestWorkspace.Temporary("file-input-import");
        // Nonuniform data must exercise physical storage; constant blocks use metadata only.
        string row = string.Join(',', Enumerable.Repeat("7", 1023).Append("8")) + "\n";
        using (var reader = new MatrixRepeatingRows(row, 1025))
        using (var matrix = MatrixTextCodec.Read(reader, temporaryDirectory: workspace.DirectoryPath))
        {
            TestAssert.That(matrix.IsFileBacked && matrix.Rows == 1025 && matrix.Columns == 1024
                && matrix.GetValue(1024, 1023) == 8 && matrix.ResidentBytes < 6L * 1024 * 1024,
                "Large CSV input must stream into a bounded disk store with its actual final shape.");
            TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == 1,
                "A completed import must retain only its owned input file, not its staging spool.");
        }
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == 0, "Disposing imported input must delete its file.");
        using var stop = new CancellationTokenSource();
        using var canceledReader = new MatrixRepeatingRows(row, 1026, n => { if (n == 1025) stop.Cancel(); });
        TestAssert.Throws<OperationCanceledException>(() => MatrixTextCodec.Read(canceledReader, stop.Token, workspace.DirectoryPath),
            "Canceling an import after it switches to disk must stop and discard staging data.");
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == 0,
            "Canceled disk imports must not leak temporary files.");
        using var invalidReader = new MatrixRepeatingRows(row, 1025, suffix: "1,2\n");
        TestAssert.Throws<MatrixInputException>(() => MatrixTextCodec.Read(invalidReader, temporaryDirectory: workspace.DirectoryPath),
            "A malformed row after spilling to disk must still reject the entire import.");
        TestAssert.That(Directory.GetFiles(workspace.DirectoryPath).Length == 0,
            "Rejected disk imports must remove their partial spool.");
    }

    private sealed class MatrixRepeatingRows(string row, int rows, Action<int>? beginRow = null, string suffix = "") : TextReader
    {
        private int _position, _row, _suffix;
        public override int Read()
        {
            if (_row >= rows) return _suffix < suffix.Length ? suffix[_suffix++] : -1;
            if (_position == 0) beginRow?.Invoke(_row);
            char c = row[_position++];
            if (_position == row.Length) { _position = 0; _row++; }
            return c;
        }
    }
}
