using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixCellTypingLimits()
    {
        foreach(string? text in new string?[]{null,""," ","-","+","0","-0","+1","9223372036854775807","-9223372036854775808","+9223372036854775807","00000000000000000000000000000000000001"})
            TestAssert.That(MatrixCellInput.Validate(text)==MatrixCellTextError.None,"Typing must allow exact boundaries, signed integers and unfinished signs: "+text);
        foreach(string text in new[]{"9223372036854775808","-9223372036854775809","+9223372036854775808","18446744073709551615",new string('9',64)})
            TestAssert.That(MatrixCellInput.Validate(text)==MatrixCellTextError.OutOfRange,"Overflow must be reported without parsing exceptions: "+text);
        foreach(string text in new[]{"1,000","1.0","1e3","--1","12-3","++","١٢","abc"})
            TestAssert.That(MatrixCellInput.Validate(text)==MatrixCellTextError.InvalidFormat,"Matrix input grammar remains exact ASCII integers: "+text);
    }
}
