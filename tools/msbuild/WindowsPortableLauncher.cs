using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

internal static class WindowsPortableLauncher
{
    [STAThread]
    private static int Main()
    {
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app");
        var start = new ProcessStartInfo(Path.Combine(directory, "MathSolver.exe"))
        {
            Arguments = GetForwardedArguments(),
            UseShellExecute = false,
            WorkingDirectory = directory
        };

        try
        {
            using (Process process = Process.Start(start))
            {
                return 0;
            }
        }
        catch (Win32Exception error)
        {
            MessageBox(IntPtr.Zero, "Could not start Math Solver.\n\n" + error.Message, "Math Solver", 0x10);
            return 1;
        }
    }

    private static string GetForwardedArguments()
    {
        // Keep the original Windows quoting when forwarding the arguments.
        string commandLine = Environment.CommandLine.TrimStart();
        int argumentsStart = commandLine.StartsWith("\"", StringComparison.Ordinal)
            ? commandLine.IndexOf('"', 1) + 1
            : commandLine.IndexOfAny(new[] { ' ', '\t' });

        return argumentsStart > 0 ? commandLine.Substring(argumentsStart).TrimStart() : string.Empty;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}
