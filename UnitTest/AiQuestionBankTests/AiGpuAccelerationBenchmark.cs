using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Opt-in real app inference; preferences are held by TestHost's memory stub.</summary>
internal static class AiGpuAccelerationBenchmark
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task RunAsync(string model, string directory)
    {
        if (!OperatingSystem.IsWindows()) throw new TestUsageException("The AI GPU benchmark requires Windows.");
        if (!File.Exists(model)) throw new TestUsageException("GGUF file not found: " + model);
        model = Path.GetFullPath(model);
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);

        // Create() is the legacy numeric v1 contract; the app runtime accepts templates.
        var contract = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.ThreeStars,
            AppLanguage.Vietnamese, new Random(713));
        string prompt = BasicQuestionPrompt.Build(contract);
        var runtime = new GgufQuestionRuntime();
        QuestionSamplingSettings sampling = runtime.Sampling with { };
        runtime.Sampling = sampling;
        AiComputeMode originalMode = AiAccelerationManager.Mode;
        using var overall = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var report = new BenchmarkReport
        {
            StartedUtc = DateTimeOffset.UtcNow,
            ProcessId = Environment.ProcessId,
            ModelPath = model,
            ModelBytes = new FileInfo(model).Length,
            Adapter = AiAccelerationManager.AdapterName,
            PreferredBackend = AiAccelerationManager.PreferredBackend,
            OriginalMode = originalMode,
            Sampling = sampling,
            Contract = contract,
            Prompt = prompt
        };
        await File.WriteAllTextAsync(Path.Combine(directory, "prompt.txt"), prompt);
        await SaveAsync();
        try
        {
            Check(contract.IsValid && contract.IsTemplate && !contract.OpenProse, "Benchmark requires a valid closed-grammar app contract.");
            await RunModeAsync("cpu", AiComputeMode.Cpu, generate: true);
            await RunModeAsync("gpu", AiComputeMode.Gpu, generate: true);
            await RunModeAsync("cpu-recheck", AiComputeMode.Cpu, generate: false);
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
                await runtime.EjectAsync();
                report.WeightsReleased = !runtime.IsLoaded;
            }
            finally
            {
                AiAccelerationManager.SetMode(originalMode);
                report.ModeRestored = AiAccelerationManager.Mode == originalMode;
                report.FinishedUtc = DateTimeOffset.UtcNow;
                await SaveAsync();
            }
        }

        Console.WriteLine("PASS real app CPU -> GPU -> CPU reload and offload checks. Evidence: " + directory);
        foreach (ModeResult mode in report.Modes.Where(value => value.Attempts.Count > 0))
            Console.WriteLine($"{mode.Name}: {mode.ActiveBackend}, {mode.ActiveDeviceName}, layers={mode.GpuLayerCount}, " +
                $"load={mode.LoadMilliseconds:F0} ms, measured decode={mode.AggregateMeasuredDecodeTokensPerSecond:F2} token/s " +
                "(one warmup excluded; two measured generations).");

        async Task RunModeAsync(string name, AiComputeMode requested, bool generate)
        {
            overall.Token.ThrowIfCancellationRequested();
            // Release before changing the requested mode: never keep two weight sets resident.
            await runtime.ReleaseAsync();
            Check(!runtime.IsLoaded, "Previous mode retained native model weights.");
            AiAccelerationManager.SetMode(requested);
            var mode = new ModeResult { Name = name, RequestedMode = requested, EffectiveRequestedMode = AiAccelerationManager.Mode };
            report.Modes.Add(mode);
            var load = Stopwatch.StartNew();
            try
            {
                using var deadline = CallDeadline();
                await runtime.LoadAsync(model, deadline.Token);
                mode.LoadMilliseconds = load.Elapsed.TotalMilliseconds;
                CaptureBackend(mode);
                await SaveModeAsync(mode);
                if (requested == AiComputeMode.Cpu)
                    Check(runtime.ActiveBackend == AiGpuBackend.Cpu && runtime.GpuLayerCount == 0,
                        name + " did not load with zero GPU layers on the CPU backend.");
                else
                    Check(AiAccelerationManager.Mode == AiComputeMode.Gpu &&
                        runtime.ActiveBackend is AiGpuBackend.Cuda or AiGpuBackend.Vulkan && runtime.GpuLayerCount > 0,
                        "GPU acceleration unavailable; explicit CPU fallback: " + runtime.AccelerationFallbackReason);

                Check(runtime.ModelPath == model && runtime.Sampling == sampling,
                    "Mode change altered the selected model or sampling settings.");
                if (generate)
                    for (int attempt = 0; attempt < 3; attempt++)
                        await GenerateAsync(mode, attempt, warmup: attempt == 0);
                mode.Completed = true;
            }
            catch (Exception error)
            {
                mode.Error = error.ToString();
                throw;
            }
            finally
            {
                if (mode.LoadMilliseconds is null) mode.LoadMilliseconds = load.Elapsed.TotalMilliseconds;
                CaptureBackend(mode);
                await runtime.ReleaseAsync();
                mode.WeightsReleased = !runtime.IsLoaded;
                mode.SelectionPreserved = runtime.ModelPath == model && runtime.CanGenerate;
                await SaveModeAsync(mode);
            }
            Check(mode.WeightsReleased && mode.SelectionPreserved, name + " cleanup retained weights or removed model selection.");
        }

        async Task GenerateAsync(ModeResult mode, int index, bool warmup)
        {
            var attempt = new AttemptResult { Index = index, Warmup = warmup };
            mode.Attempts.Add(attempt);
            var streamed = new StringBuilder();
            long started = Stopwatch.GetTimestamp();
            long firstToken = 0;
            AiGenerationMetrics? metrics = null;
            try
            {
                using var deadline = CallDeadline();
                attempt.RawJson = await runtime.GenerateAsync(contract, prompt, deadline.Token,
                    text => streamed.Append(text), value =>
                    {
                        if (firstToken == 0) firstToken = Stopwatch.GetTimestamp();
                        metrics = value;
                    });
                attempt.TotalMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                attempt.TimeToFirstTokenMilliseconds = firstToken == 0 ? null : Stopwatch.GetElapsedTime(started, firstToken).TotalMilliseconds;
                attempt.Metrics = metrics;
                attempt.Validation = BasicQuestionValidator.Validate(attempt.RawJson, contract);
                Check(streamed.ToString() == attempt.RawJson && metrics?.GeneratedTokens > 1,
                    "Native generation lost streamed output or token metrics.");
                Check(metrics?.TokensPerSecond is double speed && double.IsFinite(speed) && speed > 0,
                    "Generation produced no finite positive decode throughput.");
                Check(attempt.Validation.IsValid,
                    mode.Name + " output failed app validation: " + attempt.Validation.ErrorCode + "; " + attempt.Validation.ErrorDetails);
                attempt.Completed = true;
            }
            catch (Exception error)
            {
                attempt.Error = error.ToString();
                attempt.RawJson ??= streamed.ToString();
                attempt.Metrics ??= metrics;
                attempt.TotalMilliseconds ??= Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                attempt.TimeToFirstTokenMilliseconds ??= firstToken == 0 ? null : Stopwatch.GetElapsedTime(started, firstToken).TotalMilliseconds;
                attempt.Validation ??= BasicQuestionValidator.Validate(attempt.RawJson, contract);
                throw;
            }
            finally
            {
                CaptureBackend(mode);
                string stem = mode.Name + "-" + (warmup ? "warmup" : "measured-" + index);
                await File.WriteAllTextAsync(Path.Combine(directory, stem + ".json"), attempt.RawJson ?? streamed.ToString());
                await SaveModeAsync(mode);
            }
        }

        CancellationTokenSource CallDeadline()
        {
            var deadline = CancellationTokenSource.CreateLinkedTokenSource(overall.Token);
            deadline.CancelAfter(TimeSpan.FromMinutes(5));
            return deadline;
        }

        void CaptureBackend(ModeResult mode)
        {
            mode.ActiveBackend = runtime.ActiveBackend;
            mode.GpuLayerCount = runtime.GpuLayerCount;
            mode.ActiveDeviceName = runtime.ActiveDeviceName;
            mode.AccelerationFallbackReason = runtime.AccelerationFallbackReason;
            mode.NativeLoadDiagnostics = runtime.NativeLoadDiagnostics.ToArray();
            mode.BackendDevices = runtime.BackendDevices.ToArray();
        }

        async Task SaveModeAsync(ModeResult mode)
        {
            await File.WriteAllLinesAsync(Path.Combine(directory, mode.Name + "-native.log"), mode.NativeLoadDiagnostics);
            await SaveAsync();
        }

        Task SaveAsync() => File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(report, JsonOptions));
    }

    private sealed class BenchmarkReport
    {
        public DateTimeOffset StartedUtc { get; init; }
        public DateTimeOffset? FinishedUtc { get; set; }
        public int ProcessId { get; init; }
        public string ModelPath { get; init; } = "";
        public long ModelBytes { get; init; }
        public string Adapter { get; init; } = "";
        public AiGpuBackend PreferredBackend { get; init; }
        public AiComputeMode OriginalMode { get; init; }
        public QuestionSamplingSettings Sampling { get; init; } = new();
        public BasicQuestionContract? Contract { get; init; }
        public string Prompt { get; init; } = "";
        public string Methodology => "Same process, model, prompt, closed app grammar and sampling settings; random sampling seeds remain app defaults. " +
            "CPU then GPU each use one excluded warmup and two measured generations, followed by a CPU model-load recheck. " +
            "Decode throughput is (streamed token count - 1) / first-to-last-token wall time, including grammar/sampling/callback overhead. " +
            "TTFT includes context creation, tokenization, prompt prefill and first sampling; total includes context cleanup. " +
            "Native prompt/decode timings are not claimed. Cancellation is cooperative: five minutes per native call, twenty minutes overall.";
        public List<ModeResult> Modes { get; } = [];
        public bool Completed { get; set; }
        public bool WeightsReleased { get; set; }
        public bool ModeRestored { get; set; }
        public string? Error { get; set; }
    }

    private sealed class ModeResult
    {
        public string Name { get; init; } = "";
        public AiComputeMode RequestedMode { get; init; }
        public AiComputeMode EffectiveRequestedMode { get; init; }
        public AiGpuBackend ActiveBackend { get; set; }
        public int GpuLayerCount { get; set; }
        public string ActiveDeviceName { get; set; } = "";
        public string? AccelerationFallbackReason { get; set; }
        public string[] NativeLoadDiagnostics { get; set; } = [];
        public AiNativeGpuDevice[] BackendDevices { get; set; } = [];
        public double? LoadMilliseconds { get; set; }
        public List<AttemptResult> Attempts { get; } = [];
        public double? AggregateMeasuredDecodeTokensPerSecond
        {
            get
            {
                var measured = Attempts.Where(value => !value.Warmup && value.Completed && value.Metrics is not null).ToArray();
                double seconds = measured.Sum(value => value.Metrics!.GenerationElapsed.TotalSeconds);
                return measured.Length > 0 && seconds > 0 ? measured.Sum(value => value.Metrics!.GeneratedTokens - 1) / seconds : null;
            }
        }
        public double? MeanMeasuredTimeToFirstTokenMilliseconds =>
            Attempts.Where(value => !value.Warmup && value.Completed).Select(value => value.TimeToFirstTokenMilliseconds).Average();
        public bool Completed { get; set; }
        public bool WeightsReleased { get; set; }
        public bool SelectionPreserved { get; set; }
        public string? Error { get; set; }
    }

    private sealed class AttemptResult
    {
        public int Index { get; init; }
        public bool Warmup { get; init; }
        public double? TimeToFirstTokenMilliseconds { get; set; }
        public double? TotalMilliseconds { get; set; }
        public AiGenerationMetrics? Metrics { get; set; }
        public string? RawJson { get; set; }
        public BasicDraftValidation? Validation { get; set; }
        public bool Completed { get; set; }
        public string? Error { get; set; }
    }
}
