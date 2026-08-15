using System.Text;

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

    public const long MaximumModelFileSizeBytes =
        5_500_000_000L;

    public string? GetSavedModelPath()
    {
        string path =
            Preferences.Default.Get(
                ModelPathPreferenceKey,
                string.Empty);

        if (IsSupportedModelPath(path))
        {
            return path;
        }

        Preferences.Default.Remove(
            ModelPathPreferenceKey);

        return null;
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

        await ValidateGgufModelAsync(
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

        string temporaryPath =
            $"{destinationPath}.{Guid.NewGuid():N}.importing";

        try
        {
            await using Stream source =
                await fileResult.OpenReadAsync();

            await using (var destination =
                new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    useAsync: true))
            {
                await CopyWithSizeLimitAsync(
                    source,
                    destination,
                    cancellationToken);

                await destination.FlushAsync(
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                destinationPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

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

    public static bool IsSupportedModelPath(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) ||
            !string.Equals(
                Path.GetExtension(path),
                ".gguf",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(path);

            if (fileInfo.Length > MaximumModelFileSizeBytes)
            {
                return false;
            }

            using Stream stream = File.OpenRead(path);
            string architecture =
                ReadGgufArchitecture(stream);

            return IsGemma4Architecture(architecture);
        }
        catch
        {
            return false;
        }
    }

    private static async Task ValidateGgufModelAsync(
        FileResult fileResult,
        CancellationToken cancellationToken)
    {
        await using Stream stream =
            await fileResult.OpenReadAsync();

        if (stream.CanSeek &&
            stream.Length > MaximumModelFileSizeBytes)
        {
            throw new QuizLlmModelTooLargeException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        string architecture =
            ReadGgufArchitecture(stream);

        if (!IsGemma4Architecture(architecture))
        {
            throw new UnsupportedQuizLlmModelException();
        }
    }

    private static bool IsGemma4Architecture(
        string architecture)
    {
        return string.Equals(
            architecture,
            "gemma4",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadGgufArchitecture(
        Stream stream)
    {
        using var reader =
            new BinaryReader(
                stream,
                Encoding.UTF8,
                leaveOpen: true);

        byte[] magic =
            reader.ReadBytes(GgufMagic.Length);

        if (!magic.AsSpan().SequenceEqual(GgufMagic))
        {
            throw new InvalidDataException(
                "The selected file does not contain a GGUF header.");
        }

        uint version = reader.ReadUInt32();

        if (version is < 2 or > 3)
        {
            throw new InvalidDataException(
                "Unsupported GGUF version.");
        }

        _ = reader.ReadUInt64();
        ulong metadataCount = reader.ReadUInt64();

        if (metadataCount > 4096)
        {
            throw new InvalidDataException(
                "Invalid GGUF metadata count.");
        }

        for (ulong index = 0;
             index < metadataCount;
             index++)
        {
            string key =
                ReadGgufString(
                    reader,
                    maximumByteCount: 16 * 1024);

            var valueType =
                (GgufValueType)reader.ReadUInt32();

            if (string.Equals(
                    key,
                    "general.architecture",
                    StringComparison.Ordinal))
            {
                if (valueType != GgufValueType.String)
                {
                    throw new InvalidDataException(
                        "Invalid GGUF architecture metadata.");
                }

                return ReadGgufString(
                    reader,
                    maximumByteCount: 256);
            }

            SkipGgufValue(
                reader,
                valueType,
                depth: 0);
        }

        throw new InvalidDataException(
            "GGUF architecture metadata was not found.");
    }

    private static string ReadGgufString(
        BinaryReader reader,
        int maximumByteCount)
    {
        ulong length = reader.ReadUInt64();

        if (length > (ulong)maximumByteCount)
        {
            throw new InvalidDataException(
                "Invalid GGUF string length.");
        }

        byte[] bytes =
            reader.ReadBytes((int)length);

        if (bytes.Length != (int)length)
        {
            throw new EndOfStreamException();
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void SkipGgufValue(
        BinaryReader reader,
        GgufValueType valueType,
        int depth)
    {
        if (depth > 4)
        {
            throw new InvalidDataException(
                "Invalid nested GGUF metadata.");
        }

        switch (valueType)
        {
            case GgufValueType.UInt8:
            case GgufValueType.Int8:
            case GgufValueType.Bool:
                SkipBytes(reader, 1);
                break;
            case GgufValueType.UInt16:
            case GgufValueType.Int16:
                SkipBytes(reader, 2);
                break;
            case GgufValueType.UInt32:
            case GgufValueType.Int32:
            case GgufValueType.Float32:
                SkipBytes(reader, 4);
                break;
            case GgufValueType.UInt64:
            case GgufValueType.Int64:
            case GgufValueType.Float64:
                SkipBytes(reader, 8);
                break;
            case GgufValueType.String:
                ulong stringLength = reader.ReadUInt64();
                SkipBytes(reader, stringLength);
                break;
            case GgufValueType.Array:
                var elementType =
                    (GgufValueType)reader.ReadUInt32();
                ulong elementCount = reader.ReadUInt64();

                if (elementCount > 1_000_000)
                {
                    throw new InvalidDataException(
                        "Invalid GGUF array length.");
                }

                for (ulong index = 0;
                     index < elementCount;
                     index++)
                {
                    SkipGgufValue(
                        reader,
                        elementType,
                        depth + 1);
                }

                break;
            default:
                throw new InvalidDataException(
                    "Unsupported GGUF metadata type.");
        }
    }

    private static void SkipBytes(
        BinaryReader reader,
        ulong byteCount)
    {
        if (byteCount > int.MaxValue)
        {
            throw new InvalidDataException(
                "Invalid GGUF metadata size.");
        }

        byte[] skipped =
            reader.ReadBytes((int)byteCount);

        if (skipped.Length != (int)byteCount)
        {
            throw new EndOfStreamException();
        }
    }

    private static async Task CopyWithSizeLimitAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024 * 1024];
        long totalBytes = 0;

        while (true)
        {
            int bytesRead =
                await source.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;

            if (totalBytes > MaximumModelFileSizeBytes)
            {
                throw new QuizLlmModelTooLargeException();
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }
    }

    private enum GgufValueType : uint
    {
        UInt8 = 0,
        Int8 = 1,
        UInt16 = 2,
        Int16 = 3,
        UInt32 = 4,
        Int32 = 5,
        Float32 = 6,
        Bool = 7,
        String = 8,
        Array = 9,
        UInt64 = 10,
        Int64 = 11,
        Float64 = 12
    }
}

public sealed class UnsupportedQuizLlmModelException :
    Exception
{
}

public sealed class QuizLlmModelTooLargeException :
    Exception
{
}
