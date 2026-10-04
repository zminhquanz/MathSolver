using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using System.Diagnostics;
using System.Text;

namespace MathSolver.Services.QuestionBank;

/// <summary>One loaded model and one native context at a time on Windows/Android.</summary>
public sealed class GgufQuestionRuntime : IQuestionTextRuntime
{
    private const string JsonGrammar = """
        root ::= "{" ws "\"given_a\"" ws ":" ws string-a ws "," ws "\"given_b\"" ws ":" ws string-b ws "," ws "\"question\"" ws ":" ws string-q ws "," ws "\"solution_lead\"" ws ":" ws string ws "," ws "\"unit_id\"" ws ":" ws unit ws "}" ws
        string-a ::= "\"" "{A_PREFIX}{ACTOR_A}" char{0,180} "{a} {unit}" char{0,180} "\""
        string-b ::= "\"" "{ACTOR_B}" char{0,180} "{b}" char{0,180} "\""
        string-q ::= "\"" char{0,180} "{ACTOR_Q}" char{0,180} "\""
        string ::= "\"" char{1,400} "\""
        char ::= [^"\\{}0-9\x00-\x1f] | "\\" ["\\/bfnrt] | placeholder
        placeholder ::= "{name}" | "{other}" | "{unit}" | "{group}" | "{group_one}"
        unit ::= "\"" ("books" | "notebooks" | "pencils" | "candies" | "apples" | "oranges" | "flowers" | "cards" | "balls" | "stickers" | "cakes") "\""
        ws ::= [ \t\n\r]*
        """;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LLamaWeights? _weights;
    private ModelParams? _parameters;
    private static int _nativeConfigured;
    public bool IsLoaded => _weights is not null;
    public string ModelName { get; private set; } = "";
    public string ModelPath { get; private set; } = "";
    public int InferenceThreadCount => IsLoaded ? _parameters?.Threads ?? 0 : 0;
    public int PromptThreadCount => IsLoaded ? _parameters?.BatchThreads ?? 0 : 0;

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
                if (!File.Exists(path) || !path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Choose a GGUF model file.");
                if (Interlocked.Exchange(ref _nativeConfigured, 1) == 0)
                {
                    NativeLibraryConfig.All.WithLogCallback((_, _) => { });
#if ANDROID
                    // Android packages .so files outside the managed working
                    // directory. Resolve the installed native folder explicitly
                    // and load the CPU backend before llama initialises its registry.
                    string nativeDirectory = Android.App.Application.Context.ApplicationInfo!.NativeLibraryDir!;
                    foreach (string dependency in new[] { "libggml-base.so", "libggml-cpu.so", "libggml.so" })
                        System.Runtime.InteropServices.NativeLibrary.Load(Path.Combine(nativeDirectory, dependency));
                    NativeLibraryConfig.LLama.WithLibrary(Path.Combine(nativeDirectory, "libllama.so"));
                    NativeLibraryConfig.Mtmd.WithLibrary(Path.Combine(nativeDirectory, "libmtmd.so"));
                    NativeLibraryConfig.All.WithCuda(false).WithVulkan(false);
#endif
                }
                var parameters = new ModelParams(path)
                {
                    ContextSize = 2048, BatchSize = 256, UBatchSize = 128,
                    GpuLayerCount = 0, UseMemorymap = true,
                    // Leave CPU capacity for navigation, rendering and the C# calculators.
                    Threads = GetInferenceThreadCount(Environment.ProcessorCount),
                    BatchThreads = GetInferenceThreadCount(Environment.ProcessorCount)
                };
                // Eject first: switching models must not double peak resident weights.
                _weights?.Dispose();
                _weights = null;
                ModelName = "";
                ModelPath = "";
                var weights = await LLamaWeights.LoadFromFileAsync(parameters, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) { weights.Dispose(); cancellationToken.ThrowIfCancellationRequested(); }
                _parameters = parameters;
                ModelName = Path.GetFileName(path);
                ModelPath = Path.GetFullPath(path);
                _weights = weights;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task EjectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => { _weights?.Dispose(); _weights = null; _parameters = null; ModelName = ""; ModelPath = ""; }).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
    {
        if (!contract.IsTemplate || !contract.IsValid) throw new ArgumentException("InvalidContract", nameof(contract));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_weights is null || _parameters is null) throw new InvalidOperationException("ModelNotLoaded");
            return await Task.Run(async () =>
            {
                bool gemma4 = _weights.Metadata.TryGetValue("general.architecture", out string? architecture) && architecture == "gemma4";
                var executor = new StatelessExecutor(_weights, _parameters) { ApplyTemplate = !gemma4 };
                // Google's newer Gemma 4 GGUF chat template is full Jinja, which
                // llama_chat_apply_template in this backend cannot interpret.
                // Use the documented E2B/E4B text-only, thinking-off framing.
                // https://ai.google.dev/gemma/docs/capabilities/thinking
                string input = gemma4 ? $"<|turn>user\n{prompt}<turn|>\n<|turn>model\n" : prompt;
                var result = new StringBuilder();
                int generatedTokens = 0;
                long generationStarted = 0;
                try
                {
                    await foreach (string text in executor.InferAsync(input, new InferenceParams
                    {
                        MaxTokens = 700,
                        SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.65f, TopP = 0.9f,
                            GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.None,
                            Grammar = new Grammar(BuildGrammar(contract), "root") },
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
        if (c.Version == AdditionQuestionCatalogue.Version)
        {
            return BuildAdditionGrammar(c);
        }
        string a = c.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer
            ? "other" : c.Structure == BasicQuestionStructure.EqualGroups ? "group_one" : "name";
        string b = c.Structure is BasicQuestionStructure.Combine or BasicQuestionStructure.Difference or BasicQuestionStructure.TimesFewer ? "other" : "name";
        string q = c.Structure == BasicQuestionStructure.EqualShare ? "group_one" : "name";
        // Exactly one quantity slot in each given; actor/item words remain freely generated.
        // Keep the grammar ASCII for native Windows interop.
        return JsonGrammar.Replace("{ACTOR_A}", "{" + a + "}")
            .Replace("{A_PREFIX}", c.Structure == BasicQuestionStructure.EqualGroups
                ? c.Language == MathSolver.Services.AppLanguage.Vietnamese ? "M\\u1ed7i " : "Each " : "")
            .Replace("{ACTOR_B}", "{" + b + "}").Replace("{ACTOR_Q}", "{" + q + "}");
    }

    private static string BuildAdditionGrammar(BasicQuestionContract c)
    {
        var scene = AdditionQuestionCatalogue.Find(c.SceneId)!;
        var scale = scene.Scale(c.Tier)!;
        bool parts = scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts;
        bool comparison = c.Structure is BasicQuestionStructure.AddComparisonMore or BasicQuestionStructure.AddComparisonInverse;
        string actorA = comparison ? "{other}" : "{name}";
        bool vi = c.Language == MathSolver.Services.AppLanguage.Vietnamese;
        string actorB = comparison
            ? c.Structure == BasicQuestionStructure.AddComparisonMore ? "{name}" : "{other}"
            : c.Structure == BasicQuestionStructure.Combine && !parts ? "{other}" : "{name}";
        // Bind the actor before ordinary prose in stock/comparison clauses. The old
        // free prefix let a literal name start a story, then forced the correct slot
        // into a dangling explanation at the end. Each field is now one sentence;
        // required slots are separate from prose, so they cannot be repeated there.
        static string Literal(string value) => System.Text.Json.JsonSerializer.Serialize(value);
        static string Sequence(string[] slots) => string.Join(" prose{0,48} ",
            slots.Select(slot => Literal(slot is "{name}" or "{other}" ? slot + " " : slot))) + " prose{0,48}";
        static string Sentence(string body, string ending) => "\"\\\"\" " + body + " " + Literal(ending) + " \"\\\"\"";
        string QuantityB() => !vi && c.Structure == BasicQuestionStructure.AddComparisonMore ? Literal("{b} more {unit}")
            : !vi && c.Structure == BasicQuestionStructure.AddComparisonInverse ? Literal("{b} fewer {unit}")
            : !vi && c.Structure == BasicQuestionStructure.Increase && scene.Kind != AdditionSceneKind.Arrivals
                ? "(" + Literal("{b} {unit}") + " | " + Literal("{b} more {unit}") + ")" : Literal("{b} {unit}");
        string aBody, bBody;
        if (parts)
        {
            string PartBody(string part, string actor, string quantity, bool continuing = false)
            {
                string opening = scene.Kind == AdditionSceneKind.Parts ? vi ? "Ở " : "In " : vi ? "Vào " : "In ";
                string alternative = scene.Kind == AdditionSceneKind.Parts ? vi ? "Tại " : "At " : vi ? "Trong " : "During ";
                if (continuing)
                {
                    opening = char.ToLowerInvariant(opening[0]) + opening[1..];
                    alternative = char.ToLowerInvariant(alternative[0]) + alternative[1..];
                }
                string link = scene.Kind == AdditionSceneKind.Parts ? vi ? " trong " : " in " : ", ";
                return "(" + Literal(opening) + " | " + Literal(alternative) + ")? " + Literal(part + link + actor + " ")
                    + " prose{1,48} " + Literal(quantity) + " prose{0,32}";
            }
            aBody = PartBody("{part_a}", actorA, "{a} {unit}");
            bBody = PartBody("{part_b}", actorB, "{b} {unit}", continuing: true);
        }
        else if (scene.Kind == AdditionSceneKind.Arrivals)
        {
            aBody = vi ? "(" + Literal("Ở {name} có {a} {unit}") + " | " + Literal("Tại {name} có {a} {unit}") + ") prose{0,32}"
                : Literal("There are {a} {unit} at {name}") + " prose{0,32}";
            string[] viLinks = scene.Id == "club-arrivals" ? ["đến ", "tới ", "ở ", "tại ", "vào ", "tham gia "] : ["đến ", "tới ", "ở ", "tại ", "vào "];
            string links = vi ? "(" + string.Join(" | ", viLinks.Select(Literal)) + ")"
                : "(" + string.Join(" | ", new[] { "into ", "at ", "to join " }.Select(Literal)) + ")";
            string start = vi ? "(" + Literal("có ") + " | " + Literal("sau đó, có thêm ") + " | " + Literal("tiếp đó, có thêm ") + ")"
                : "(" + Literal("another ") + " | " + Literal("then, another ") + ")";
            bBody = start + " " + Literal("{b} {unit}") + " prose{1,32} " + links + " " + Literal(actorB) + " prose{0,24}";
        }
        else
        {
            aBody = Sequence([actorA, "{a} {unit}"]);
            string prefix = c.Structure == BasicQuestionStructure.RecoverInitial
                ? "(" + Literal(vi ? "trước đó, " : "previously, ") + " | " + Literal(vi ? "lúc trước, " : "earlier, ") + ")? "
                : c.Structure == BasicQuestionStructure.Increase
                    ? "(" + Literal(vi ? "sau đó, " : "then, ") + " | " + Literal(vi ? "tiếp đó, " : "later, ") + ")? " : "";
            string rest = comparison ? vi
                ? "(" + Literal(c.Structure == BasicQuestionStructure.AddComparisonMore ? "{other}" : "{name}") + " prose{0,48} " + QuantityB()
                    + " | " + QuantityB() + " prose{0,48} " + Literal(c.Structure == BasicQuestionStructure.AddComparisonMore ? "{other}" : "{name}") + ")"
                : QuantityB() + " prose{0,48} " + Literal(c.Structure == BasicQuestionStructure.AddComparisonMore ? "{other}" : "{name}")
                : QuantityB();
            string bStart = comparison && scene.Kind == AdditionSceneKind.Contributions
                ? actorB + " " + (vi ? scene.VietnameseAction : scene.EnglishAction) : actorB;
            bBody = prefix + Literal(bStart + " ") + " prose{0,48} " + rest + " prose{0,48}";
            if (c.Structure == BasicQuestionStructure.RecoverInitial)
            {
                // The second amount was removed, not just owned or 'taken'
                // ambiguously. Let the model choose a supported removal phrase.
                string[] removals = vi
                    ? ["đã cho đi ", "cho đi ", "đã bán ", "bán ", "đã lấy ra ", "lấy ra ", "đã dùng ", "dùng ", "đã tặng ", "tặng "]
                    : ["gave away ", "removed ", "sold ", "used ", "donated ", "lost ", "had given away ", "had removed ", "had sold "];
                bBody = prefix + Literal(actorB + " ") + " (" + string.Join(" | ", removals.Select(Literal))
                    + ") " + QuantityB() + " prose{0,48}";
            }
        }
        bool both = c.Structure == BasicQuestionStructure.Combine && !parts;
        string actors = both ? "(" + Literal("{name}") + " " + Literal(vi ? " và " : " and ") + " " + Literal("{other}")
            + " | " + Literal("{other}") + " " + Literal(vi ? " và " : " and ") + " " + Literal("{name}") + ")" : Literal("{name}");
        string questionPrefix = c.Structure == BasicQuestionStructure.RecoverInitial ? vi ? "ban đầu " : ""
            : scene.Kind == AdditionSceneKind.Parts ? vi ? scale.VietnameseSpan + " này trong " : ""
            : scene.Kind == AdditionSceneKind.Arrivals ? vi ? "ở " : "" : "";
        // Put the target in a normal question order, rather than allowing the model
        // to mention it only after an unrelated question or a lengthy explanation.
        string q = vi ? Literal("Hỏi " + questionPrefix) + " " + actors + " " + Literal(" ") + " prose{1,64} " + Literal("{unit}")
            : Literal("How many {unit}") + " prose{1,48} " + actors + " " + Literal(" ") + " prose{0,48}";
        string leadPrefix = c.Structure == BasicQuestionStructure.RecoverInitial ? vi ? "Số " : "The original number of "
            : comparison ? vi ? "Số " : "The number of " : vi ? "Tổng số " : "The total number of ";
        string lead = Literal(leadPrefix + "{unit}") + " (prose{1,48} | prose{1,32} " + actors + " prose{1,48})";
        // These templates support Vietnamese and English. Allow their Latin letters
        // and clause punctuation, rather than only excluding ASCII digits: otherwise
        // the sampler can insert Arabic/full-width digits despite the numeric ban.
        return """
            root ::= "{" ws "\"given_a\"" ws ":" ws string-a ws "," ws "\"given_b\"" ws ":" ws string-b ws "," ws "\"question\"" ws ":" ws string-q ws "," ws "\"solution_lead\"" ws ":" ws string ws "," ws "\"unit_id\"" ws ":" ws unit ws "}" ws
            string-a ::= {STRING_A}
            string-b ::= {STRING_B}
            string-q ::= {STRING_Q}
            string ::= {STRING_LEAD}
            prose ::= [A-Za-z\u00c0-\u00d6\u00d8-\u00f6\u00f8-\u024f\u1e00-\u1eff ,'\u2019-]
            unit ::= "\"" ({UNIT_IDS}) "\""
            ws ::= [ \t\n\r]*
            """.Replace("{STRING_A}", Sentence(aBody, ","))
            .Replace("{STRING_B}", Sentence(bBody, "."))
            .Replace("{STRING_Q}", Sentence(q, "?"))
            .Replace("{STRING_LEAD}", Sentence(lead, ":"))
            .Replace("{UNIT_IDS}", string.Join(" | ", scale.UnitIds.Select(id => "\"" + id + "\"")));
    }

}
