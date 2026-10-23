using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixParallelStorageErrors()
    {
        TestAssert.That(MatrixStorageErrors.IsFailure(new IOException())
            && MatrixStorageErrors.IsFailure(new AggregateException(new EndOfStreamException(),
                new AggregateException(new UnauthorizedAccessException(), new ObjectDisposedException("input")))),
            "Direct and nested parallel file errors must use storage-specific feedback.");
        TestAssert.That(!MatrixStorageErrors.IsFailure(new AggregateException())
            && !MatrixStorageErrors.IsFailure(new AggregateException(new IOException(), new InvalidOperationException()))
            && !MatrixStorageErrors.IsFailure(new OutOfMemoryException()),
            "Storage classification must not hide unrelated or mixed worker failures.");
    }
}
