using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed record AiModelDownload(string Name, string Repository, string FileName);
public sealed record AiDownloadProgress(long Received, long? Total);

/// <summary>Models stay in app-owned storage on Android; Windows selections may stay external.</summary>
public sealed class AiModelLibrary(string directory)
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };
    public string DirectoryPath { get; } = directory;
    public static IReadOnlyList<AiModelDownload> Downloads { get; } =
    [
        new("Gemma 4 E2B · Q4_0", "google/gemma-4-E2B-it-qat-q4_0-gguf", "gemma-4-E2B_q4_0-it.gguf"),
        new("Gemma 4 E4B · Q4_0", "google/gemma-4-E4B-it-qat-q4_0-gguf", "gemma-4-E4B_q4_0-it.gguf")
    ];

    public IReadOnlyList<string> Files()
    {
        Directory.CreateDirectory(DirectoryPath);
        return Directory.GetFiles(DirectoryPath, "*.gguf").OrderBy(Path.GetFileName).ToArray();
    }

    public async Task<string> ImportAsync(Stream source, string fileName, CancellationToken cancellationToken)
    {
        string name = Path.GetFileName(fileName);
        if (!name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("InvalidModelFile");
        Directory.CreateDirectory(DirectoryPath);
        string destination = Path.Combine(DirectoryPath, name);
        string partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            await CheckGgufAsync(partial, cancellationToken).ConfigureAwait(false);
            // Never replace a loaded model in place.
            if (File.Exists(destination)) destination = Path.Combine(DirectoryPath, Path.GetFileNameWithoutExtension(name) + "-" + Guid.NewGuid().ToString("N")[..8] + ".gguf");
            File.Move(partial, destination);
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    public async Task<string> DownloadAsync(AiModelDownload model,
        IProgress<AiDownloadProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DirectoryPath);
        string destination = Path.Combine(DirectoryPath, model.FileName);
        if (File.Exists(destination)) { await CheckGgufAsync(destination, cancellationToken).ConfigureAwait(false); return destination; }
        string partial = destination + ".partial";
        // Resolve a specific revision and expected size from the official repository.
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, $"https://huggingface.co/api/models/{model.Repository}?blobs=true");
        using var metadataResponse = await Client.SendAsync(metadataRequest, cancellationToken).ConfigureAwait(false);
        if (metadataResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new InvalidOperationException("HuggingFaceAccessRequired");
        metadataResponse.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        string revision = metadata.RootElement.GetProperty("sha").GetString()!;
        string? expectedHash = null;
        long? expectedSize = null;
        foreach (var file in metadata.RootElement.GetProperty("siblings").EnumerateArray())
        {
            if (file.GetProperty("rfilename").GetString() != model.FileName || !file.TryGetProperty("lfs", out var lfs)) continue;
            expectedHash = lfs.TryGetProperty("sha256", out var sha) ? sha.GetString() : null;
            expectedSize = lfs.TryGetProperty("size", out var size) ? size.GetInt64() : null;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://huggingface.co/{model.Repository}/resolve/{revision}/{model.FileName}");
        try
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new InvalidOperationException("HuggingFaceAccessRequired");
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            long received = 0;
            using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                byte[] buffer = new byte[1024 * 1024];
                var lastReport = DateTime.MinValue;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    checksum.AppendData(buffer, 0, read);
                    received += read;
                    if ((DateTime.UtcNow - lastReport).TotalMilliseconds >= 250)
                    { progress?.Report(new(received, total)); lastReport = DateTime.UtcNow; }
                }
            }
            if (total.HasValue && received != total) throw new InvalidDataException("IncompleteModelDownload");
            if (expectedSize.HasValue && received != expectedSize) throw new InvalidDataException("IncompleteModelDownload");
            if (expectedHash is not null && !Convert.ToHexString(checksum.GetHashAndReset()).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("IncompleteModelDownload");
            await CheckGgufAsync(partial, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, destination, false);
            progress?.Report(new(received, total));
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    public static async Task CheckGgufAsync(string path, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] magic = new byte[4];
        int count = await input.ReadAsync(magic, cancellationToken).ConfigureAwait(false);
        if (count != 4 || !magic.AsSpan().SequenceEqual("GGUF"u8)) throw new InvalidDataException("InvalidModelFile");
    }
}
