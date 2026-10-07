using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckShutdownDiagnostics()
    {
        string folder = Path.Combine(Path.GetTempPath(), "MathSolverShutdownTests-" + Guid.NewGuid().ToString("N"));
        string log = Path.Combine(folder, "windows-unhandled.log");
        Directory.CreateDirectory(folder);
        try
        {
            Exception exception;
            try
            {
                ThrowDiagnosticException();
                throw new Exception("Expected diagnostic error.");
            }
            catch (InvalidOperationException inner)
            {
                exception = new ObjectDisposedException("Shutdown callback", inner);
            }

            Require(UnhandledExceptionLog.Record(log, "WinUI UnhandledException", exception, "Closing window"),
                "Shutdown errors must be recorded without MAUI services.");
            string text = File.ReadAllText(log);
            Require(text.Contains("ObjectDisposedException") && text.Contains("InvalidOperationException")
                && text.Contains(nameof(ThrowDiagnosticException)) && text.Contains("Closing window")
                && text.Contains("HResult: 0x"), "Diagnostics must preserve the original error and inner stack.");

            File.WriteAllText(log, new string('x', 1024 * 1024));
            Require(UnhandledExceptionLog.Record(log, "WinUI", exception) && File.Exists(log + ".previous")
                && new FileInfo(log).Length < 1024 * 1024, "Exception logs must rotate at the size limit.");
            Require(!UnhandledExceptionLog.Record(Path.Combine(log, "invalid-child.log"), "WinUI", exception),
                "A logging failure must not throw another exception during teardown.");
        }
        finally
        {
            File.Delete(log);
            File.Delete(log + ".previous");
            Directory.Delete(folder);
        }
    }

    private static void ThrowDiagnosticException() => throw new InvalidOperationException("Shutdown callback test");
}
