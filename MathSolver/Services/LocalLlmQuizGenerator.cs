using LLama;
using LLama.Common;
using LLama.Sampling;
using MathSolver.Models;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>
/// Dùng LLM cục bộ để diễn đạt một biểu thức do engine sở hữu thành toán đố.
/// Model chỉ viết ngôn ngữ tự nhiên; mọi số, phép tính, đáp án và lựa chọn đều
/// được ArithmeticQuizGenerator/BasicArithmeticEngine quyết định trước.
/// </summary>
public sealed class LocalLlmQuizGenerator
{
    /// <summary>
    /// Giữ 4 luồng trên CPU tối đa 8 logical threads; CPU lớn hơn dùng tối đa
    /// 8 luồng để tăng tốc sinh đề mà vẫn chừa tài nguyên cho giao diện, hệ
    /// điều hành và các tác vụ nền. Máy dưới 4 threads không bị oversubscribe.
    /// </summary>
    public static int CpuThreadCount { get; } =
        Environment.ProcessorCount > 8
            ? 8
            : Math.Min(4, Environment.ProcessorCount);
    public const int MaximumAttempts = 3;
    public const int MaximumOutputTokens = 240;
    public const int ModelUnloadGracePeriodSeconds = 60;
    public const uint ContextSize = 1536;

    private readonly ArithmeticQuizGenerator _quizGenerator;
    private readonly BasicArithmeticEngine _engine;
    private readonly LlmWordProblemValidator _wordProblemValidator = new();
    private readonly SemaphoreSlim _generationGate = new(1, 1);
    private readonly object _modelLifetimeSync = new();

    private LLamaWeights? _loadedWeights;
    private ModelParams? _loadedModelParameters;
    private string? _loadedModelPath;
    private CancellationTokenSource? _scheduledUnloadCancellation;

    public LocalLlmQuizGenerator(
        ArithmeticQuizGenerator quizGenerator,
        BasicArithmeticEngine engine)
    {
        _quizGenerator =
            quizGenerator ??
            throw new ArgumentNullException(nameof(quizGenerator));

        _engine =
            engine ??
            throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Bỏ ngay weights đang cache khi người dùng reject hoặc đổi model.
    /// File GGUF trên ổ đĩa không bị xóa.
    /// </summary>
    public async Task UnloadModelAsync(
        CancellationToken cancellationToken = default)
    {
        CancelScheduledModelUnload();
        await _generationGate.WaitAsync(cancellationToken);

        try
        {
            await Task.Run(
                DisposeLoadedModel,
                cancellationToken);
        }
        finally
        {
            _generationGate.Release();
        }
    }

    /// <summary>
    /// Giữ model trong RAM khi người dùng quay lại trang Toán đố trong thời
    /// gian chờ. Có thể gọi nhiều lần an toàn.
    /// </summary>
    public void CancelScheduledModelUnload()
    {
        CancellationTokenSource? cancellation;

        lock (_modelLifetimeSync)
        {
            cancellation = _scheduledUnloadCancellation;
            _scheduledUnloadCancellation = null;
        }

        CancelWithoutThrow(cancellation);
    }

    /// <summary>
    /// Bắt đầu grace period 60 giây. Hết thời gian mà trang không xuất hiện
    /// lại thì weights mới được giải phóng; context/KV không được giữ ở đây.
    /// </summary>
    public void ScheduleModelUnload(
        Func<Task>? onModelUnloadedAsync = null)
    {
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previousCancellation;

        lock (_modelLifetimeSync)
        {
            previousCancellation = _scheduledUnloadCancellation;
            _scheduledUnloadCancellation = cancellation;
        }

        CancelWithoutThrow(previousCancellation);

        _ = UnloadModelAfterGracePeriodAsync(
            cancellation,
            onModelUnloadedAsync);
    }

    public async Task<LlmQuizGenerationResult> GenerateAsync(
        string modelPath,
        ArithmeticQuizMode mode,
        ArithmeticOperation? requestedOperation,
        AppLanguage language,
        IProgress<LlmQuizProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidModelPath(modelPath))
        {
            return new(null, 0, "ModelFileNotFound", false);
        }

        CancelScheduledModelUnload();
        await _generationGate.WaitAsync(cancellationToken);

        bool modelWasLoaded = false;
        int completedAttempts = 0;

        try
        {
            ArithmeticQuizQuestion contract =
                CreateNaturalLanguageContract(
                    mode,
                    requestedOperation);

            (LLamaWeights weights, ModelParams modelParameters) =
                await EnsureModelLoadedAsync(
                    modelPath,
                    progress,
                    cancellationToken);

            // ModelWasLoaded nghĩa là weights đã sẵn sàng trong RAM, bất kể
            // vừa nạp từ GGUF hay được tái sử dụng từ câu trước.
            modelWasLoaded = true;

            // Gemma 4 stores a Jinja template that currently cannot be
            // evaluated by LLamaSharp's chat-template bridge. The model
            // weights themselves are supported by the 0.27 backend, so use
            // Gemma 4's documented control-token format directly.
            // QuizLlmModelStore đã xác nhận metadata kiến trúc Gemma 4 trước
            // khi đến đây, nên luôn dùng control-token template tương ứng và
            // không phụ thuộc vào cách người dùng đặt tên file.
            const bool useManualGemma4Template = true;

            string systemPrompt =
                LlmQuizPromptBuilder.BuildSystemPrompt(
                    language);

            // Chọn gợi ý tên đúng một lần cho cả lượt sinh và mọi lần retry.
            // Không đưa toàn bộ catalog tên/xưng hô vào prompt vì phần đó chỉ
            // làm tăng token prefill mà không giúp model viết đề chính xác hơn.
            WordProblemStudent? studentHint =
                LlmQuizPromptBuilder.SelectStudentHint(
                    language);

            // Executor chỉ tồn tại trong một lần sinh câu. StatelessExecutor
            // tạo context/KV cho InferAsync và giải phóng chúng khi lượt suy
            // luận kết thúc; chỉ LLamaWeights được cache giữa các câu.
            var executor =
                new StatelessExecutor(
                    weights,
                    modelParameters)
                {
                    ApplyTemplate = !useManualGemma4Template,
                    SystemMessage = useManualGemma4Template
                        ? string.Empty
                        : systemPrompt
                };

            string? previousErrorCode = null;

            for (int attempt = 1;
                 attempt <= MaximumAttempts;
                 attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                completedAttempts = attempt;

                progress?.Report(
                    new(
                        LlmQuizProgressStage.Generating,
                        attempt,
                        MaximumAttempts));

                string userPrompt =
                    LlmQuizPromptBuilder.BuildUserPrompt(
                        contract.Expression,
                        language,
                        studentHint,
                        previousErrorCode);

                string prompt = useManualGemma4Template
                    ? LlmQuizPromptBuilder.BuildGemma4Prompt(
                        systemPrompt,
                        userPrompt)
                    : userPrompt;

                var inferenceParameters =
                    new InferenceParams
                    {
                        MaxTokens = MaximumOutputTokens,
                        SamplingPipeline =
                            new DefaultSamplingPipeline
                            {
                                Temperature = 0.35f,
                                TopP = 0.9f
                            }
                    };

                var output = new StringBuilder();
                string? lastPreview = null;
                var previewTimer =
                    System.Diagnostics.Stopwatch.StartNew();

                await foreach (string token in
                    executor.InferAsync(
                        prompt,
                        inferenceParameters,
                        cancellationToken))
                {
                    output.Append(token);

                    if (previewTimer.ElapsedMilliseconds >= 80)
                    {
                        previewTimer.Restart();

                        if (LlmWordProblemParser.TryExtractProblemTextPreview(
                                output.ToString(),
                                out string preview) &&
                            !string.Equals(
                                preview,
                                lastPreview,
                                StringComparison.Ordinal))
                        {
                            lastPreview = preview;

                            progress?.Report(
                                new(
                                    LlmQuizProgressStage.Generating,
                                    attempt,
                                    MaximumAttempts,
                                    preview));
                        }
                    }
                }

                string rawOutput = output.ToString();

                if (LlmWordProblemParser.TryExtractProblemTextPreview(
                        rawOutput,
                        out string completedPreview))
                {
                    progress?.Report(
                        new(
                            LlmQuizProgressStage.Generating,
                            attempt,
                            MaximumAttempts,
                            completedPreview));
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Local LLM attempt {attempt} raw output: {rawOutput}");

                progress?.Report(
                    new(
                        LlmQuizProgressStage.Validating,
                        attempt,
                        MaximumAttempts));

                if (!LlmWordProblemParser.TryParse(
                        rawOutput,
                        out LlmWordProblemDraft? draft,
                        out string parseErrorCode))
                {
                    previousErrorCode = parseErrorCode;

                    System.Diagnostics.Debug.WriteLine(
                        $"Local LLM attempt {attempt} rejected by parser: {parseErrorCode}");
                }
                else
                {
                    LlmWordProblemValidationResult validation =
                        _wordProblemValidator.Validate(
                            draft,
                            contract.Expression,
                            contract.CorrectAnswer,
                            language);

                    if (validation.IsValid &&
                        validation.WordProblem is not null)
                    {
                        return new(
                            contract with
                            {
                                WordProblem = validation.WordProblem
                            },
                            attempt,
                            null,
                            true);
                    }

                    previousErrorCode =
                        validation.ErrorCode ??
                        "InvalidWordProblem";

                    System.Diagnostics.Debug.WriteLine(
                        $"Local LLM attempt {attempt} rejected by validator: {previousErrorCode}");
                }

                if (attempt < MaximumAttempts)
                {
                    progress?.Report(
                        new(
                            LlmQuizProgressStage.Retrying,
                            attempt,
                            MaximumAttempts));
                }
            }

            return new(
                null,
                completedAttempts,
                previousErrorCode ?? "InvalidWordProblem",
                modelWasLoaded);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Local LLM generation failed: {exception}");

            return new(
                null,
                completedAttempts,
                MapRuntimeError(exception),
                modelWasLoaded);
        }
        finally
        {
            _generationGate.Release();
        }
    }

    private async Task<(LLamaWeights Weights, ModelParams Parameters)>
        EnsureModelLoadedAsync(
            string modelPath,
            IProgress<LlmQuizProgress>? progress,
            CancellationToken cancellationToken)
    {
        string normalizedPath =
            Path.GetFullPath(modelPath);

        LLamaWeights? cachedWeights = _loadedWeights;
        ModelParams? cachedParameters = _loadedModelParameters;

        if (cachedWeights is not null &&
            cachedParameters is not null &&
            string.Equals(
                _loadedModelPath,
                normalizedPath,
                ModelPathComparison))
        {
            progress?.Report(
                new(
                    LlmQuizProgressStage.ModelLoaded,
                    0,
                    MaximumAttempts));

            System.Diagnostics.Debug.WriteLine(
                "Local LLM reused cached GGUF weights.");

            return (cachedWeights, cachedParameters);
        }

        DisposeLoadedModel();

        var modelParameters =
            CreateModelParameters(normalizedPath);

        progress?.Report(
            new(
                LlmQuizProgressStage.LoadingModel,
                0,
                MaximumAttempts));

        LLamaWeights? loadedWeights = null;

        try
        {
            loadedWeights =
                await LLamaWeights.LoadFromFileAsync(
                    modelParameters);

            cancellationToken.ThrowIfCancellationRequested();

            LLamaWeights weightsToCache = loadedWeights;

            _loadedWeights = weightsToCache;
            _loadedModelParameters = modelParameters;
            _loadedModelPath = normalizedPath;
            loadedWeights = null;

            System.Diagnostics.Debug.WriteLine(
                "Local LLM loaded GGUF weights into the shared model cache.");

            progress?.Report(
                new(
                    LlmQuizProgressStage.ModelLoaded,
                    0,
                    MaximumAttempts));

            return (weightsToCache, modelParameters);
        }
        finally
        {
            // Nếu quá trình nạp bị hủy hoặc phát sinh lỗi trước khi cache nhận
            // quyền sở hữu thì giải phóng ngay phần weights vừa tạo.
            loadedWeights?.Dispose();
        }
    }

    private static ModelParams CreateModelParameters(
        string modelPath) =>
        new(modelPath)
        {
            ContextSize = ContextSize,
            GpuLayerCount = 0,
            Threads = CpuThreadCount,
            BatchThreads = CpuThreadCount,
            BatchSize = 256,
            UBatchSize = 128,
            UseMemorymap = true,
            UseMemoryLock = false
        };

    private async Task UnloadModelAfterGracePeriodAsync(
        CancellationTokenSource cancellation,
        Func<Task>? onModelUnloadedAsync)
    {
        CancellationToken cancellationToken =
            cancellation.Token;

        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(
                    ModelUnloadGracePeriodSeconds),
                cancellationToken);

            await _generationGate.WaitAsync(
                cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_modelLifetimeSync)
                {
                    if (!ReferenceEquals(
                            _scheduledUnloadCancellation,
                            cancellation))
                    {
                        return;
                    }

                    _scheduledUnloadCancellation = null;
                }

                DisposeLoadedModel();

                if (onModelUnloadedAsync is not null)
                {
                    await onModelUnloadedAsync();
                }

                System.Diagnostics.Debug.WriteLine(
                    "Local LLM weights were released after the 60-second grace period.");
            }
            finally
            {
                _generationGate.Release();
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Người dùng đã quay lại trang trước khi hết grace period.
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Local LLM delayed unload failed: {exception}");
        }
        finally
        {
            lock (_modelLifetimeSync)
            {
                if (ReferenceEquals(
                        _scheduledUnloadCancellation,
                        cancellation))
                {
                    _scheduledUnloadCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void DisposeLoadedModel()
    {
        LLamaWeights? weights = _loadedWeights;

        _loadedWeights = null;
        _loadedModelParameters = null;
        _loadedModelPath = null;

        weights?.Dispose();
    }

    private static StringComparison ModelPathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static void CancelWithoutThrow(
        CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Tác vụ unload đã tự hoàn tất và giải phóng CTS đúng lúc lời gọi
            // hủy được gửi tới; model khi đó đã ở trạng thái nhất quán.
        }
    }

    private static bool IsValidModelPath(
        string? modelPath) =>
        QuizLlmModelStore.IsSupportedModelPath(
            modelPath);

    private ArithmeticQuizQuestion CreateNaturalLanguageContract(
        ArithmeticQuizMode mode,
        ArithmeticOperation? requestedOperation)
    {
        // Tránh các đề kiểu “có 0 đồ vật” vốn đúng toán học nhưng không tự nhiên.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            ArithmeticQuizQuestion question =
                _quizGenerator.Generate(
                    mode,
                    requestedOperation);

            if (question.Expression.LeftOperand > 0 &&
                question.Expression.RightOperand > 0)
            {
                IntegerArithmeticResult calculation =
                    _engine.CalculateInteger(
                        question.Expression);

                if (calculation.Result >= 0)
                {
                    return question;
                }
            }
        }

        throw new InvalidOperationException(
            "Could not create a natural-language arithmetic contract.");
    }

    private static string MapRuntimeError(
        Exception exception)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return "ModelFileNotFound";
        }

        if (exception is OutOfMemoryException)
        {
            return "NotEnoughMemory";
        }

        return "ModelRuntimeError";
    }
}

internal static class LlmQuizPromptBuilder
{
    private const int WordProblemPeoplePercent = 50;

    public static WordProblemStudent? SelectStudentHint(
        AppLanguage language)
    {
        WordProblemPeopleProfile people =
            WordProblemPeopleCatalog.GetProfile(
                language);

        if (people.Students.Count == 0 ||
            Random.Shared.Next(100) >= WordProblemPeoplePercent)
        {
            return null;
        }

        return people.Students[
            Random.Shared.Next(people.Students.Count)];
    }

    public static string BuildSystemPrompt(
        AppLanguage language)
    {
        return language == AppLanguage.Vietnamese
            ? "Bạn là giáo viên tiểu học Việt Nam thân thiện. Bạn chỉ viết bài toán đố một bước dùng cộng, trừ, nhân hoặc chia theo dữ kiện bắt buộc. Không tự đổi số, phép tính hay đáp án. Chỉ trả về đúng một JSON hợp lệ, không Markdown, không lời chào và không giải thích."
            : "You are a friendly elementary-school teacher writing for an English-language primary curriculum. Write only one-step addition, subtraction, multiplication, or division word problems from the required facts. Never change the numbers, operation, or answer. Return exactly one valid JSON object with no Markdown, greeting, or commentary.";
    }

    public static string BuildGemma4Prompt(
        string systemPrompt,
        string userPrompt)
    {
        // E2B/E4B with thinking disabled use a user turn followed by a model
        // turn. Put the role instruction inside that user turn; a separate
        // system turn is the thinking-enabled shape for these small models.
        return string.Concat(
            "<|turn>user\n",
            systemPrompt,
            "\n\n",
            userPrompt,
            "<turn|>\n",
            "<|turn>model");
    }

    public static string BuildUserPrompt(
        IntegerArithmeticExpression expression,
        AppLanguage language,
        WordProblemStudent? studentHint,
        string? previousErrorCode)
    {
        string operation =
            expression.Operation switch
            {
                ArithmeticOperation.Add => "add",
                ArithmeticOperation.Subtract => "subtract",
                ArithmeticOperation.Multiply => "multiply",
                ArithmeticOperation.Divide => "divide",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(expression))
            };

        string languageName =
            language == AppLanguage.Vietnamese
                ? "Vietnamese used in Vietnamese primary schools"
                : "natural English used in an English-language elementary school";

        string retry =
            string.IsNullOrWhiteSpace(previousErrorCode)
                ? string.Empty
                : BuildRetryInstruction(
                    previousErrorCode,
                    language);

        string characterRule =
            BuildCharacterRule(
                language,
                studentHint);

        string classroomRule =
            language == AppLanguage.Vietnamese
                ? "Nếu dùng tên lớp, khối chỉ được từ lớp 1 đến lớp 5. Lớp con dạng số chỉ từ 1 đến 9 và phải viết lớp 3/1 ... lớp 3/9; dạng chữ đi theo alphabet từ lớp 3A ... lớp 3I. Không viết lớp 30, lớp 3/0, lớp 3/10 hoặc lớp 3J; không viết gọn lớp 31 mà phải viết lớp 3/1."
                : "If a class label is used, the grade must be Class 1 through Class 5. Numeric sections are only 1 through 9 and must be written Class 3/1 ... Class 3/9; alphabetic sections follow A through I, as in Class 3A ... Class 3I. Never write Class 30, Class 3/0, Class 3/10, or Class 3J; write Class 3/1 instead of compact Class 31.";

        return FormattableString.Invariant(
            $$"""
            Write one natural, age-appropriate word problem in {{languageName}}.
            Required left number: {{expression.LeftOperand}}
            Required right number: {{expression.RightOperand}}
            Required operation: {{operation}}

            Rules:
            - problem_text must contain the two required input numbers as digits and no other arithmetic quantities. One valid class label described below is allowed as metadata.
            - Do not calculate or reveal the answer inside problem_text.
            - Use a realistic elementary-school situation and make the operation unambiguous.
            - {{characterRule}}
            - {{classroomRule}}
            - For subtraction, the left quantity must decrease by the right quantity.
            - For division, divide the left total exactly into the right number of equal groups.
            - solution_lead is one short textbook answer sentence naming the quantity asked in problem_text. For example: "Số cái bút các bạn có tất cả là:" or "The total number of pencils the students have is:". Never write only "Ta thực hiện phép nhân" or "We perform the multiplication".
            - answer_unit is a short noun phrase without a number.
            - subject_name is the person, group, or object described in problem_text; it may be a generic reference and does not require a personal name.
            {{retry}}

            JSON schema:
            {"problem_text":"... ?","subject_name":"...","answer_unit":"...","solution_lead":"...:"}
            """);
    }

    private static string BuildCharacterRule(
        AppLanguage language,
        WordProblemStudent? studentHint)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return studentHint is null
                ? "Tên riêng không bắt buộc; hãy dùng cách gọi chung tự nhiên như một bạn học sinh, các bạn trong lớp hoặc người thân."
                : $"Nếu dùng tên riêng, chỉ dùng gợi ý \"{studentHint.NaturalReference}\"; cũng có thể không dùng tên và viết bằng cách gọi chung tự nhiên.";
        }

        return studentHint is null
            ? "A personal name is optional; use a natural generic reference such as a student, the classmates, or a family member."
            : $"If using a personal name, use only the hint \"{studentHint.NaturalReference}\"; a natural generic reference without a name is also valid.";
    }

    private static string BuildRetryInstruction(
        string errorCode,
        AppLanguage language) =>
        errorCode switch
        {
            "InvalidJson" or "EmptyModelOutput" =>
                "\nThe previous response was not one complete JSON object. Return only the JSON object from the schema.",

            "ProblemNumbersMismatch" or "AnswerRevealedInProblem" =>
                "\nThe previous story used wrong or extra numbers. Use each required input number and no other number; do not state the answer.",

            "OperationMeaningUnclear" or "OperationMeaningConflict" =>
                "\nThe previous story did not clearly express the required operation. Rewrite it with an unmistakable elementary-school action for that operation.",

            "InvalidClassLabel" =>
                language == AppLanguage.Vietnamese
                    ? "\nTên lớp trước không hợp lệ. Chỉ dùng khối 1–5, lớp con 1–9 hoặc A–I; ví dụ lớp 3/1 hoặc lớp 3A."
                    : "\nThe previous class label was invalid. Use only grades 1–5 with sections 1–9 or A–I, such as Class 3/1 or Class 3A.",

            _ =>
                "\nThe previous response failed validation. Return a shorter corrected story using the exact schema and required facts."
        };
}

internal sealed class LlmWordProblemDraft
{
    [JsonPropertyName("problem_text")]
    public string? ProblemText { get; init; }

    [JsonPropertyName("subject_name")]
    public string? SubjectName { get; init; }

    [JsonPropertyName("answer_unit")]
    public string? AnswerUnit { get; init; }

    [JsonPropertyName("solution_lead")]
    public string? SolutionLead { get; init; }
}

internal static class LlmWordProblemParser
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow
        };

    public static bool TryParse(
        string rawOutput,
        out LlmWordProblemDraft? draft,
        out string errorCode)
    {
        draft = null;
        errorCode = "InvalidJson";

        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            errorCode = "EmptyModelOutput";
            return false;
        }

        List<string> jsonObjects =
            ExtractCompleteJsonObjects(rawOutput);

        for (int index = jsonObjects.Count - 1;
             index >= 0;
             index--)
        {
            try
            {
                LlmWordProblemDraft? candidate =
                    JsonSerializer.Deserialize<LlmWordProblemDraft>(
                        jsonObjects[index],
                        JsonOptions);

                if (!string.IsNullOrWhiteSpace(
                        candidate?.ProblemText))
                {
                    draft = candidate;
                    errorCode = string.Empty;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Thử object hoàn chỉnh khác hoặc fallback văn bản bên dưới.
            }
        }

        // Nếu model hết token sau khi đã viết xong problem_text nhưng chưa
        // đóng toàn bộ JSON, vẫn có thể dùng đề bài và để engine dựng metadata
        // lời giải an toàn. Đây cũng hỗ trợ model trả thẳng một câu hỏi.
        if (TryExtractProblemTextPreview(
                rawOutput,
                out string problemText) &&
            problemText.Length >= 12)
        {
            draft =
                new LlmWordProblemDraft
                {
                    ProblemText = problemText
                };

            errorCode = string.Empty;
            return true;
        }

        return false;
    }

    public static bool TryExtractProblemTextPreview(
        string rawOutput,
        out string preview)
    {
        preview = string.Empty;

        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return false;
        }

        const string propertyName = "\"problem_text\"";
        int propertyIndex =
            rawOutput.LastIndexOf(
                propertyName,
                StringComparison.OrdinalIgnoreCase);

        if (propertyIndex >= 0)
        {
            int colonIndex =
                rawOutput.IndexOf(
                    ':',
                    propertyIndex + propertyName.Length);

            int quoteIndex = colonIndex >= 0
                ? rawOutput.IndexOf('"', colonIndex + 1)
                : -1;

            if (quoteIndex >= 0)
            {
                preview =
                    DecodePartialJsonString(
                        rawOutput,
                        quoteIndex + 1);

                preview =
                    NormalizePreview(preview);

                return preview.Length > 0;
            }
        }

        string plain =
            StripModelControlText(rawOutput);

        if (plain.StartsWith('{') ||
            plain.StartsWith("```", StringComparison.Ordinal))
        {
            return false;
        }

        int questionMark = plain.IndexOf('?');

        if (questionMark >= 0)
        {
            plain = plain[..(questionMark + 1)];
        }

        preview = NormalizePreview(plain);
        return preview.Length >= 4;
    }

    private static List<string> ExtractCompleteJsonObjects(
        string rawOutput)
    {
        var objects = new List<string>();
        int depth = 0;
        int startIndex = -1;
        bool insideString = false;
        bool escaping = false;

        for (int index = 0;
             index < rawOutput.Length;
             index++)
        {
            char current = rawOutput[index];

            if (insideString)
            {
                if (escaping)
                {
                    escaping = false;
                }
                else if (current == '\\')
                {
                    escaping = true;
                }
                else if (current == '"')
                {
                    insideString = false;
                }

                continue;
            }

            if (current == '"')
            {
                insideString = true;
                continue;
            }

            if (current == '{')
            {
                if (depth == 0)
                {
                    startIndex = index;
                }

                depth++;
            }
            else if (current == '}' && depth > 0)
            {
                depth--;

                if (depth == 0 && startIndex >= 0)
                {
                    objects.Add(
                        rawOutput[
                            startIndex..(index + 1)]);

                    startIndex = -1;
                }
            }
        }

        return objects;
    }

    private static string DecodePartialJsonString(
        string rawOutput,
        int startIndex)
    {
        var builder = new StringBuilder();

        for (int index = startIndex;
             index < rawOutput.Length;
             index++)
        {
            char current = rawOutput[index];

            if (current == '"')
            {
                break;
            }

            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (++index >= rawOutput.Length)
            {
                break;
            }

            char escaped = rawOutput[index];

            switch (escaped)
            {
                case '"':
                case '\\':
                case '/':
                    builder.Append(escaped);
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 'n':
                    builder.Append(' ');
                    break;
                case 'r':
                    builder.Append(' ');
                    break;
                case 't':
                    builder.Append(' ');
                    break;
                case 'u' when index + 4 < rawOutput.Length:
                    string hex =
                        rawOutput.Substring(
                            index + 1,
                            4);

                    if (ushort.TryParse(
                            hex,
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out ushort codePoint))
                    {
                        builder.Append((char)codePoint);
                        index += 4;
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private static string StripModelControlText(
        string value)
    {
        string result = value
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Replace("<turn|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|turn>model", string.Empty, StringComparison.Ordinal)
            .Trim();

        int channelEnd =
            result.LastIndexOf(
                "<channel|>",
                StringComparison.Ordinal);

        if (channelEnd >= 0)
        {
            result =
                result[(channelEnd + "<channel|>".Length)..]
                    .Trim();
        }

        return result;
    }

    private static string NormalizePreview(
        string value)
    {
        string normalized =
            Regex.Replace(
                    value,
                    @"\s+",
                    " ",
                    RegexOptions.CultureInvariant)
                .Trim();

        return normalized.Length <= 600
            ? normalized
            : normalized[..600];
    }
}

internal sealed record LlmWordProblemValidationResult(
    bool IsValid,
    string? ErrorCode,
    MathWordProblem? WordProblem)
{
    public static LlmWordProblemValidationResult Invalid(
        string errorCode) =>
        new(false, errorCode, null);
}

internal sealed partial class LlmWordProblemValidator
{
    public LlmWordProblemValidationResult Validate(
        LlmWordProblemDraft draft,
        IntegerArithmeticExpression expression,
        BigInteger correctAnswer,
        AppLanguage language)
    {
        if (expression.LeftOperand < int.MinValue ||
            expression.LeftOperand > int.MaxValue ||
            expression.RightOperand < int.MinValue ||
            expression.RightOperand > int.MaxValue ||
            correctAnswer < int.MinValue ||
            correctAnswer > int.MaxValue)
        {
            return LlmWordProblemValidationResult.Invalid(
                "ContractOutOfRange");
        }

        int left = (int)expression.LeftOperand;
        int right = (int)expression.RightOperand;
        int answer = (int)correctAnswer;

        string problem =
            NormalizeSingleLine(draft.ProblemText);
        string subject =
            NormalizeSingleLine(draft.SubjectName);
        string unit =
            NormalizeSingleLine(draft.AnswerUnit);
        string solutionLead =
            NormalizeSingleLine(draft.SolutionLead);

        if (problem.Length is < 12 or > 600 ||
            subject.Length > 80 ||
            unit.Length > 80 ||
            solutionLead.Length > 220)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidTextLength");
        }

        problem = NormalizeClassLabels(
            problem,
            out bool hasInvalidClassLabel);

        if (hasInvalidClassLabel)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidClassLabel");
        }

        // Dấu hỏi và cách mở câu là lỗi trình bày, không phải lỗi dữ kiện.
        // Model nhỏ thường viết một câu mệnh lệnh hoặc quên dấu hỏi dù nội
        // dung bài toán vẫn dùng được. Chuẩn hóa tại đây để tránh tốn thêm
        // một lượt inference chỉ vì hình thức câu hỏi.
        if (!IsQuestionSentence(problem, language))
        {
            problem = AppendDefaultQuestion(problem, language);
        }
        else if (!problem.Contains('?'))
        {
            problem = problem.TrimEnd('.', '!', ';', ':') + "?";
        }

        string problemWithoutClassLabels =
            ClassLabelRegex().Replace(
                problem,
                " ");

        int[] numbers =
            NumberRegex()
                .Matches(problemWithoutClassLabels)
                .Select(match =>
                    int.TryParse(
                        match.Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value)
                            ? value
                            : int.MinValue)
                .ToArray();

        if (numbers.Length < 2 ||
            numbers.Any(number =>
                number != left && number != right) ||
            !numbers.Contains(left) ||
            !numbers.Contains(right) ||
            (left == right && numbers.Count(number => number == left) < 2))
        {
            return LlmWordProblemValidationResult.Invalid(
                "ProblemNumbersMismatch");
        }

        if (answer != left &&
            answer != right &&
            numbers.Contains(answer))
        {
            return LlmWordProblemValidationResult.Invalid(
                "AnswerRevealedInProblem");
        }

        string lowerProblem =
            problem.ToLowerInvariant();

        // Từ khóa chỉ là heuristic ngôn ngữ, không phải bằng chứng toán học.
        // Không loại một đề đã có đúng hai dữ kiện chỉ vì model dùng cách
        // diễn đạt nằm ngoài danh sách từ khóa. Engine C# vẫn sở hữu phép
        // tính và đáp án; cảnh báo này chỉ phục vụ chẩn đoán khi cần.
        if (!HasUnambiguousOperationMeaning(
                lowerProblem,
                expression.Operation,
                language))
        {
            if (HasClearlyConflictingOperationMeaning(
                    lowerProblem,
                    expression.Operation,
                    language))
            {
                return LlmWordProblemValidationResult.Invalid(
                    "OperationMeaningConflict");
            }

            System.Diagnostics.Debug.WriteLine(
                "Local LLM semantic keyword check was inconclusive; " +
                "the problem was accepted after numeric validation.");
        }

        if (string.IsNullOrWhiteSpace(subject) ||
            !lowerProblem.Contains(
                subject.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            subject =
                language == AppLanguage.Vietnamese
                    ? "Bài toán"
                    : "The problem";
        }

        if (string.IsNullOrWhiteSpace(unit) ||
            NumberRegex().IsMatch(unit) ||
            unit.Contains('='))
        {
            unit =
                language == AppLanguage.Vietnamese
                    ? "đơn vị"
                    : "items";
        }

        solutionLead =
            ElementaryWordProblemSolutionFormatter
                .NormalizeSolutionLeadPunctuation(
                    solutionLead);

        if (ShouldRebuildSolutionLead(
                solutionLead,
                language))
        {
            solutionLead =
                ElementaryWordProblemSolutionFormatter
                    .NormalizeSolutionLeadPunctuation(
                        BuildNaturalSolutionLead(
                            problem,
                            expression.Operation,
                            unit,
                            language));
        }

        return new(
            true,
            null,
            new MathWordProblem(
                problem,
                solutionLead,
                unit,
                subject));
    }

    private static bool HasUnambiguousOperationMeaning(
        string problem,
        ArithmeticOperation operation,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return operation switch
            {
                ArithmeticOperation.Add =>
                    ContainsAny(problem, "thêm", "nhận", "được cho", "và", "gộp", "mua thêm", "hái thêm", "nhặt thêm", "có thêm") &&
                    ContainsAny(problem, "tất cả", "tổng cộng", "có bao nhiêu"),

                ArithmeticOperation.Subtract =>
                    ContainsAny(problem, "cho", "tặng", "bớt", "mất", "dùng", "ăn", "bán", "lấy đi", "lấy ra", "bỏ đi", "rời đi", "bay đi") &&
                    ContainsAny(problem, "còn lại", "còn bao nhiêu"),

                ArithmeticOperation.Multiply =>
                    ContainsAny(problem, "mỗi", "từng", "đều có") &&
                    ContainsAny(problem, "tất cả", "tổng cộng", "có bao nhiêu"),

                ArithmeticOperation.Divide =>
                    ContainsAny(problem, "chia", "chia đều", "đều cho", "xếp đều", "phân đều", "chia thành") &&
                    ContainsAny(problem, "mỗi", "một nhóm", "một phần", "mỗi nhóm", "mỗi phần"),

                _ => false
            };
        }

        return operation switch
        {
            ArithmeticOperation.Add =>
                ContainsAny(problem, "more", "received", "added", "and", "bought", "found", "joined") &&
                ContainsAny(problem, "altogether", "in total", "how many"),

            ArithmeticOperation.Subtract =>
                ContainsAny(problem, "gave", "used", "lost", "took away", "ate", "sold", "left", "removed") &&
                ContainsAny(problem, "left", "remain"),

            ArithmeticOperation.Multiply =>
                ContainsAny(problem, "each", "every") &&
                ContainsAny(problem, "in all", "altogether", "how many"),

            ArithmeticOperation.Divide =>
                ContainsAny(problem, "shared", "divided", "split", "placed equally") &&
                ContainsAny(problem, "each", "per group", "each group"),

            _ => false
        };
    }

    private static bool ContainsAny(
        string value,
        params string[] candidates) =>
        candidates.Any(candidate =>
            value.Contains(
                candidate,
                StringComparison.Ordinal));

    private static bool HasClearlyConflictingOperationMeaning(
        string problem,
        ArithmeticOperation expectedOperation,
        AppLanguage language) =>
        Enum.GetValues<ArithmeticOperation>()
            .Where(operation => operation != expectedOperation)
            .Any(operation =>
                HasUnambiguousOperationMeaning(
                    problem,
                    operation,
                    language));

    private static bool IsQuestionSentence(
        string problem,
        AppLanguage language)
    {
        if (problem.Contains('?'))
        {
            return true;
        }

        string lower = problem.ToLowerInvariant();

        return language == AppLanguage.Vietnamese
            ? ContainsAny(
                lower,
                "hỏi ",
                "bao nhiêu",
                "còn lại")
            : ContainsAny(
                lower,
                "how many",
                "how much",
                "what is");
    }

    private static string AppendDefaultQuestion(
        string problem,
        AppLanguage language)
    {
        string statement =
            problem.TrimEnd('.', '!', '?', ';', ':');

        return language == AppLanguage.Vietnamese
            ? $"{statement}. Hỏi kết quả là bao nhiêu?"
            : $"{statement}. What is the answer?";
    }

    private static bool ShouldRebuildSolutionLead(
        string solutionLead,
        AppLanguage language)
    {
        if (string.IsNullOrWhiteSpace(solutionLead) ||
            solutionLead.Contains('='))
        {
            return true;
        }

        string normalized =
            solutionLead
                .Trim()
                .TrimEnd('.', ',', '!', '?', ':', ';', '…')
                .Trim()
                .ToLowerInvariant();

        if (language == AppLanguage.Vietnamese)
        {
            return normalized.StartsWith(
                       "ta thực hiện phép ",
                       StringComparison.Ordinal) ||
                   normalized.StartsWith(
                       "thực hiện phép ",
                       StringComparison.Ordinal) ||
                   normalized.StartsWith(
                       "ta cần thực hiện phép ",
                       StringComparison.Ordinal) ||
                   normalized is
                       "ta tính" or
                       "phép tính cần thực hiện";
        }

        return normalized.StartsWith(
                   "we perform the ",
                   StringComparison.Ordinal) ||
               normalized.StartsWith(
                   "perform the ",
                   StringComparison.Ordinal) ||
               normalized.StartsWith(
                   "we need to perform the ",
                   StringComparison.Ordinal) ||
               normalized is
                   "we calculate" or
                   "the calculation to perform";
    }

    private static string BuildNaturalSolutionLead(
        string problem,
        ArithmeticOperation operation,
        string unit,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese &&
            TryBuildVietnameseSolutionLeadFromQuestion(
                problem,
                unit,
                out string contextualLead))
        {
            return contextualLead;
        }

        return BuildDefaultSolutionLead(
            operation,
            unit,
            language);
    }

    private static bool TryBuildVietnameseSolutionLeadFromQuestion(
        string problem,
        string unit,
        out string solutionLead)
    {
        solutionLead = string.Empty;

        int questionIndex =
            problem.LastIndexOf(
                "bao nhiêu",
                StringComparison.OrdinalIgnoreCase);

        if (questionIndex < 0)
        {
            return false;
        }

        string questionContext =
            problem[..questionIndex].TrimEnd();

        int clauseBoundary =
            questionContext.LastIndexOfAny(
                ['.', '!', '?', ';', ',']);

        if (clauseBoundary >= 0)
        {
            questionContext =
                questionContext[(clauseBoundary + 1)..];
        }

        questionContext =
            TrimVietnameseQuestionStarter(
                questionContext.Trim());

        if (!IsUsefulVietnameseQuestionContext(
                questionContext))
        {
            return false;
        }

        bool alreadyNamesQuantity =
            questionContext.StartsWith(
                "số ",
                StringComparison.OrdinalIgnoreCase);

        bool alreadyEndsWithIs =
            questionContext.EndsWith(
                " là",
                StringComparison.OrdinalIgnoreCase) ||
            questionContext.Equals(
                "là",
                StringComparison.OrdinalIgnoreCase);

        string core = alreadyNamesQuantity
            ? questionContext
            : $"Số {unit} {questionContext}";

        solutionLead = alreadyEndsWithIs
            ? $"{core}:"
            : $"{core} là:";

        return true;
    }

    private static string TrimVietnameseQuestionStarter(
        string value)
    {
        string result = value;

        string[] starters =
        [
            "vậy thì ",
            "cho biết ",
            "vậy ",
            "hỏi ",
            "thì "
        ];

        bool removed;

        do
        {
            removed = false;

            foreach (string starter in starters)
            {
                if (!result.StartsWith(
                        starter,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result =
                    result[starter.Length..].TrimStart();

                removed = true;
                break;
            }
        }
        while (removed);

        return result;
    }

    private static bool IsUsefulVietnameseQuestionContext(
        string value)
    {
        if (value.Length is < 3 or > 120)
        {
            return false;
        }

        string normalized =
            value
                .Trim()
                .TrimEnd('.', ',', '!', '?', ':', ';', '…')
                .Trim()
                .ToLowerInvariant();

        return normalized is not
            "có" and not
            "có tất cả" and not
            "tất cả có" and not
            "tổng cộng có" and not
            "còn lại" and not
            "mỗi phần có" and not
            "mỗi nhóm có";
    }

    private static string BuildDefaultSolutionLead(
        ArithmeticOperation operation,
        string unit,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return operation switch
            {
                ArithmeticOperation.Subtract =>
                    $"Số {unit} còn lại là:",
                ArithmeticOperation.Divide =>
                    $"Số {unit} trong mỗi phần là:",
                _ =>
                    $"Tổng số {unit} là:"
            };
        }

        return operation switch
        {
            ArithmeticOperation.Subtract =>
                $"The number of {unit} left is:",
            ArithmeticOperation.Divide =>
                $"The number of {unit} in each group is:",
            _ =>
                $"The total number of {unit} is:"
        };
    }

    private static string NormalizeSingleLine(
        string? value) =>
        WhitespaceRegex().Replace(
            value?.Trim() ?? string.Empty,
            " ");

    private static string NormalizeClassLabels(
        string problem,
        out bool hasInvalidClassLabel)
    {
        bool invalid = false;

        string normalized =
            ClassLabelRegex().Replace(
                problem,
                match =>
                {
                    string prefix =
                        match.Groups["prefix"].Value;

                    if (PrimarySchoolClassCatalog.TryNormalizeLabel(
                            match.Groups["label"].Value,
                            out string normalizedLabel))
                    {
                        return prefix + normalizedLabel;
                    }

                    invalid = true;
                    return match.Value;
                });

        hasInvalidClassLabel = invalid;
        return normalized;
    }

    [GeneratedRegex(@"(?<!\d)-?\d+(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
        @"(?<prefix>\b(?:lớp|class)\s*)(?<label>\d+(?:\s*/\s*\d+|[A-Za-z])?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassLabelRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

}
