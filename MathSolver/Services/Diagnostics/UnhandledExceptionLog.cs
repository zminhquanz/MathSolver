using System.Diagnostics;
using System.Text;

namespace MathSolver.Services;

/// <summary>Records the original error without needing MAUI services during native teardown.</summary>
internal static class UnhandledExceptionLog
{
    private static readonly object Gate = new();
    private const long MaximumBytes = 1024 * 1024;

    internal static bool Record(string path, string source, Exception error, string? message = null)
    {
        string entry = $"[{DateTimeOffset.Now:O}] {source}\nHResult: 0x{error.HResult:X8}\n"
            + (string.IsNullOrWhiteSpace(message) ? "" : message + "\n") + error + "\n\n";
        Debug.WriteLine(entry);
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length >= MaximumBytes)
                    File.Move(path, path + ".previous", overwrite: true);
                File.AppendAllText(path, entry, Encoding.UTF8);
            }
            return true;
        }
        catch (Exception loggingError)
        {
            // A diagnostic failure must not replace the original unhandled exception.
            Debug.WriteLine($"Unable to save exception log: {loggingError.Message}");
            return false;
        }
    }
}
