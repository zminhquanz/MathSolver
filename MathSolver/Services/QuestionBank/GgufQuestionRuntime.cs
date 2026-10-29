using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;
using System.Diagnostics;
using System.Text;

namespace MathSolver.Services.QuestionBank;

/// <summary>One loaded model and one native context at a time on Windows/Android.</summary>
public sealed partial class GgufQuestionRuntime : IQuestionTextRuntime
{
    private const string JsonGrammar = """
        root ::= "{" ws "\"given_a\"" ws ":" ws string-a ws "," ws "\"given_b\"" ws ":" ws string-b ws "," ws "\"question\"" ws ":" ws string-q ws "," ws "\"solution_lead\"" ws ":" ws string ws "," ws "\"unit_id\"" ws ":" ws unit ws "}" ws
        string-a ::= "\"" "{A_PREFIX}{ACTOR_A}" char{0,180} "{a} {unit}" char{0,180} "\""
        string-b ::= "\"" "{ACTOR_B}" char{0,180} "{b}" char{0,180} "\""
        string-q ::= "\"" char{0,180} "{ACTOR_Q}" char{0,180} "\""
        string ::= "\"" char{1,400} "\""
        char ::= [{PROSE_LETTERS} ,.'?!:\u2019-] | placeholder
        placeholder ::= "{name}" | "{other}" | "{unit}" | "{group}" | "{group_one}"
        unit ::= "\"" ("books" | "notebooks" | "pencils" | "candies" | "apples" | "oranges" | "flowers" | "cards" | "balls" | "stickers" | "cakes") "\""
        ws ::= [ \t\n\r]*
        """;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LLamaWeights? _weights;
    private ModelParams? _parameters;
    private QuestionSamplingSettings _sampling = new();
    public QuestionSamplingSettings Sampling { get => _sampling; set { ArgumentNullException.ThrowIfNull(value); value.Validate(); _sampling = value; } }
    public bool IsLoaded => _weights is not null;
    public bool CanGenerate => ModelPath.Length > 0;
    public string ModelName { get; private set; } = "";
    public string ModelPath { get; private set; } = "";
    public int InferenceThreadCount => IsLoaded ? _parameters?.Threads ?? 0 : 0;
    public int PromptThreadCount => IsLoaded ? _parameters?.BatchThreads ?? 0 : 0;
    internal const int MaximumContextTokens = 2048;
    internal const int MaximumOutputTokens = 700;
    public int LastContextTokens { get; private set; }

    internal static int GetContextTokens(int promptTokens)
    {
        if (promptTokens < 0) throw new ArgumentOutOfRangeException(nameof(promptTokens));
        long required = (long)promptTokens + MaximumOutputTokens + 64;
        if (required > MaximumContextTokens) throw new InvalidDataException("PromptTooLong");
        return Math.Max(1024, (int)((required + 255) / 256 * 256));
    }

    // Selecting/importing a model must not keep gigabytes of unused weights resident.
    public async Task SelectAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ValidateModelPath(path);
            await using var file = File.OpenRead(path);
            var magic = new byte[4];
            await file.ReadExactlyAsync(magic, cancellationToken).ConfigureAwait(false);
            if (!magic.AsSpan().SequenceEqual("GGUF"u8)) throw new InvalidDataException("Choose a GGUF model file.");
            await Task.Run(DisposeWeights).ConfigureAwait(false);
            ModelPath = Path.GetFullPath(path);
            ModelName = Path.GetFileName(path);
        }
        finally { _gate.Release(); }
    }

    private static void ValidateModelPath(string path)
    {
        if (!File.Exists(path) || !path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a GGUF model file.");
    }

    public Task PrepareAsync(CancellationToken cancellationToken)
        => IsLoaded && _loadedMode == AiAccelerationManager.Mode ? Task.CompletedTask : LoadAsync(ModelPath, cancellationToken);

    public Task ReleaseAsync() => ReleaseWeightsAsync(clearSelection: false);

    private void DisposeWeights()
    {
        var weights = _weights;
        _weights = null;
        _parameters = null;
        ActiveBackend = AiGpuBackend.Cpu;
        GpuLayerCount = 0;
        ActiveDeviceName = "";
        weights?.Dispose();
    }

    // This is a worker-thread budget, not an operating-system CPU-utilisation
    // limiter. Use the same budget for prompt processing and token decoding.
    internal static int GetInferenceThreadCount(int logicalProcessorCount)
    {
        if (logicalProcessorCount < 1) throw new ArgumentOutOfRangeException(nameof(logicalProcessorCount));
        return Math.Max(1, (int)((long)logicalProcessorCount * 3 / 4));
    }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateModelPath(path);
                ConfigureNativeAcceleration();
                // Eject first: switching models must not double peak resident weights.
                DisposeWeights();
                ModelName = Path.GetFileName(path);
                ModelPath = Path.GetFullPath(path);
                await LoadForComputeModeAsync(path,cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public Task EjectAsync() => ReleaseWeightsAsync(clearSelection: true);

    private async Task ReleaseWeightsAsync(bool clearSelection)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                DisposeWeights();
                if (clearSelection) { ModelName = ""; ModelPath = ""; }
            }).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        => GenerateNovelAsync(contract, prompt, new HashSet<string>(StringComparer.Ordinal), cancellationToken, onText, onMetrics);

    internal static string FormatChatPrompt(LLamaWeights weights, BasicQuestionContract contract, string prompt)
    {
        weights.Metadata.TryGetValue("general.architecture", out string? architecture);
        string system = BasicQuestionPrompt.SystemInstruction(contract.Language);
        if (architecture == "gemma4")
        {
            // Gemma 4 supports a real system turn. Omit <|think|> for E2B/E4B
            // text-only, thinking-off inference; the executor adds BOS itself.
            // https://huggingface.co/google/gemma-4-E4B-it/raw/main/chat_template.jinja
            return $"<|turn>system\n{system}<turn|>\n<|turn>user\n{prompt}<turn|>\n<|turn>model\n";
        }
        var template = new LLamaTemplate(weights.NativeHandle) { AddAssistant = true };
        if (architecture?.StartsWith("gemma", StringComparison.Ordinal) == true)
            // Earlier Gemma templates accept only user/model turns.
            template.Add("user", system + "\n\n" + prompt);
        else
        {
            template.Add("system", system);
            template.Add("user", prompt);
        }
        return PromptTemplateTransformer.ToModelPrompt(template);
    }

    public async Task<string> GenerateNovelAsync(BasicQuestionContract contract, string prompt, IReadOnlySet<string> excludedProse,
        CancellationToken cancellationToken,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
    {
        if (!contract.IsTemplate || !contract.IsValid) throw new ArgumentException("InvalidContract", nameof(contract));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_weights is null || _parameters is null) throw new InvalidOperationException("ModelNotLoaded");
            var settings=Sampling;
            return await Task.Run(async () =>
            {
                string input = FormatChatPrompt(_weights, contract, prompt);
                // Count the exact chat framing without allocating a KV cache first.
                int tokens = _weights.Tokenize(input, true, true, _parameters.Encoding).Length;
                int contextTokens = GetContextTokens(tokens);
                var parameters = _parameters with { ContextSize = (uint)contextTokens };
                LastContextTokens = contextTokens;
                // Sampling owns native grammar/sampler chains too; release them
                // deterministically before the job releases the model weights.
                using var sampling = new DefaultSamplingPipeline {
                    Temperature = settings.Temperature, TopP = settings.TopP, TopK = settings.TopK, MinP = settings.MinP,
                    // Mild within-response repetition control: JSON keys and role
                    // slots legitimately recur. SQLite handles cross-question duplication.
                    RepeatPenalty = settings.RepeatPenalty,
                    // Leave Seed unset: LLamaSharp generates a fresh random seed
                    // for every pipeline, including retries within the same job.
                    // Validate the sampled candidate first; fall back to the full
                    // vocabulary when it violates the grammar. Grammar remains enforced.
                    GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Basic,
                    Grammar = new Grammar(BuildNovelGrammar(contract, excludedProse), "root") };
                var executor = new StatelessExecutor(_weights, parameters) { ApplyTemplate = false };
                var result = new StringBuilder();
                int generatedTokens = 0;
                long generationStarted = 0;
                try
                {
                    await foreach (string text in executor.InferAsync(input, new InferenceParams
                    {
                        MaxTokens = MaximumOutputTokens,
                        OverflowStrategy = ContextOverflowStrategy.ThrowException,
                        SamplingPipeline = sampling,
                        AntiPrompts = ["<end_of_turn>", "<|im_end|>", "<|turn>", "<turn|>"]
                    }, cancellationToken).ConfigureAwait(false))
                    {
                        // LLamaSharp v0.27 yields once per sampled non-EOG token, even
                        // when the UTF-8 decoder has not yet produced any text.
                        // https://github.com/SciSharp/LLamaSharp/blob/v0.27.0/LLama/LLamaStatelessExecutor.cs
                        if (generatedTokens++ == 0) generationStarted = Stopwatch.GetTimestamp();
                        onMetrics?.Invoke(new(generatedTokens, Stopwatch.GetElapsedTime(generationStarted)));
                        result.Append(text);
                        if (result.Length > 12_000) throw new InvalidDataException("ModelOutputTooLong");
                        onText?.Invoke(text);
                    }
                    return result.ToString();
                }
                finally { executor.Context?.Dispose(); }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    internal static string BuildGrammar(BasicQuestionContract c)
    {
        if (c.OpenProse) return OpenQuestionProse.Grammar(c);
        if (c.Version == ExpandedArithmeticCatalogue.Version) return ExpandedArithmeticCatalogue.Grammar(c);
        if (c.Version == ReasoningStoryCatalogue.Version) return ReasoningStoryValidator.Grammar(c);
        if (c.Version == FractionQuestionCatalogue.Version) return FractionQuestionCatalogue.Prose(c).Grammar(c, new HashSet<string>(StringComparer.Ordinal));
        if (c.Version == FindXQuestionCatalogue.Version) return FindXQuestionCatalogue.Grammar(c);
        if (c.Version == AppliedQuestionCatalogue.Version) return AppliedQuestionCatalogue.Grammar(c);
        if (c.Version is ArithmeticQuestionCatalogue.Version or AdditionQuestionCatalogue.Version)
            return ReviewedQuestionProse.For(c)!.Grammar(c, new HashSet<string>(StringComparer.Ordinal));
        string a = c.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer
            ? "other" : c.Structure == BasicQuestionStructure.EqualGroups ? "group_one" : "name";
        string b = c.Structure is BasicQuestionStructure.Combine or BasicQuestionStructure.Difference or BasicQuestionStructure.TimesFewer ? "other" : "name";
        string q = c.Structure == BasicQuestionStructure.EqualShare ? "group_one" : "name";
        // Exactly one quantity slot in each given; actor/item words remain freely generated.
        // Keep the grammar ASCII for native Windows interop.
        return JsonGrammar.Replace("{ACTOR_A}", "{" + a + "}")
            .Replace("{PROSE_LETTERS}", QuestionProseLanguage.GrammarLetters(c.Language))
            .Replace("{A_PREFIX}", c.Structure == BasicQuestionStructure.EqualGroups
                ? c.Language == MathSolver.Services.AppLanguage.Vietnamese ? "M\\u1ed7i " : "Each " : "")
            .Replace("{ACTOR_B}", "{" + b + "}").Replace("{ACTOR_Q}", "{" + q + "}");
    }

    internal static string BuildNovelGrammar(BasicQuestionContract c, IReadOnlySet<string> excludedProse)
        => c.OpenProse ? OpenQuestionProse.Grammar(c) : c.Version == ExpandedArithmeticCatalogue.Version ? ExpandedArithmeticCatalogue.Grammar(c, excludedProse)
            : ReasoningStoryCatalogue.UsesReviewedPhrasings(c.Family) ? ReasoningStoryValidator.Grammar(c, excludedProse)
            : ReviewedQuestionProse.For(c)?.Grammar(c, excludedProse) ?? BuildGrammar(c);

}
