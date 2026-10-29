using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>Opt-in Vulkan package smoke check; invoke in its own process.</summary>
internal static class AiVulkanSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task RunAsync(string model, string directory)
    {
        if (!OperatingSystem.IsWindows()) throw new TestUsageException("The Vulkan smoke check requires Windows.");
        if (!File.Exists(model)) throw new TestUsageException("GGUF file not found: " + model);
        model = Path.GetFullPath(model);
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        var messages = new ConcurrentQueue<string>();
        var report = new SmokeResult
        {
            StartedUtc = DateTimeOffset.UtcNow,
            ProcessId = Environment.ProcessId,
            ModelPath = model,
            ModelBytes = new FileInfo(model).Length,
            Adapter = AiAccelerationManager.AdapterName
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        LLamaWeights? weights = null;
        var output = new StringBuilder();
        long firstToken = 0;
        long generationStarted = 0;
        int generatedTokens = 0;
        try
        {
            if (NativeLibraryConfig.LLama.LibraryHasLoaded || NativeLibraryConfig.Mtmd.LibraryHasLoaded)
                throw new TestUsageException("Run the Vulkan smoke check in a separate process before any other native model command.");
            // This must precede NativeApi and ModelParams: native selection is process-wide.
            NativeLibraryConfig.All.WithLogCallback((level, text) => messages.Enqueue(text));
            NativeLibraryConfig.All.WithCuda(false).WithVulkan(true).WithAutoFallback(false);

            report.SupportsGpuOffload = NativeApi.llama_supports_gpu_offload();
            var library = NativeApi.GetLoadedNativeLibrary(NativeLibraryName.LLama);
            report.LibraryUsesVulkan = library?.Metadata?.UseVulkan == true;
            report.LibraryUsesCuda = library?.Metadata?.UseCuda == true;
            report.NativeLibraryDescription = library?.Metadata?.ToString();
            int count = checked((int)NativeApi.ggml_backend_dev_count());
            Check(count is >= 0 and <= 1024, "Invalid native backend device count.");
            report.RegisteredDevices = Enumerable.Range(0, count)
                .Select(index => Marshal.PtrToStringUTF8(NativeApi.ggml_backend_dev_name(NativeApi.ggml_backend_dev_get((nuint)index))))
                .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).ToArray();
            report.ActiveDeviceName = report.RegisteredDevices.FirstOrDefault(name => name.StartsWith("Vulkan", StringComparison.OrdinalIgnoreCase)) ?? "";
            Check(report.SupportsGpuOffload && report.LibraryUsesVulkan && !report.LibraryUsesCuda && report.ActiveDeviceName.Length > 0,
                "Vulkan backend/device unavailable; inspect native.log. This check does not accept CPU fallback.");

            var parameters = GgufQuestionRuntime.CreateModelParameters(model, AiComputeMode.Gpu, report.ActiveDeviceName);
            report.RequestedGpuLayers = parameters.GpuLayerCount;
            report.InferenceThreads = parameters.Threads;
            report.PromptThreads = parameters.BatchThreads;
            long loadStarted = Stopwatch.GetTimestamp();
            try { weights = await LLamaWeights.LoadFromFileAsync(parameters, timeout.Token); }
            finally { report.ModelLoadMilliseconds = Stopwatch.GetElapsedTime(loadStarted).TotalMilliseconds; }
            timeout.Token.ThrowIfCancellationRequested();
            report.ModelLayers = weights.NativeHandle.LayerCount;
            report.ModelMetadata = weights.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value);
            string loadLog = string.Concat(messages);
            report.OffloadedLayers = ReadOffloadedLayers(loadLog);
            var buffer = Regex.Match(loadLog, @"\b" + Regex.Escape(report.ActiveDeviceName) + @"\s+model buffer size\s*=\s*([0-9.]+)",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            report.VulkanModelBufferMiB = buffer.Success && double.TryParse(buffer.Groups[1].Value,
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double size) ? size : null;
            Check(report.OffloadedLayers > 0 && report.VulkanModelBufferMiB is > 0,
                "Vulkan model placement was not confirmed by positive layer and model-buffer diagnostics.");

            var contract = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.ThreeStars,
                AppLanguage.Vietnamese, new Random(713));
            report.Contract = contract;
            report.Prompt = BasicQuestionPrompt.Build(contract);
            generationStarted = Stopwatch.GetTimestamp();
            string input = GgufQuestionRuntime.FormatChatPrompt(weights, contract, report.Prompt);
            report.PromptTokens = weights.Tokenize(input, true, true, parameters.Encoding).Length;
            report.ContextTokens = GgufQuestionRuntime.GetContextTokens(report.PromptTokens);
            parameters = parameters with { ContextSize = (uint)report.ContextTokens };
            var settings = report.Sampling;
            using var sampling = new DefaultSamplingPipeline
            {
                Temperature = settings.Temperature, TopP = settings.TopP, TopK = settings.TopK,
                MinP = settings.MinP, RepeatPenalty = settings.RepeatPenalty,
                GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Basic,
                Grammar = new Grammar(GgufQuestionRuntime.BuildGrammar(contract), "root")
            };
            var executor = new StatelessExecutor(weights, parameters) { ApplyTemplate = false };
            try
            {
                await foreach (string text in executor.InferAsync(input, new InferenceParams
                {
                    MaxTokens = GgufQuestionRuntime.MaximumOutputTokens,
                    OverflowStrategy = ContextOverflowStrategy.ThrowException,
                    SamplingPipeline = sampling,
                    AntiPrompts = ["<end_of_turn>", "<|im_end|>", "<|turn>", "<turn|>"]
                }, timeout.Token))
                {
                    if (generatedTokens++ == 0) firstToken = Stopwatch.GetTimestamp();
                    report.Metrics = new(generatedTokens, Stopwatch.GetElapsedTime(firstToken));
                    output.Append(text);
                    if (output.Length > 12_000) throw new InvalidDataException("ModelOutputTooLong");
                }
                timeout.Token.ThrowIfCancellationRequested();
                report.GenerationTotalMilliseconds = Stopwatch.GetElapsedTime(generationStarted).TotalMilliseconds;
                report.TimeToFirstTokenMilliseconds = firstToken == 0 ? null : Stopwatch.GetElapsedTime(generationStarted, firstToken).TotalMilliseconds;
                report.RawJson = output.ToString();
                report.Validation = BasicQuestionValidator.Validate(report.RawJson, contract);
                Check(report.Validation.IsValid, "Vulkan generation failed app validation: " + report.Validation.ErrorCode + "; " + report.Validation.ErrorDetails);
                Check(report.Metrics?.TokensPerSecond is double speed && double.IsFinite(speed) && speed > 0,
                    "Vulkan generation did not produce positive wall-clock decode metrics.");
            }
            finally { executor.Context?.Dispose(); }
            report.Completed = true;
        }
        catch (Exception error)
        {
            report.Error = error.ToString();
            throw;
        }
        finally
        {
            try
            {
                weights?.Dispose();
                report.WeightsReleased = weights is null || weights.NativeHandle.IsClosed;
            }
            finally
            {
                report.RawJson ??= output.ToString();
                if (generationStarted != 0)
                {
                    report.GenerationTotalMilliseconds ??= Stopwatch.GetElapsedTime(generationStarted).TotalMilliseconds;
                    report.TimeToFirstTokenMilliseconds ??= firstToken == 0 ? null : Stopwatch.GetElapsedTime(generationStarted, firstToken).TotalMilliseconds;
                }
                if (report.Contract is not null && report.Validation is null)
                {
                    try { report.Validation = BasicQuestionValidator.Validate(report.RawJson, report.Contract); }
                    catch (Exception validationError) { report.Error ??= validationError.ToString(); }
                }
                report.NativeLogs = messages.ToArray();
                report.FinishedUtc = DateTimeOffset.UtcNow;
                await File.WriteAllTextAsync(Path.Combine(directory, "raw.json"), report.RawJson);
                await File.WriteAllTextAsync(Path.Combine(directory, "prompt.txt"), report.Prompt ?? "");
                await File.WriteAllTextAsync(Path.Combine(directory, "native.log"), string.Concat(report.NativeLogs));
                await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(report, JsonOptions));
            }
        }
        Console.WriteLine($"PASS Vulkan package on {report.Adapter}: {report.ActiveDeviceName}, " +
            $"{report.OffloadedLayers}/{report.ModelLayers + 1} layers, {report.Metrics!.TokensPerSecond:F2} wall decode token/s. " +
            "This is validation on this adapter, not AMD/Intel hardware coverage. Evidence: " + directory);
    }

    private static int ReadOffloadedLayers(string log)
    {
        var matches = Regex.Matches(log, @"offloaded\s+(\d+)\s*/\s*\d+\s+layers to GPU",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return matches.Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).DefaultIfEmpty().Max();
    }

    private sealed class SmokeResult
    {
        public DateTimeOffset StartedUtc { get; init; }
        public DateTimeOffset? FinishedUtc { get; set; }
        public int ProcessId { get; init; }
        public string ModelPath { get; init; } = "";
        public long ModelBytes { get; init; }
        public string Adapter { get; init; } = "";
        public string HardwareCoverage => "Actual adapter named above only; Vulkan success on NVIDIA is not AMD/Intel hardware testing.";
        public string Methodology => "Separate-process Vulkan-only native selection, one real generation with app model parameters/chat framing/closed grammar/sampling. " +
            "No warmup or average. TTFT includes chat framing/tokenization/context creation/prefill/first sampling; wall decode is " +
            "(streamed tokens - 1) / first-to-last-token elapsed, including sampling/grammar/callback overhead. Empty UTF-8 deltas count as tokens. " +
            "Twenty-minute cooperative deadline allows initial Vulkan shader compilation. No pure native prompt/decode timing is claimed.";
        public bool SupportsGpuOffload { get; set; }
        public bool LibraryUsesVulkan { get; set; }
        public bool LibraryUsesCuda { get; set; }
        public string? NativeLibraryDescription { get; set; }
        public string[] RegisteredDevices { get; set; } = [];
        public string ActiveDeviceName { get; set; } = "";
        public int RequestedGpuLayers { get; set; }
        public int ModelLayers { get; set; }
        public int OffloadedLayers { get; set; }
        public double? VulkanModelBufferMiB { get; set; }
        public Dictionary<string, string>? ModelMetadata { get; set; }
        public int? InferenceThreads { get; set; }
        public int? PromptThreads { get; set; }
        public BasicQuestionContract? Contract { get; set; }
        public string? Prompt { get; set; }
        public QuestionSamplingSettings Sampling { get; } = new();
        public int PromptTokens { get; set; }
        public int ContextTokens { get; set; }
        public double? ModelLoadMilliseconds { get; set; }
        public double? TimeToFirstTokenMilliseconds { get; set; }
        public double? GenerationTotalMilliseconds { get; set; }
        public AiGenerationMetrics? Metrics { get; set; }
        public string? RawJson { get; set; }
        public BasicDraftValidation? Validation { get; set; }
        public string[] NativeLogs { get; set; } = [];
        public bool Completed { get; set; }
        public bool WeightsReleased { get; set; }
        public string? Error { get; set; }
    }
}
