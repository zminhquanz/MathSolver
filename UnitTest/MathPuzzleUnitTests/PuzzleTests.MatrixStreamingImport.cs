using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixStreamingImport()
    {
        // A return to whole-input List<long> staging exceeds this budget even without a CSV string.
        using var reader = new GeneratedMatrixReader(1024, 1024);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        using var matrix = MatrixTextCodec.Read(reader);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        TestAssert.That(matrix.Rows == 1024 && matrix.Columns == 1024,
            "Generated CSV records must stream into a complete rectangular matrix.");
        Console.WriteLine($"Streaming matrix import allocated {allocated:N0} bytes for 1,048,576 cells.");
        TestAssert.That(allocated < 16L * 1024 * 1024,
            $"CSV import must retain bounded rows and pages instead of staging all cells; allocated {allocated:N0} bytes.");
        TestAssert.That(matrix.DenseData is null && matrix.IsFileBacked && !matrix.TryGetUniformValue(out _),
            "Nonuniform CSV input must remain paged after its builder transfers ownership.");
        TestAssert.Throws<InvalidOperationException>(() => _ = matrix.Data,
            "Legacy Data access must never materialize a dense imported matrix.");
        for (int row = 0; row < matrix.Rows; row++)
        {
            TestAssert.That(matrix.GetValue(row, 0) == 1 && matrix.GetValue(row, 1023) == 2,
                "Reading across imported pages must preserve each row's first and final cell.");
            TestAssert.That(matrix.CachedBytes <= matrix.CacheLimitBytes,
                "Traversing an imported matrix must respect its bounded page cache.");
        }
        TestAssert.That(matrix.CacheLimitBytes <= 2L * 1024 * 1024,
            "The CSV input cache must retain the application input budget.");
        Console.WriteLine($"Streaming matrix input stored {matrix.StoredBytes:N0} bytes; cache {matrix.CachedBytes:N0}/{matrix.CacheLimitBytes:N0} bytes after page traversal.");

        using var extremesReader = new StringReader(" \"-9223372036854775808\" ,+9223372036854775807,0\r\n-7,\"42\",-0\r\n\r\n");
        using var extremes = MatrixTextCodec.Read(extremesReader);
        TestAssert.That(extremes.GetValue(0, 0) == long.MinValue && extremes.GetValue(0, 1) == long.MaxValue
            && extremes.GetValue(0, 2) == 0 && extremes.GetValue(1, 0) == -7
            && extremes.GetValue(1, 1) == 42 && extremes.GetValue(1, 2) == 0,
            "Quoted, signed and extreme Int64 values must survive paged CSV storage exactly.");
        using var uniformReader = new StringReader("-7;-7\n-7;-7\n");
        using var uniform = MatrixTextCodec.Read(uniformReader);
        TestAssert.That(uniform.TryGetUniformValue(out long value) && value == -7 && !uniform.IsFileBacked,
            "A constant CSV must retain compact uniform metadata.");
    }

    public static void CheckMatrixStreamingImportBoundsAndFailures()
    {
        (string Input, MatrixInputError Code, int Row, int Column)[] failures =
        [
            ("", MatrixInputError.Empty, 1, 1),
            (" \r\n\n", MatrixInputError.Empty, 1, 1),
            ("\n1", MatrixInputError.InvalidShape, 1, 1),
            ("1\n\n2", MatrixInputError.InvalidShape, 2, 1),
            ("1,2\n3\n", MatrixInputError.InvalidShape, 2, 2),
            ("1,2\n3,4,5\n", MatrixInputError.InvalidShape, 2, 3),
            ("1,2\n3;4", MatrixInputError.InvalidInteger, 2, 1),
            ("\"1,2\",3", MatrixInputError.InvalidInteger, 1, 1),
            ("1,2\n9223372036854775808,0", MatrixInputError.InvalidInteger, 2, 1),
            ("-9223372036854775809", MatrixInputError.InvalidInteger, 1, 1),
            ("1e2", MatrixInputError.InvalidInteger, 1, 1),
            ("1 2", MatrixInputError.InvalidInteger, 1, 1),
            ("\"1", MatrixInputError.InvalidInteger, 1, 1)
        ];
        foreach (var failure in failures)
        {
            using var reader = new StringReader(failure.Input);
            AssertImportError(reader, failure.Code, failure.Row, failure.Column);
        }
        using var tooWide = new PrefixOnlyReader("1,2,");
        AssertImportError(tooWide, MatrixInputError.TooLarge, 1, 3, maximumDimension: 2);
        using var tooTall = new PrefixOnlyReader("1\n2\n3");
        AssertImportError(tooTall, MatrixInputError.TooLarge, 3, 1, maximumDimension: 2);
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MatrixTextCodec.Read(new StringReader("1"), maximumDimension: 0),
            "A dimension limit below one must fail before reading.");
        TestAssert.Throws<ArgumentOutOfRangeException>(() => MatrixTextCodec.Read(new StringReader("1"), maximumDimension: 16385),
            "A dimension limit above the model ceiling must fail before reading.");

        // Observe the actual files created during this import, then prove every failed exit releases them.
        foreach (bool failParsing in new[] { true, false })
        {
            var createdFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] existingFiles = Directory.GetFiles(Path.GetTempPath(), "matrix-input-*.bin");
            using var reader = new GeneratedMatrixReader(16, 1024, afterCharacter: count =>
            {
                if (count != 16385) return;
                foreach (string path in Directory.GetFiles(Path.GetTempPath(), "matrix-input-*.bin"))
                    if (!existingFiles.Contains(path, StringComparer.OrdinalIgnoreCase)) createdFiles.Add(path);
                if (!failParsing) throw new IOException("Injected input read failure.");
            }, invalidAtCharacter: failParsing ? 16385 : long.MaxValue);
            if (failParsing) AssertImportError(reader, MatrixInputError.InvalidInteger, 9, 1);
            else TestAssert.Throws<IOException>(() => MatrixTextCodec.Read(reader), "Reader failures must propagate unchanged.");
            TestAssert.That(createdFiles.Count > 0 && createdFiles.All(path => !File.Exists(path)),
                "A parse or reader failure must delete every temporary input file already written.");
        }
    }

    public static void CheckMatrixStreamingImportCancellation()
    {
        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        using var unread = new PrefixOnlyReader("");
        TestAssert.Throws<OperationCanceledException>(() => MatrixTextCodec.Read(unread, alreadyCancelled.Token),
            "An already-cancelled import must not request any reader data.");

        using var cancellation = new CancellationTokenSource();
        var createdFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] existingFiles = Directory.GetFiles(Path.GetTempPath(), "matrix-input-*.bin");
        using var reader = new GeneratedMatrixReader(1024, 1024, cancellation, cancelAfterCharacters: 16384,
            afterCharacter: count =>
            {
                if (count != 16384) return;
                foreach (string path in Directory.GetFiles(Path.GetTempPath(), "matrix-input-*.bin"))
                    if (!existingFiles.Contains(path, StringComparer.OrdinalIgnoreCase)) createdFiles.Add(path);
            });
        TestAssert.Throws<OperationCanceledException>(() => MatrixTextCodec.Read(reader, cancellation.Token),
            "Cancellation must stop a generated CSV while records are being consumed.");
        TestAssert.That(reader.CharactersRead == 16384,
            "The importer must observe cancellation before requesting another input character.");
        TestAssert.That(createdFiles.Count > 0 && createdFiles.All(path => !File.Exists(path)),
            "Cancellation must delete the unpublished input file after rows have already streamed to disk.");
    }

    private static void AssertImportError(TextReader reader, MatrixInputError code, int row, int column,
        int maximumDimension = 16384)
    {
        try { using var unexpected = MatrixTextCodec.Read(reader, maximumDimension: maximumDimension); }
        catch (MatrixInputException error)
        {
            TestAssert.That(error.Code == code && error.Row == row && error.Column == column,
                $"Expected {code} at {row},{column}; received {error.Code} at {error.Row},{error.Column}.");
            return;
        }
        throw new InvalidOperationException($"Expected CSV import failure {code} at {row},{column}.");
    }

    // Generates each character without allocating source rows or a whole CSV string.
    private sealed class GeneratedMatrixReader(int rows, int columns,
        CancellationTokenSource? cancellation = null, long cancelAfterCharacters = long.MaxValue,
        Action<long>? afterCharacter = null, long invalidAtCharacter = long.MaxValue) : TextReader
    {
        private readonly long _length = (long)rows * columns * 2;
        public long CharactersRead { get; private set; }
        public override int Read()
        {
            if (CharactersRead == _length) return -1;
            long position = CharactersRead++;
            if (CharactersRead == cancelAfterCharacters) cancellation?.Cancel();
            afterCharacter?.Invoke(CharactersRead);
            if (CharactersRead == invalidAtCharacter) return 'x';
            if ((position & 1) == 0) return '1' + (int)((position / 2) & 1);
            return (position + 1) % (columns * 2L) == 0 ? '\n' : ',';
        }
        public override string ReadToEnd() => throw new InvalidOperationException("The importer must stream characters.");
        public override string? ReadLine() => throw new InvalidOperationException("The importer must bound field parsing.");
    }

    private sealed class PrefixOnlyReader(string prefix) : TextReader
    {
        private int _position;
        public override int Read() => _position < prefix.Length ? prefix[_position++]
            : throw new InvalidOperationException("The importer read past a conclusive dimension violation.");
    }
}
