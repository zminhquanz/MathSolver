using System.Diagnostics;

namespace MathSolver.Services;

/// <summary>
/// Opens the folder containing the selected local AI model when the current
/// platform exposes a desktop file manager. Mobile app-data folders remain
/// private to the application and therefore cannot be browsed directly.
/// </summary>
public sealed class ModelFileLocationService
{
    public Task<bool> TryOpenContainingFolderAsync(
        string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            modelPath);

#if WINDOWS
        try
        {
            if (!File.Exists(modelPath))
            {
                return Task.FromResult(false);
            }

            string? directoryPath =
                Path.GetDirectoryName(modelPath);

            if (string.IsNullOrWhiteSpace(directoryPath) ||
                !Directory.Exists(directoryPath))
            {
                return Task.FromResult(false);
            }

            Process? process =
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments =
                            $"/select,\"{modelPath}\"",
                        UseShellExecute = true
                    });

            return Task.FromResult(process is not null);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(
                $"Opening the model folder failed: {exception}");

            return Task.FromResult(false);
        }
#else
        return Task.FromResult(false);
#endif
    }
}
