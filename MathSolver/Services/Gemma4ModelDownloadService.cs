using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace MathSolver.Services;

public enum Gemma4ModelVariant
{
    E2B,
    E4B
}

public sealed record Gemma4ModelDescriptor(
    Gemma4ModelVariant Variant,
    string DisplayName,
    string FileName,
    Uri ModelPageUri,
    Uri DownloadUri,
    long ApproximateSizeBytes);

public sealed record Gemma4ModelDownloadSelection(
    Gemma4ModelDescriptor Model,
    string DestinationDirectory);

public sealed record Gemma4ModelDownloadProgress(
    long BytesReceived,
    long? TotalBytes)
{
    public double Fraction =>
        TotalBytes is > 0
            ? Math.Clamp(
                (double)BytesReceived / TotalBytes.Value,
                0d,
                1d)
            : 0d;
}

/// <summary>
/// Tải đúng hai checkpoint Gemma 4 QAT Q4_0 chính chủ mà Math Solver hỗ trợ.
/// File mmproj không được tải vì tính năng Toán đố chỉ dùng văn bản.
/// </summary>
public sealed class Gemma4ModelDownloadService
{
    private const int BufferSize = 1024 * 1024;

    private static readonly HttpClient HttpClient =
        new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

    public static Gemma4ModelDescriptor E2B { get; } =
        new(
            Gemma4ModelVariant.E2B,
            "Gemma 4 E2B Q4_0",
            "gemma-4-E2B_q4_0-it.gguf",
            new Uri(
                "https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf"),
            new Uri(
                "https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/resolve/main/gemma-4-E2B_q4_0-it.gguf?download=true"),
            3_350_000_000L);

    public static Gemma4ModelDescriptor E4B { get; } =
        new(
            Gemma4ModelVariant.E4B,
            "Gemma 4 E4B Q4_0",
            "gemma-4-E4B_q4_0-it.gguf",
            new Uri(
                "https://huggingface.co/google/gemma-4-E4B-it-qat-q4_0-gguf"),
            new Uri(
                "https://huggingface.co/google/gemma-4-E4B-it-qat-q4_0-gguf/resolve/main/gemma-4-E4B_q4_0-it.gguf?download=true"),
            5_150_000_000L);

    public static Gemma4ModelDescriptor GetDescriptor(
        Gemma4ModelVariant variant) =>
        variant == Gemma4ModelVariant.E2B
            ? E2B
            : E4B;

    public static string GetDefaultModelsDirectory() =>
        Path.Combine(
            FileSystem.AppDataDirectory,
            "Models");

    public static string GetDestinationPath(
        Gemma4ModelDescriptor model,
        string? destinationDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        string modelsDirectory =
            string.IsNullOrWhiteSpace(destinationDirectory)
                ? GetDefaultModelsDirectory()
                : Path.GetFullPath(destinationDirectory);

        return Path.Combine(
            modelsDirectory,
            model.FileName);
    }

    public async Task<string> DownloadAsync(
        Gemma4ModelDescriptor model,
        string? destinationDirectory = null,
        IProgress<Gemma4ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        string modelsDirectory =
            string.IsNullOrWhiteSpace(destinationDirectory)
                ? GetDefaultModelsDirectory()
                : Path.GetFullPath(destinationDirectory);

        Directory.CreateDirectory(modelsDirectory);

        string destinationPath =
            GetDestinationPath(
                model,
                modelsDirectory);

        if (QuizLlmModelStore.IsSupportedModelPath(
                destinationPath))
        {
            long existingSize =
                new FileInfo(destinationPath).Length;

            progress?.Report(
                new Gemma4ModelDownloadProgress(
                    existingSize,
                    existingSize));

            return destinationPath;
        }

        string partialPath =
            Path.Combine(
                modelsDirectory,
                $".{model.FileName}.downloading.gguf");

        for (int requestAttempt = 0;
             requestAttempt < 2;
             requestAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long resumeOffset =
                GetValidPartialLength(partialPath);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    model.DownloadUri);

            request.Headers.UserAgent.ParseAdd(
                "MathSolver/0.2");

            if (resumeOffset > 0)
            {
                request.Headers.Range =
                    new RangeHeaderValue(
                        resumeOffset,
                        null);
            }

            using HttpResponseMessage response =
                await HttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            if (response.StatusCode ==
                    HttpStatusCode.RequestedRangeNotSatisfiable &&
                resumeOffset > 0)
            {
                if (response.Content.Headers.ContentRange?.Length is
                        long remoteLength &&
                    remoteLength == resumeOffset &&
                    QuizLlmModelStore.IsSupportedModelPath(
                        partialPath))
                {
                    File.Move(
                        partialPath,
                        destinationPath,
                        overwrite: true);

                    progress?.Report(
                        new Gemma4ModelDownloadProgress(
                            remoteLength,
                            remoteLength));

                    return destinationPath;
                }

                File.Delete(partialPath);
                continue;
            }

            response.EnsureSuccessStatusCode();

            bool isResuming =
                resumeOffset > 0 &&
                response.StatusCode ==
                    HttpStatusCode.PartialContent;

            if (!isResuming)
            {
                resumeOffset = 0;
            }

            long? totalBytes =
                response.Content.Headers.ContentRange?.Length;

            if (totalBytes is null &&
                response.Content.Headers.ContentLength is long contentLength)
            {
                totalBytes = resumeOffset + contentLength;
            }

            if (totalBytes is >
                QuizLlmModelStore.MaximumModelFileSizeBytes)
            {
                File.Delete(partialPath);
                throw new QuizLlmModelTooLargeException();
            }

            await CopyResponseToFileAsync(
                response,
                partialPath,
                resumeOffset,
                totalBytes,
                progress,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!QuizLlmModelStore.IsSupportedModelPath(
                    partialPath))
            {
                File.Delete(partialPath);
                throw new InvalidDataException(
                    "The downloaded file is not a supported Gemma 4 GGUF model.");
            }

            File.Move(
                partialPath,
                destinationPath,
                overwrite: true);

            return destinationPath;
        }

        throw new HttpRequestException(
            "The model download could not be restarted.");
    }

    private static long GetValidPartialLength(
        string partialPath)
    {
        if (!File.Exists(partialPath))
        {
            return 0;
        }

        long length =
            new FileInfo(partialPath).Length;

        if (length <= 0 ||
            length > QuizLlmModelStore.MaximumModelFileSizeBytes)
        {
            File.Delete(partialPath);
            return 0;
        }

        return length;
    }

    private static async Task CopyResponseToFileAsync(
        HttpResponseMessage response,
        string partialPath,
        long resumeOffset,
        long? totalBytes,
        IProgress<Gemma4ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using Stream source =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        await using var destination =
            new FileStream(
                partialPath,
                resumeOffset > 0
                    ? FileMode.Append
                    : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        byte[] buffer =
            ArrayPool<byte>.Shared.Rent(BufferSize);

        long bytesReceived = resumeOffset;
        var progressTimer = Stopwatch.StartNew();

        progress?.Report(
            new Gemma4ModelDownloadProgress(
                bytesReceived,
                totalBytes));

        try
        {
            while (true)
            {
                int read =
                    await source.ReadAsync(
                        buffer.AsMemory(0, BufferSize),
                        cancellationToken);

                if (read == 0)
                {
                    break;
                }

                bytesReceived += read;

                if (bytesReceived >
                    QuizLlmModelStore.MaximumModelFileSizeBytes)
                {
                    throw new QuizLlmModelTooLargeException();
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);

                if (progressTimer.ElapsedMilliseconds >= 250)
                {
                    progress?.Report(
                        new Gemma4ModelDownloadProgress(
                            bytesReceived,
                            totalBytes));

                    progressTimer.Restart();
                }
            }

            await destination.FlushAsync(
                cancellationToken);

            if (totalBytes is > 0 &&
                bytesReceived != totalBytes.Value)
            {
                throw new IOException(
                    "The model download ended before all bytes were received.");
            }

            progress?.Report(
                new Gemma4ModelDownloadProgress(
                    bytesReceived,
                    totalBytes));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
