namespace MathSolver.Services;

/// <summary>
/// Lưu đường dẫn model cục bộ. Trên thiết bị di động, file do document picker
/// trả về được sao chép vào AppData để quyền truy cập không mất sau khi app đóng.
/// </summary>
public sealed class QuizLlmModelStore
{
    private static readonly byte[] GgufMagic =
    [
        (byte)'G',
        (byte)'G',
        (byte)'U',
        (byte)'F'
    ];

    private const string ModelPathPreferenceKey =
        "quiz_llm_model_path";

    private const string FirstGreetingPreferenceKey =
        "quiz_llm_first_greeting_shown";

    public string? GetSavedModelPath()
    {
        string path =
            Preferences.Default.Get(
                ModelPathPreferenceKey,
                string.Empty);

        return File.Exists(path) &&
               string.Equals(
                   Path.GetExtension(path),
                   ".gguf",
                   StringComparison.OrdinalIgnoreCase)
            ? path
            : null;
    }

    public async Task<string> ImportAsync(
        FileResult fileResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileResult);

        if (!string.Equals(
                Path.GetExtension(fileResult.FileName),
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Only GGUF model files are supported.");
        }

        await ValidateGgufHeaderAsync(
            fileResult,
            cancellationToken);

#if WINDOWS
        if (!string.IsNullOrWhiteSpace(fileResult.FullPath) &&
            File.Exists(fileResult.FullPath))
        {
            return fileResult.FullPath;
        }
#endif

        string modelsDirectory =
            Path.Combine(
                FileSystem.AppDataDirectory,
                "Models");

        Directory.CreateDirectory(
            modelsDirectory);

        string safeFileName =
            Path.GetFileName(fileResult.FileName);

        string destinationPath =
            Path.Combine(
                modelsDirectory,
                safeFileName);

        await using Stream source =
            await fileResult.OpenReadAsync();

        await using var destination =
            new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true);

        await source.CopyToAsync(
            destination,
            1024 * 1024,
            cancellationToken);

        await destination.FlushAsync(
            cancellationToken);

        return destinationPath;
    }

    public void SaveModelPath(
        string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            modelPath);

        Preferences.Default.Set(
            ModelPathPreferenceKey,
            modelPath);
    }

    public bool ShouldShowFirstGreeting() =>
        !Preferences.Default.Get(
            FirstGreetingPreferenceKey,
            false);

    public void MarkFirstGreetingShown()
    {
        Preferences.Default.Set(
            FirstGreetingPreferenceKey,
            true);
    }

    public void ClearSavedModelPath()
    {
        Preferences.Default.Remove(
            ModelPathPreferenceKey);
    }

    public static bool IsRecommendedQuantization(
        string modelPath)
    {
        string name =
            Path.GetFileNameWithoutExtension(modelPath);

        return name.Contains(
                   "Q4_K_M",
                   StringComparison.OrdinalIgnoreCase) ||
               name.Contains(
                   "IQ4_XS",
                   StringComparison.OrdinalIgnoreCase) ||
               name.Contains(
                   "Q4_0",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ValidateGgufHeaderAsync(
        FileResult fileResult,
        CancellationToken cancellationToken)
    {
        await using Stream stream =
            await fileResult.OpenReadAsync();

        var header = new byte[GgufMagic.Length];
        int bytesRead = 0;

        while (bytesRead < header.Length)
        {
            int read =
                await stream.ReadAsync(
                    header.AsMemory(
                        bytesRead,
                        header.Length - bytesRead),
                    cancellationToken);

            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        if (bytesRead != GgufMagic.Length ||
            !header.AsSpan().SequenceEqual(GgufMagic))
        {
            throw new InvalidDataException(
                "The selected file does not contain a GGUF header.");
        }
    }
}
