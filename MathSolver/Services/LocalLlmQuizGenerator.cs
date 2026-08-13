using LLama;
using LLama.Common;
using LLama.Sampling;
using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>
/// Dùng LLM cục bộ để diễn đạt hợp đồng số học, Tìm x hoặc hình học do engine
/// sở hữu thành toán đố. Model chỉ viết ngôn ngữ tự nhiên; dữ kiện và đáp án
/// do C# giữ.
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
    // Đủ chứa contract ban đầu, tối đa ba JSON và hai feedback validation
    // trong cùng KV cache. Phần tăng nhỏ này tránh retry cuối bị tràn context.
    public const uint ContextSize = 2048;

    private readonly ArithmeticQuizGenerator _quizGenerator;
    private readonly GeometryQuizGenerator _geometryQuizGenerator;
    private readonly FindXQuizGenerator _findXQuizGenerator;
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
        BasicArithmeticEngine engine,
        GeometryQuizGenerator geometryQuizGenerator,
        FindXQuizGenerator findXQuizGenerator)
    {
        _quizGenerator =
            quizGenerator ??
            throw new ArgumentNullException(nameof(quizGenerator));

        _engine =
            engine ??
            throw new ArgumentNullException(nameof(engine));

        _geometryQuizGenerator =
            geometryQuizGenerator ??
            throw new ArgumentNullException(nameof(geometryQuizGenerator));

        _findXQuizGenerator =
            findXQuizGenerator ??
            throw new ArgumentNullException(nameof(findXQuizGenerator));
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
        QuizProblemRequest problemRequest,
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
        int generatedTokenCount = 0;
        TimeSpan totalGenerationTime = TimeSpan.Zero;
        var attemptReports =
            new List<LlmQuizAttemptReport>();

        try
        {
            ArithmeticQuizQuestion contract =
                problemRequest.Kind switch
                {
                    QuizProblemKind.Geometry =>
                        _geometryQuizGenerator.Generate(
                            mode,
                            language),
                    QuizProblemKind.Arithmetic =>
                        CreateNaturalLanguageContract(
                            mode,
                            problemRequest.ArithmeticOperation),
                    QuizProblemKind.FindX =>
                        _findXQuizGenerator.Generate(mode),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(problemRequest))
                };

            // Chỉ chọn tối đa một tên cho cả lượt sinh. Mọi lần retry giữ
            // nguyên gợi ý này và catalog đầy đủ không bị nối vào prompt.
            WordProblemStudent? selectedStudent =
                LlmQuizPromptBuilder.SelectStudent(
                    language);

            // Chọn đúng một nhóm đồ vật cho cả lượt sinh và mọi lần retry.
            // Catalog đầy đủ ở C# không bị ghép vào prompt.
            WordProblemStoryContext selectedStoryContext =
                LlmQuizPromptBuilder.SelectStoryContext(
                    language);

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

            // Một context/KV duy nhất được giữ trong toàn bộ chuỗi retry để
            // model nhớ prompt, JSON sai và phản hồi validation trước đó.
            // Context chỉ được giải phóng khi lượt GenerateAsync đã kết thúc:
            // đề được chấp nhận, hết số lần thử, bị hủy hoặc gặp lỗi runtime.
            // LLamaWeights vẫn được cache riêng giữa các câu như trước.
            using LLamaContext context =
                weights.CreateContext(modelParameters);

            var executor =
                new InteractiveExecutor(context);

            string? previousErrorCode = null;
            string? previousValidationFeedback = null;

            for (int attempt = 1;
                 attempt <= MaximumAttempts;
                 attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                completedAttempts = attempt;

                var attemptDiagnostics =
                    new List<LlmQuizDiagnostic>();

                void ReportDiagnostic(
                    LlmQuizDiagnosticEvent diagnosticEvent,
                    LlmQuizProgressStage stage,
                    string? detail = null,
                    int characterCount = 0,
                    string? rawModelOutput = null)
                {
                    var diagnostic =
                        new LlmQuizDiagnostic(
                            diagnosticEvent,
                            attempt,
                            MaximumAttempts,
                            detail,
                            characterCount);

                    attemptDiagnostics.Add(diagnostic);

                    progress?.Report(
                        new(
                            stage,
                            attempt,
                            MaximumAttempts,
                            RawModelOutput: rawModelOutput,
                            Diagnostic: diagnostic));
                }

                ReportDiagnostic(
                    LlmQuizDiagnosticEvent.AttemptStarted,
                    LlmQuizProgressStage.Generating);

                string prompt;

                if (attempt == 1)
                {
                    string userPrompt =
                        contract.FindXProblem is FindXQuizContract findX
                            ? LlmQuizPromptBuilder.BuildFindXUserPrompt(
                                findX,
                                language,
                                selectedStudent,
                                selectedStoryContext,
                                previousErrorCode: null)
                            : contract.GeometryProblem is GeometryQuizContract geometry
                            ? LlmQuizPromptBuilder.BuildGeometryUserPrompt(
                                geometry,
                                language,
                                selectedStudent,
                                previousErrorCode: null)
                            : LlmQuizPromptBuilder.BuildUserPrompt(
                                contract.Expression,
                                language,
                                selectedStudent,
                                selectedStoryContext,
                                previousErrorCode: null);

                    prompt = useManualGemma4Template
                        ? LlmQuizPromptBuilder.BuildGemma4Prompt(
                            systemPrompt,
                            userPrompt)
                        : string.Concat(
                            systemPrompt,
                            Environment.NewLine,
                            Environment.NewLine,
                            userPrompt);
                }
                else
                {
                    // InteractiveExecutor tiếp tục ngay trên KV cache cũ.
                    // Chỉ gửi feedback ngắn thay vì lặp lại toàn bộ contract;
                    // model vẫn nhìn thấy đề sai và dữ kiện gốc trong context.
                    prompt = useManualGemma4Template
                        ? LlmQuizPromptBuilder.BuildGemma4RetryPrompt(
                            previousErrorCode ?? "InvalidWordProblem",
                            language,
                            previousValidationFeedback)
                        : LlmQuizPromptBuilder.BuildRetryPrompt(
                            previousErrorCode ?? "InvalidWordProblem",
                            language,
                            previousValidationFeedback);
                }

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
                var speedReportTimer =
                    System.Diagnostics.Stopwatch.StartNew();
                var generationTimer =
                    new System.Diagnostics.Stopwatch();
                int attemptGeneratedTokenCount = 0;

                await foreach (string token in
                    executor.InferAsync(
                        prompt,
                        inferenceParameters,
                        cancellationToken))
                {
                    attemptGeneratedTokenCount++;

                    // InferAsync evaluates the prompt before yielding its
                    // first generated token. Start timing at that first yield
                    // so token/s measures decode speed rather than model load
                    // or prompt-prefill time.
                    if (attemptGeneratedTokenCount == 1)
                    {
                        generationTimer.Start();
                    }

                    output.Append(token);

                    string? previewToReport = null;

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
                            previewToReport = preview;
                        }
                    }

                    bool shouldReportSpeed =
                        speedReportTimer.ElapsedMilliseconds >= 250;

                    if (shouldReportSpeed)
                    {
                        speedReportTimer.Restart();
                    }

                    if (previewToReport is not null ||
                        shouldReportSpeed)
                    {
                        int currentGeneratedTokenCount =
                            generatedTokenCount +
                            attemptGeneratedTokenCount;

                        TimeSpan currentGenerationTime =
                            totalGenerationTime +
                            generationTimer.Elapsed;

                        progress?.Report(
                            new(
                                LlmQuizProgressStage.Generating,
                                attempt,
                                MaximumAttempts,
                                previewToReport,
                                currentGeneratedTokenCount,
                                CalculateTokensPerSecond(
                                    currentGeneratedTokenCount,
                                    currentGenerationTime),
                                RawModelOutput: output.ToString()));
                    }
                }

                generationTimer.Stop();

                generatedTokenCount +=
                    attemptGeneratedTokenCount;

                totalGenerationTime +=
                    generationTimer.Elapsed;

                double tokensPerSecond =
                    CalculateTokensPerSecond(
                        generatedTokenCount,
                        totalGenerationTime);

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
                            completedPreview,
                            generatedTokenCount,
                            tokensPerSecond,
                            RawModelOutput: rawOutput));
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Local LLM attempt {attempt} raw output: {rawOutput}");

                ReportDiagnostic(
                    LlmQuizDiagnosticEvent.JsonReceived,
                    LlmQuizProgressStage.Validating,
                    characterCount: rawOutput.Length,
                    rawModelOutput: rawOutput);

                if (!LlmWordProblemParser.TryParse(
                        rawOutput,
                        out LlmWordProblemDraft? draft,
                        out string parseErrorCode))
                {
                    previousErrorCode = parseErrorCode;
                    previousValidationFeedback =
                        LlmQuizPromptBuilder.BuildParserFeedback(
                            parseErrorCode,
                            language);

                    ReportDiagnostic(
                        LlmQuizDiagnosticEvent.ParseFailed,
                        LlmQuizProgressStage.Validating,
                        detail: previousValidationFeedback,
                        rawModelOutput: rawOutput);

                    System.Diagnostics.Debug.WriteLine(
                        $"Local LLM attempt {attempt} rejected by parser: {parseErrorCode}");
                }
                else
                {
                    ReportDiagnostic(
                        LlmQuizDiagnosticEvent.ParseSucceeded,
                        LlmQuizProgressStage.Validating,
                        rawModelOutput: rawOutput);

                    LlmWordProblemValidationResult validation =
                        contract.FindXProblem is FindXQuizContract validatedFindX
                            ? _wordProblemValidator.ValidateFindX(
                                draft,
                                validatedFindX,
                                selectedStoryContext.NaturalReference,
                                LlmQuizPromptBuilder.GetFindXAnswerUnit(
                                    validatedFindX,
                                    selectedStoryContext,
                                    language),
                                language)
                            : contract.GeometryProblem is GeometryQuizContract validatedGeometry
                            ? _wordProblemValidator.ValidateGeometry(
                                draft,
                                validatedGeometry,
                                language)
                            : _wordProblemValidator.Validate(
                                draft,
                                contract.Expression,
                                contract.CorrectAnswer,
                                selectedStoryContext,
                                language);

                    if (validation.IsValid &&
                        validation.WordProblem is not null)
                    {
                        ReportDiagnostic(
                            LlmQuizDiagnosticEvent.ValidationSucceeded,
                            LlmQuizProgressStage.Validating,
                            rawModelOutput: rawOutput);

                        attemptReports.Add(
                            new(
                                attempt,
                                MaximumAttempts,
                                rawOutput,
                                attemptDiagnostics.ToArray()));

                        return new(
                            contract with
                            {
                                WordProblem = validation.WordProblem
                            },
                            attempt,
                            null,
                            true,
                            generatedTokenCount,
                            tokensPerSecond,
                            attemptReports.ToArray());
                    }

                    previousErrorCode =
                        validation.ErrorCode ??
                        "InvalidWordProblem";
                    previousValidationFeedback =
                        validation.Feedback;

                    ReportDiagnostic(
                        LlmQuizDiagnosticEvent.ValidationFailed,
                        LlmQuizProgressStage.Validating,
                        detail:
                            previousValidationFeedback ??
                            previousErrorCode,
                        rawModelOutput: rawOutput);

                    System.Diagnostics.Debug.WriteLine(
                        $"Local LLM attempt {attempt} rejected by validator: {previousErrorCode}");
                }

                if (attempt < MaximumAttempts)
                {
                    ReportDiagnostic(
                        LlmQuizDiagnosticEvent.RetryScheduled,
                        LlmQuizProgressStage.Retrying,
                        detail:
                            previousValidationFeedback ??
                            previousErrorCode,
                        rawModelOutput: rawOutput);
                }
                else
                {
                    ReportDiagnostic(
                        LlmQuizDiagnosticEvent.GenerationFailed,
                        LlmQuizProgressStage.Validating,
                        detail:
                            previousValidationFeedback ??
                            previousErrorCode,
                        rawModelOutput: rawOutput);
                }

                attemptReports.Add(
                    new(
                        attempt,
                        MaximumAttempts,
                        rawOutput,
                        attemptDiagnostics.ToArray()));
            }

            return new(
                null,
                completedAttempts,
                previousErrorCode ?? "InvalidWordProblem",
                modelWasLoaded,
                generatedTokenCount,
                CalculateTokensPerSecond(
                    generatedTokenCount,
                    totalGenerationTime),
                attemptReports.ToArray());
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
                modelWasLoaded,
                generatedTokenCount,
                CalculateTokensPerSecond(
                    generatedTokenCount,
                    totalGenerationTime),
                attemptReports.ToArray());
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

    private static double CalculateTokensPerSecond(
        int generatedTokenCount,
        TimeSpan elapsed)
    {
        return generatedTokenCount > 0 &&
               elapsed.TotalSeconds > 0d
            ? generatedTokenCount / elapsed.TotalSeconds
            : 0d;
    }
}

internal static class LlmQuizPromptBuilder
{
    private static readonly WordProblemContextCategory[] StoryContextCategories =
        Enum.GetValues<WordProblemContextCategory>();

    public static string BuildSystemPrompt(
        AppLanguage language)
    {
        return language == AppLanguage.Vietnamese
            ? "Bạn là giáo viên tiểu học Việt Nam thân thiện. Bạn viết bài toán đố số học, Tìm x hoặc hình học từ dữ kiện bắt buộc. Không tự đổi số, phép tính, vai trò của x, hình, đơn vị, đại lượng cần tìm hay đáp án. solution_lead chỉ là câu dẫn đứng trước phép tính; tuyệt đối không chứa số, phép tính, dấu bằng, kết quả hoặc đáp án. Chỉ trả về đúng một JSON hợp lệ, trình bày mỗi thuộc tính trên một dòng như schema, không Markdown, không lời chào và không giải thích."
            : "You are a friendly elementary-school teacher writing for an English-language primary curriculum. Write arithmetic, Find-x, or geometry word problems from the required facts. Never change the numbers, operation, role of x, shape, unit, requested measurement, or answer. solution_lead is only the sentence before the calculation; it must never contain a number, calculation, equals sign, result, or answer. Return exactly one valid JSON object with one property per line as shown in the schema and no Markdown, greeting, or commentary.";
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

    public static string BuildGemma4RetryPrompt(
        string errorCode,
        AppLanguage language,
        string? validationFeedback)
    {
        // Kết thúc model turn trước, thêm một user turn sửa lỗi rồi mở model
        // turn mới. InteractiveExecutor giữ KV cache nên model vẫn thấy đầy
        // đủ contract và JSON đã trả ở lần trước.
        return string.Concat(
            "<turn|>\n",
            "<|turn>user\n",
            BuildRetryPrompt(
                errorCode,
                language,
                validationFeedback),
            "<turn|>\n",
            "<|turn>model");
    }

    public static string BuildRetryPrompt(
        string errorCode,
        AppLanguage language,
        string? validationFeedback)
    {
        string correction =
            BuildRetryInstruction(
                errorCode,
                language)
            .Trim();

        string feedback =
            string.IsNullOrWhiteSpace(validationFeedback)
                ? correction
                : validationFeedback.Trim();

        return language == AppLanguage.Vietnamese
            ? $"Đề vừa tạo không vượt qua validation C# ({errorCode}).\nCHI TIẾT LỖI: {feedback}\nYÊU CẦU SỬA: {correction} Hãy giữ nguyên dữ kiện gốc trong context và trả lại đúng một JSON đã sửa, mỗi thuộc tính trên một dòng, không Markdown hay giải thích."
            : $"The previous problem failed C# validation ({errorCode}).\nEXACT ERROR: {feedback}\nREQUIRED FIX: {correction} Keep the original facts already present in the context and return exactly one corrected JSON object with one property per line and no Markdown or commentary.";
    }

    public static string BuildParserFeedback(
        string errorCode,
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? errorCode switch
            {
                "EmptyModelOutput" =>
                    "Phản hồi rỗng: model chưa tạo ra JSON nào. Cần trả đủ bốn trường problem_text, subject_name, answer_unit và solution_lead.",
                _ =>
                    "Phản hồi không phải đúng một JSON object hoàn chỉnh hoặc sai schema. Không thêm Markdown, lời giải thích hay JSON thứ hai; cần trả đủ bốn trường problem_text, subject_name, answer_unit và solution_lead."
            }
            : errorCode switch
            {
                "EmptyModelOutput" =>
                    "The response was empty: the model produced no JSON. Return all four fields: problem_text, subject_name, answer_unit, and solution_lead.",
                _ =>
                    "The response was not exactly one complete JSON object or did not match the schema. Do not add Markdown, commentary, or a second JSON object; return all four fields: problem_text, subject_name, answer_unit, and solution_lead."
            };

    public static string BuildUserPrompt(
        IntegerArithmeticExpression expression,
        AppLanguage language,
        WordProblemStudent? selectedStudent,
        WordProblemStoryContext selectedStoryContext,
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
            selectedStudent is null
                ? language == AppLanguage.Vietnamese
                    ? "Tên riêng không bắt buộc; hãy dùng cách gọi chung tự nhiên như một bạn học sinh, các bạn trong lớp hoặc một người thân."
                    : "A personal name is optional; use a natural generic reference such as a student, the classmates, or a family member."
                : language == AppLanguage.Vietnamese
                    ? $"Nếu dùng tên riêng, chỉ gợi ý dùng \"{selectedStudent.NaturalReference}\"; cũng có thể dùng cách gọi chung tự nhiên."
                    : $"If a personal name is used, use \"{selectedStudent.NaturalReference}\"; a natural generic reference is also valid.";

        string classroomRule =
            language == AppLanguage.Vietnamese
                ? "Nếu dùng tên lớp, khối chỉ được từ lớp 1 đến lớp 5. Lớp con dạng số chỉ từ 1 đến 9 và phải viết lớp 3/1 ... lớp 3/9; dạng chữ đi theo alphabet từ lớp 3A ... lớp 3I. Không viết lớp 30, lớp 3/0, lớp 3/10 hoặc lớp 3J; không viết gọn lớp 31 mà phải viết lớp 3/1."
                : "If a class label is used, the grade must be Class 1 through Class 5. Numeric sections are only 1 through 9 and must be written Class 3/1 ... Class 3/9; alphabetic sections follow A through I, as in Class 3A ... Class 3I. Never write Class 30, Class 3/0, Class 3/10, or Class 3J; write Class 3/1 instead of compact Class 31.";

        string storyContextRule =
            BuildStoryContextRule(
                selectedStoryContext,
                language);

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
            - {{storyContextRule}}
            - {{classroomRule}}
            - For subtraction, the left quantity must decrease by the right quantity.
            - For division, divide the left total exactly into the right number of equal groups.
            - solution_lead is only one short textbook lead-in sentence before the student's calculation. It must repeat the exact answer_unit noun phrase (or the same pen noun with cây/cái/chiếc exchanged) and end with a colon. It must contain no number, arithmetic expression, operator, equals sign, calculated result, or answer. Valid example: "Số quả chôm chôm mẹ còn lại là:". Forbidden example: "Số quả chôm chôm mẹ còn lại là: 11 - 1 = 10 quả chôm chôm." Never replace a specific unit such as "cuốn sổ tay" with a broader word such as "sách".
            - answer_unit is a short noun phrase without a number.
            - subject_name is the person, group, or object described in problem_text; it may be a generic reference and does not require a personal name.
            {{retry}}

            JSON schema:
            {
              "problem_text": "... ?",
              "subject_name": "...",
              "answer_unit": "...",
              "solution_lead": "...:"
            }
            """);
    }

    private static string BuildStoryContextRule(
        WordProblemStoryContext storyContext,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese &&
            WordProblemUnitEquivalence.IsVietnamesePenUnit(
                storyContext.NaturalReference))
        {
            string alternatives = string.Join(
                " / ",
                WordProblemUnitEquivalence
                    .GetVietnamesePenVariants(
                        storyContext.NaturalReference)
                    .Select(value => $"\"{value}\""));

            return $"Ngữ cảnh được chọn là bút. Trong problem_text có thể dùng tự nhiên một trong các cách tương đương: {alternatives}. answer_unit phải dùng đúng cùng cách gọi đã chọn trong problem_text; cây/cái/chiếc chỉ là từ chỉ loại, không làm thay đổi đồ vật.";
        }

        return language == AppLanguage.Vietnamese
            ? $"Ngữ cảnh được chọn là \"{storyContext.NaturalReference}\". Hãy dùng đúng loại đồ vật này trong đề và đặt answer_unit là \"{storyContext.AnswerUnit}\"."
            : $"The selected story item is \"{storyContext.NaturalReference}\". Use this exact kind of item in the problem and set answer_unit to \"{storyContext.AnswerUnit}\".";
    }

    private static string BuildFindXStoryItemRule(
        WordProblemStoryContext storyContext,
        string answerUnit,
        AppLanguage language)
    {
        bool isVietnamesePen =
            language == AppLanguage.Vietnamese &&
            WordProblemUnitEquivalence.IsVietnamesePenUnit(
                storyContext.NaturalReference);

        if (!isVietnamesePen)
        {
            return language == AppLanguage.Vietnamese
                ? $"Dùng đúng đồ vật \"{storyContext.NaturalReference}\" và đặt answer_unit là \"{answerUnit}\"."
                : $"Use the exact story item \"{storyContext.NaturalReference}\" and set answer_unit to \"{answerUnit}\".";
        }

        string alternatives = string.Join(
            " / ",
            WordProblemUnitEquivalence
                .GetVietnamesePenVariants(
                    storyContext.NaturalReference)
                .Select(value => $"\"{value}\""));
        bool xCountsPens =
            WordProblemUnitEquivalence.IsVietnamesePenUnit(
                answerUnit);

        return xCountsPens
            ? $"Dùng một trong các cách gọi tương đương {alternatives}. answer_unit phải dùng cùng cách gọi bút đã chọn trong problem_text."
            : $"Dùng một trong các cách gọi tương đương {alternatives} trong problem_text; vì x đếm số nhóm nên answer_unit vẫn phải đúng là \"{answerUnit}\".";
    }

    public static string GetFindXAnswerUnit(
        FindXQuizContract contract,
        WordProblemStoryContext storyContext,
        AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(storyContext);

        bool answerIsGroupCount =
            (contract.Operation == ArithmeticOperation.Multiply &&
             contract.UnknownIsLeftOperand) ||
            (contract.Operation == ArithmeticOperation.Divide &&
             !contract.UnknownIsLeftOperand);

        return answerIsGroupCount
            ? language == AppLanguage.Vietnamese
                ? "nhóm"
                : "groups"
            : storyContext.AnswerUnit;
    }

    public static string BuildFindXUserPrompt(
        FindXQuizContract contract,
        AppLanguage language,
        WordProblemStudent? selectedStudent,
        WordProblemStoryContext selectedStoryContext,
        string? previousErrorCode)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(selectedStoryContext);

        string languageName =
            language == AppLanguage.Vietnamese
                ? "Vietnamese used in Vietnamese primary schools"
                : "natural English used in an English-language elementary school";

        string answerUnit =
            GetFindXAnswerUnit(
                contract,
                selectedStoryContext,
                language);

        string relationship =
            GetFindXRelationshipRule(
                contract,
                selectedStoryContext,
                language);

        string storyItemRule =
            BuildFindXStoryItemRule(
                selectedStoryContext,
                answerUnit,
                language);

        string characterRule =
            selectedStudent is null
                ? language == AppLanguage.Vietnamese
                    ? "Tên riêng không bắt buộc; có thể dùng một bạn học sinh hoặc một nhóm học sinh."
                    : "A personal name is optional; a student or group of students is sufficient."
                : language == AppLanguage.Vietnamese
                    ? $"Nếu dùng tên riêng, chỉ dùng gợi ý \"{selectedStudent.NaturalReference}\"."
                    : $"If a personal name is used, use only \"{selectedStudent.NaturalReference}\".";

        string classroomRule =
            language == AppLanguage.Vietnamese
                ? "Nếu dùng tên lớp, khối chỉ từ lớp 1 đến lớp 5; lớp con chỉ từ 1 đến 9 hoặc A đến I."
                : "If a class label is used, use only grades 1 through 5 and sections 1 through 9 or A through I.";

        string retry =
            string.IsNullOrWhiteSpace(previousErrorCode)
                ? string.Empty
                : BuildRetryInstruction(
                    previousErrorCode,
                    language);

        return FormattableString.Invariant(
            $$"""
            Write one natural, age-appropriate find-x word problem in {{languageName}}.
            Required equation owned by C#: {{contract.EquationText}}
            Required known number: {{contract.KnownValue}}
            Required result number: {{contract.ResultValue}}
            The value of x is intentionally hidden by C#; do not calculate or guess it in problem_text.
            Required story item: {{selectedStoryContext.NaturalReference}}
            Required answer_unit: {{answerUnit}}

            Mathematical relationship that the wording must express:
            - {{relationship}}

            Rules:
            - problem_text must contain the required known number and result number as digits, with exactly the multiplicity shown by the equation, and no other arithmetic quantities. One valid class label is allowed as metadata.
            - Do not write the symbol x, do not reveal or calculate the hidden value, and do not state an equivalent extra numeric fact.
            - {{storyItemRule}}
            - The wording must lead to exactly {{contract.EquationText}}. Do not swap what is initially known, added, removed, grouped, shared, or left over.
            - Keep answer_unit semantically equivalent to "{{answerUnit}}"; only cây/cái/chiếc may be exchanged for a Vietnamese pen unit.
            - {{characterRule}}
            - {{classroomRule}}
            - solution_lead is only one short textbook lead-in sentence before the student's calculation. It introduces finding x, repeats the answer_unit noun phrase, ends with a colon, and contains no number, equation, operator, equals sign, value of x, result, or answer.
            - subject_name is the person, group, or object described in problem_text and contains no number.
            {{retry}}

            JSON schema:
            {
              "problem_text": "... ?",
              "subject_name": "...",
              "answer_unit": "{{answerUnit}}",
              "solution_lead": "...:"
            }
            """);
    }

    private static string GetFindXRelationshipRule(
        FindXQuizContract contract,
        WordProblemStoryContext storyContext,
        AppLanguage language)
    {
        string item = storyContext.NaturalReference;
        bool vietnamese = language == AppLanguage.Vietnamese;

        return (contract.Operation, contract.UnknownIsLeftOperand) switch
        {
            (ArithmeticOperation.Add, true) => vietnamese
                ? $"Một số {item} chưa biết ban đầu, thêm {contract.KnownValue}, thì có tất cả {contract.ResultValue}."
                : $"An unknown initial number of {item}, plus {contract.KnownValue}, gives a total of {contract.ResultValue}.",
            (ArithmeticOperation.Add, false) => vietnamese
                ? $"Ban đầu có {contract.KnownValue} {item}, nhận thêm một số chưa biết, thì có tất cả {contract.ResultValue}."
                : $"There are initially {contract.KnownValue} {item}; an unknown number is added, giving {contract.ResultValue} in total.",
            (ArithmeticOperation.Subtract, true) => vietnamese
                ? $"Một số {item} chưa biết ban đầu, bớt đi {contract.KnownValue}, thì còn {contract.ResultValue}."
                : $"Remove {contract.KnownValue} from an unknown initial number of {item}; {contract.ResultValue} remains.",
            (ArithmeticOperation.Subtract, false) => vietnamese
                ? $"Ban đầu có {contract.KnownValue} {item}, bớt đi một số chưa biết, thì còn {contract.ResultValue}."
                : $"There are initially {contract.KnownValue} {item}; an unknown number is removed, leaving {contract.ResultValue}.",
            (ArithmeticOperation.Multiply, true) => vietnamese
                ? $"Có một số nhóm chưa biết, mỗi nhóm có {contract.KnownValue} {item}, tổng cộng có {contract.ResultValue} {item}."
                : $"There is an unknown number of groups; each group has {contract.KnownValue} {item}, for a total of {contract.ResultValue} {item}.",
            (ArithmeticOperation.Multiply, false) => vietnamese
                ? $"Có {contract.KnownValue} nhóm, mỗi nhóm có một số {item} chưa biết, tổng cộng có {contract.ResultValue} {item}."
                : $"There are {contract.KnownValue} groups with an unknown number of {item} in each group, for a total of {contract.ResultValue} {item}.",
            (ArithmeticOperation.Divide, true) => vietnamese
                ? $"Một tổng số {item} chưa biết được chia đều vào {contract.KnownValue} nhóm, mỗi nhóm có {contract.ResultValue} {item}."
                : $"An unknown total number of {item} is divided equally among {contract.KnownValue} groups; each group has {contract.ResultValue} {item}.",
            (ArithmeticOperation.Divide, false) => vietnamese
                ? $"Tổng cộng có {contract.KnownValue} {item} được chia đều vào một số nhóm chưa biết, mỗi nhóm có {contract.ResultValue} {item}."
                : $"A total of {contract.KnownValue} {item} is divided equally among an unknown number of groups; each group has {contract.ResultValue} {item}.",
            _ => throw new ArgumentOutOfRangeException(nameof(contract))
        };
    }

    public static string BuildGeometryUserPrompt(
        GeometryQuizContract contract,
        AppLanguage language,
        WordProblemStudent? selectedStudent,
        string? previousErrorCode)
    {
        ArgumentNullException.ThrowIfNull(contract);

        string languageName =
            language == AppLanguage.Vietnamese
                ? "Vietnamese used in Vietnamese primary schools"
                : "natural English used in an English-language elementary school";

        string measurement =
            (contract.Measurement, language) switch
            {
                (GeometryMeasurement.Perimeter, AppLanguage.Vietnamese) => "chu vi",
                (GeometryMeasurement.Area, AppLanguage.Vietnamese) => "diện tích",
                (GeometryMeasurement.TotalArea, AppLanguage.Vietnamese) => "diện tích toàn phần",
                (GeometryMeasurement.Volume, AppLanguage.Vietnamese) => "thể tích",
                (GeometryMeasurement.Perimeter, _) => "perimeter",
                (GeometryMeasurement.Area, _) => "area",
                (GeometryMeasurement.TotalArea, _) => "total surface area",
                (GeometryMeasurement.Volume, _) => "volume",
                _ => throw new ArgumentOutOfRangeException(nameof(contract))
            };

        string dimensionFacts = string.Join(
            Environment.NewLine,
            contract.Dimensions.Select(pair =>
                $"- {GetDimensionName(pair.Key, language)}: {pair.Value} {contract.LengthUnitSymbol}"));

        string characterRule =
            selectedStudent is null
                ? language == AppLanguage.Vietnamese
                    ? "Không cần dùng tên riêng; hãy tập trung vào đồ vật thực tế đã chọn."
                    : "A personal name is unnecessary; focus on the selected real-world object."
                : language == AppLanguage.Vietnamese
                    ? $"Tên riêng không bắt buộc. Nếu cần người quan sát, chỉ gợi ý dùng \"{selectedStudent.NaturalReference}\"."
                    : $"A personal name is optional. If an observer is useful, use only \"{selectedStudent.NaturalReference}\".";

        string retry =
            string.IsNullOrWhiteSpace(previousErrorCode)
                ? string.Empty
                : BuildRetryInstruction(previousErrorCode, language);

        return FormattableString.Invariant(
            $$"""
            Write one natural, age-appropriate geometry word problem in {{languageName}}.
            Required real-world object: {{contract.ObjectName}}
            Required shape: {{contract.ShapeName}}
            Required measurement to find: {{measurement}}
            Required length unit for every dimension: {{contract.LengthUnitSymbol}}
            Required answer unit: {{contract.AnswerUnit}}
            Required dimensions:
            {{dimensionFacts}}

            Rules:
            - State that the object has the required shape.
            - problem_text must contain every required dimension as digits with its length unit and no other arithmetic quantities.
            - Ask only for the required measurement. Do not ask for a different measurement.
            - Do not convert units. All given dimensions use {{contract.LengthUnitSymbol}}.
            - Do not calculate or reveal the answer inside problem_text.
            - Use the exact real-world object naturally; do not replace it.
            - {{characterRule}}
            - solution_lead is only one short textbook lead-in sentence before the student's calculation. It names the required measurement, ends with a colon, and contains no dimension number, formula, operator, equals sign, calculated result, or answer.
            - Set answer_unit exactly to "{{contract.AnswerUnit}}". Never use a plain length unit for area or volume.
            - subject_name is the required real-world object, without a number.
            {{retry}}

            JSON schema:
            {
              "problem_text": "... ?",
              "subject_name": "...",
              "answer_unit": "{{contract.AnswerUnit}}",
              "solution_lead": "...:"
            }
            """);
    }

    private static string GetDimensionName(
        string key,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return key switch
            {
                "a" => "chiều dài hoặc cạnh a",
                "b" => "chiều rộng b",
                "h" => "chiều cao h",
                _ => key
            };
        }

        return key switch
        {
            "a" => "length or side a",
            "b" => "width b",
            "h" => "height h",
            _ => key
        };
    }

    public static WordProblemStudent? SelectStudent(
        AppLanguage language)
    {
        WordProblemPeopleProfile people =
            WordProblemPeopleCatalog.GetProfile(
                language);

        return people.Students.Count > 0 &&
               Random.Shared.Next(100) < 45
            ? people.Students[
                Random.Shared.Next(
                    people.Students.Count)]
            : null;
    }

    public static WordProblemStoryContext SelectStoryContext(
        AppLanguage language)
    {
        IReadOnlyList<WordProblemStoryContext> items =
            WordProblemStoryContextCatalog
                .GetProfile(language)
                .Items;

        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                "The word-problem story context catalog is empty.");
        }

        WordProblemContextCategory category =
            StoryContextCategories[
                Random.Shared.Next(
                    StoryContextCategories.Length)];

        int categoryItemCount = 0;

        foreach (WordProblemStoryContext item in items)
        {
            if (item.Category == category)
            {
                categoryItemCount++;
            }
        }

        if (categoryItemCount == 0)
        {
            return items[Random.Shared.Next(items.Count)];
        }

        int selectedIndex =
            Random.Shared.Next(categoryItemCount);

        foreach (WordProblemStoryContext item in items)
        {
            if (item.Category != category)
            {
                continue;
            }

            if (selectedIndex-- == 0)
            {
                return item;
            }
        }

        throw new InvalidOperationException(
            "Could not select a word-problem story context.");
    }

    private static string BuildRetryInstruction(
        string errorCode,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return errorCode switch
            {
                "InvalidJson" or "EmptyModelOutput" =>
                    "Chỉ trả về một JSON object hoàn chỉnh đúng schema.",
                "ProblemNumbersMismatch" =>
                    "Sửa đúng các dữ kiện số mà validator đã chỉ ra; dùng đủ từng số bắt buộc và không thêm số khác.",
                "AnswerRevealedInProblem" =>
                    "Xóa đáp án bị lộ khỏi problem_text; chỉ giữ các dữ kiện đầu vào và câu hỏi.",
                "SolutionLeadRevealsAnswer" =>
                    "Xóa toàn bộ số, phép tính, dấu bằng, kết quả và đáp án khỏi solution_lead. Trường này chỉ được là câu dẫn kết thúc bằng dấu hai chấm, ví dụ: “Số quả chôm chôm mẹ còn lại là:”.",
                "GeometryShapeMismatch" or "GeometryMeasurementMismatch" or
                "GeometryUnitMismatch" or "GeometryObjectMismatch" =>
                    "Giữ nguyên từng dữ kiện hình học trong contract: đồ vật, hình, đại lượng cần tìm, kích thước và đơn vị.",
                "FindXStoryItemMismatch" or "FindXAnswerUnitMismatch" or
                "FindXRelationshipMismatch" =>
                    "Giữ nguyên phương trình Tìm x, dùng đúng đồ vật và answer_unit mà validator đã nêu; không đổi vai trò của số đã biết, kết quả hoặc đại lượng x.",
                "StoryItemMismatch" or "AnswerUnitMismatch" =>
                    "Dùng đúng đồ vật và answer_unit validator đã nêu. Với bút, cây/cái/chiếc là tương đương nhưng phải giữ nguyên phần tên bút.",
                "SolutionLeadUnitMismatch" =>
                    "Viết lại solution_lead bằng đúng cụm answer_unit; không thay đơn vị cụ thể bằng một từ khái quát hơn.",
                "OperationMeaningUnclear" or "OperationMeaningConflict" =>
                    "Bỏ cụm từ gây suy ra sai phép toán và thay bằng hành động tiểu học thể hiện đúng phép tính bắt buộc.",
                "InvalidClassLabel" =>
                    "Chỉ dùng khối 1–5, lớp con 1–9 hoặc A–I; ví dụ lớp 3/1 hoặc lớp 3A.",
                _ =>
                    "Sửa đúng trường được validator nêu, giữ nguyên schema và toàn bộ dữ kiện bắt buộc."
            };
        }

        return errorCode switch
        {
            "InvalidJson" or "EmptyModelOutput" =>
                "Return exactly one complete JSON object matching the schema.",
            "ProblemNumbersMismatch" =>
                "Correct the exact numeric facts named by the validator; use every required value and no other value.",
            "AnswerRevealedInProblem" =>
                "Remove the revealed answer from problem_text; keep only the input facts and the question.",
            "SolutionLeadRevealsAnswer" =>
                "Remove every number, calculation, equals sign, result, and answer from solution_lead. It must only be a lead-in sentence ending with a colon, such as: “The number of rambutans Mother has left is:”.",
            "GeometryShapeMismatch" or "GeometryMeasurementMismatch" or
            "GeometryUnitMismatch" or "GeometryObjectMismatch" =>
                "Preserve every geometry contract fact: object, shape, requested measurement, dimensions, and units.",
            "FindXStoryItemMismatch" or "FindXAnswerUnitMismatch" or
            "FindXRelationshipMismatch" =>
                "Preserve the Find-x equation and use the exact story item and answer_unit named by the validator; do not swap the known value, result, or meaning of x.",
            "StoryItemMismatch" or "AnswerUnitMismatch" =>
                "Use the exact required story item and answer_unit named by the validator.",
            "SolutionLeadUnitMismatch" =>
                "Rewrite solution_lead with the exact answer_unit noun phrase instead of a broader category.",
            "OperationMeaningUnclear" or "OperationMeaningConflict" =>
                "Remove wording that implies the wrong operation and replace it with a clear elementary-school action for the required operation.",
            "InvalidClassLabel" =>
                "Use only grades 1–5 with sections 1–9 or A–I, such as Class 3/1 or Class 3A.",
            _ =>
                "Correct the exact field named by the validator while preserving the schema and all required facts."
        };
    }
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
    string? Feedback,
    MathWordProblem? WordProblem)
{
    public static LlmWordProblemValidationResult Invalid(
        string errorCode,
        string feedback) =>
        new(false, errorCode, feedback, null);
}

internal sealed partial class LlmWordProblemValidator
{
    public LlmWordProblemValidationResult Validate(
        LlmWordProblemDraft draft,
        IntegerArithmeticExpression expression,
        BigInteger correctAnswer,
        WordProblemStoryContext requiredStoryContext,
        AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(requiredStoryContext);

        if (expression.LeftOperand < int.MinValue ||
            expression.LeftOperand > int.MaxValue ||
            expression.RightOperand < int.MinValue ||
            expression.RightOperand > int.MaxValue ||
            correctAnswer < int.MinValue ||
            correctAnswer > int.MaxValue)
        {
            return LlmWordProblemValidationResult.Invalid(
                "ContractOutOfRange",
                language == AppLanguage.Vietnamese
                    ? "Dữ kiện contract do C# cấp nằm ngoài phạm vi số nguyên 32-bit nên validator không thể đối chiếu an toàn. Đây là lỗi dữ kiện nguồn, không phải lỗi cách viết đề."
                    : "A C# contract value is outside the 32-bit integer range, so the validator cannot compare it safely. This is a source-contract error, not a wording error.");
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
                "InvalidTextLength",
                BuildTextLengthFeedback(
                    problem.Length,
                    12,
                    600,
                    subject.Length,
                    80,
                    unit.Length,
                    80,
                    solutionLead.Length,
                    220,
                    language));
        }

        problem = NormalizeClassLabels(
            problem,
            out string[] invalidClassLabels);

        if (invalidClassLabels.Length > 0)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidClassLabel",
                BuildClassLabelFeedback(
                    invalidClassLabels,
                    language));
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

        if (answer != left &&
            answer != right &&
            numbers.Contains(answer))
        {
            return LlmWordProblemValidationResult.Invalid(
                "AnswerRevealedInProblem",
                language == AppLanguage.Vietnamese
                    ? $"Trong trường problem_text, đề đã ghi lộ đáp án {answer}. Contract chỉ cho phép hai dữ kiện đầu vào {left} và {right}; hãy xóa số {answer} khỏi nội dung đề và chỉ đặt câu hỏi để học sinh tự tính."
                    : $"The problem_text field reveals the answer {answer}. The contract permits only the two input facts {left} and {right}; remove {answer} from the story and ask the student to calculate it.");
        }

        if (numbers.Length < 2 ||
            numbers.Any(number =>
                number != left && number != right) ||
            !numbers.Contains(left) ||
            !numbers.Contains(right) ||
            (left == right && numbers.Count(number => number == left) < 2))
        {
            return LlmWordProblemValidationResult.Invalid(
                "ProblemNumbersMismatch",
                BuildNumberMismatchFeedback(
                    [left, right],
                    numbers,
                    language,
                    language == AppLanguage.Vietnamese
                        ? $"hai dữ kiện của phép tính: {left} và {right}"
                        : $"the two operation inputs: {left} and {right}"));
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
            string? conflictFeedback =
                BuildOperationConflictFeedback(
                    lowerProblem,
                    expression.Operation,
                    left,
                    right,
                    language);

            if (!string.IsNullOrWhiteSpace(conflictFeedback))
            {
                return LlmWordProblemValidationResult.Invalid(
                    "OperationMeaningConflict",
                    conflictFeedback);
            }

            System.Diagnostics.Debug.WriteLine(
                "Local LLM semantic keyword check was inconclusive; " +
                "the problem was accepted after numeric validation.");
        }

        if (!ProblemContainsRequiredStoryItem(
                problem,
                requiredStoryContext.NaturalReference,
                language))
        {
            return LlmWordProblemValidationResult.Invalid(
                "StoryItemMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trong problem_text không có đúng đồ vật bắt buộc “{requiredStoryContext.NaturalReference}” hoặc một cách gọi tương đương được phép. Nếu là bút, chỉ được đổi từ chỉ loại cây/cái/chiếc; không được đổi phần danh từ của đồ vật."
                    : $"problem_text does not contain the required story item “{requiredStoryContext.NaturalReference}”. Use that item exactly and do not substitute a different object.");
        }

        if (!AreAnswerUnitsEquivalent(
                unit,
                requiredStoryContext.AnswerUnit,
                language) ||
            NumberRegex().IsMatch(unit) ||
            unit.Contains('='))
        {
            return LlmWordProblemValidationResult.Invalid(
                "AnswerUnitMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trường answer_unit đang là “{unit}”, nhưng đồ vật bắt buộc là “{requiredStoryContext.AnswerUnit}”. Hãy dùng đúng đơn vị này; riêng bút có thể đổi tương đương giữa cây bút, cái bút và chiếc bút nhưng phải giữ nguyên phần tên bút."
                    : $"answer_unit is “{unit}”, but the required unit is “{requiredStoryContext.AnswerUnit}”. Use that exact unit without a number or equation.");
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

        string? solutionLeadDisclosureFeedback =
            BuildSolutionLeadDisclosureFeedback(
                solutionLead,
                correctAnswer,
                language);

        if (!string.IsNullOrWhiteSpace(
                solutionLeadDisclosureFeedback))
        {
            return LlmWordProblemValidationResult.Invalid(
                "SolutionLeadRevealsAnswer",
                solutionLeadDisclosureFeedback);
        }

        solutionLead =
            ElementaryWordProblemSolutionFormatter
                .NormalizeSolutionLeadPunctuation(
                    solutionLead);

        if (string.IsNullOrWhiteSpace(solutionLead) ||
            IsGenericSolutionLead(
                solutionLead,
                language))
        {
            solutionLead =
                ElementaryWordProblemSolutionFormatter
                    .NormalizeSolutionLeadPunctuation(
                        BuildQuestionBasedSolutionLead(
                            problem,
                            expression.Operation,
                            unit,
                            language));
        }

        if (!SolutionLeadMentionsAnswerUnit(
                solutionLead,
                unit,
                language))
        {
            return LlmWordProblemValidationResult.Invalid(
                "SolutionLeadUnitMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trường solution_lead đang dùng sai hoặc thiếu tên đại lượng của answer_unit “{unit}”. Ví dụ với answer_unit “cuốn sổ tay”, phải viết “Số cuốn sổ tay ... là:”; không được đổi thành “sách” hay “cuốn sách”."
                    : $"solution_lead does not name the answer_unit “{unit}”. Repeat that noun phrase in the solution sentence instead of replacing it with a broader category.");
        }

        return new(
            true,
            null,
            null,
            new MathWordProblem(
                problem,
                solutionLead,
                unit,
                subject));
    }

    public LlmWordProblemValidationResult ValidateFindX(
        LlmWordProblemDraft draft,
        FindXQuizContract contract,
        string requiredStoryItem,
        string requiredAnswerUnit,
        AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.KnownValue < int.MinValue ||
            contract.KnownValue > int.MaxValue ||
            contract.ResultValue < int.MinValue ||
            contract.ResultValue > int.MaxValue ||
            contract.CorrectAnswer < int.MinValue ||
            contract.CorrectAnswer > int.MaxValue)
        {
            return LlmWordProblemValidationResult.Invalid(
                "ContractOutOfRange",
                language == AppLanguage.Vietnamese
                    ? "Dữ kiện Tìm x do C# cấp nằm ngoài phạm vi số nguyên 32-bit. Đây là lỗi contract nguồn, không phải lỗi câu chữ của AI."
                    : "A C# Find-x contract value is outside the 32-bit integer range. This is a source-contract error, not an AI wording error.");
        }

        int known = (int)contract.KnownValue;
        int result = (int)contract.ResultValue;
        int answer = (int)contract.CorrectAnswer;
        string problem = NormalizeSingleLine(draft.ProblemText);
        string subject = NormalizeSingleLine(draft.SubjectName);
        string unit = NormalizeSingleLine(draft.AnswerUnit);
        string solutionLead = NormalizeSingleLine(draft.SolutionLead);

        if (problem.Length is < 20 or > 700 ||
            subject.Length > 100 ||
            unit.Length > 80 ||
            solutionLead.Length > 240)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidTextLength",
                BuildTextLengthFeedback(
                    problem.Length,
                    20,
                    700,
                    subject.Length,
                    100,
                    unit.Length,
                    80,
                    solutionLead.Length,
                    240,
                    language));
        }

        problem = NormalizeClassLabels(
            problem,
            out string[] invalidClassLabels);

        if (invalidClassLabels.Length > 0)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidClassLabel",
                BuildClassLabelFeedback(
                    invalidClassLabels,
                    language));
        }

        if (!IsQuestionSentence(problem, language))
        {
            problem = AppendDefaultQuestion(problem, language);
        }
        else if (!problem.Contains('?'))
        {
            problem = problem.TrimEnd('.', '!', ';', ':') + "?";
        }

        string problemWithoutClassLabels =
            ClassLabelRegex().Replace(problem, " ");

        int[] actualNumbers =
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

        int[] expectedNumbers = [known, result];

        if (!actualNumbers.Order().SequenceEqual(
                expectedNumbers.Order()))
        {
            string answerLeak =
                actualNumbers.Contains(answer) &&
                !FindMultisetDifference(
                    actualNumbers,
                    expectedNumbers).All(value => value != answer)
                    ? language == AppLanguage.Vietnamese
                        ? $" Số thừa {answer} chính là nghiệm x nên đã làm lộ đáp án."
                        : $" The extra value {answer} is the solution for x, so it reveals the answer."
                    : string.Empty;

            return LlmWordProblemValidationResult.Invalid(
                actualNumbers.Contains(answer) &&
                !FindMultisetDifference(
                    actualNumbers,
                    expectedNumbers).All(value => value != answer)
                    ? "AnswerRevealedInProblem"
                    : "ProblemNumbersMismatch",
                BuildNumberMismatchFeedback(
                    expectedNumbers,
                    actualNumbers,
                    language,
                    language == AppLanguage.Vietnamese
                        ? $"hai dữ kiện của phương trình {contract.EquationText}: {known} và {result}"
                        : $"the two facts in equation {contract.EquationText}: {known} and {result}") +
                answerLeak);
        }

        string lowerProblem = problem.ToLowerInvariant();
        string requiredItem = requiredStoryItem.Trim();

        if (requiredItem.Length == 0 ||
            !ProblemContainsRequiredStoryItem(
                problem,
                requiredItem,
                language))
        {
            return LlmWordProblemValidationResult.Invalid(
                "FindXStoryItemMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trong problem_text không có đúng loại đồ vật bắt buộc “{requiredStoryItem}”. Hãy dùng nguyên cụm này để kể lại phương trình {contract.EquationText}, không thay bằng đồ vật khác."
                    : $"problem_text does not contain the required story item “{requiredStoryItem}”. Use that exact phrase to express {contract.EquationText}; do not substitute another item.");
        }

        if (!AreAnswerUnitsEquivalent(
                unit,
                requiredAnswerUnit,
                language))
        {
            return LlmWordProblemValidationResult.Invalid(
                "FindXAnswerUnitMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trường answer_unit đang là “{unit}”, nhưng nghiệm x trong ngữ cảnh này đếm “{requiredAnswerUnit}”. Hãy dùng đúng đơn vị đó; nếu là bút thì cây/cái/chiếc được xem là tương đương, nhưng không được đổi phần tên bút hoặc thêm số."
                    : $"answer_unit is “{unit}”, but x counts “{requiredAnswerUnit}” in this context. Set answer_unit exactly to “{requiredAnswerUnit}”; do not add a number or use the unit of a different fact.");
        }

        if (!HasUnambiguousOperationMeaning(
                lowerProblem,
                contract.Operation,
                language))
        {
            string? conflictFeedback =
                BuildFindXOperationConflictFeedback(
                    lowerProblem,
                    contract,
                    language);

            if (!string.IsNullOrWhiteSpace(conflictFeedback))
            {
                return LlmWordProblemValidationResult.Invalid(
                    "OperationMeaningConflict",
                    conflictFeedback);
            }

            System.Diagnostics.Debug.WriteLine(
                "Find-x semantic keyword check was inconclusive; " +
                "the problem was accepted after contract validation.");
        }

        string? roleFeedback =
            BuildFindXRoleMismatchFeedback(
                lowerProblem,
                contract,
                language);

        if (!string.IsNullOrWhiteSpace(roleFeedback))
        {
            return LlmWordProblemValidationResult.Invalid(
                "FindXRelationshipMismatch",
                roleFeedback);
        }

        if (string.IsNullOrWhiteSpace(subject) ||
            !lowerProblem.Contains(
                subject.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            subject = requiredStoryItem;
        }

        string? solutionLeadDisclosureFeedback =
            BuildSolutionLeadDisclosureFeedback(
                solutionLead,
                contract.CorrectAnswer,
                language);

        if (!string.IsNullOrWhiteSpace(
                solutionLeadDisclosureFeedback))
        {
            return LlmWordProblemValidationResult.Invalid(
                "SolutionLeadRevealsAnswer",
                solutionLeadDisclosureFeedback);
        }

        solutionLead =
            ElementaryWordProblemSolutionFormatter
                .NormalizeSolutionLeadPunctuation(solutionLead);

        if (string.IsNullOrWhiteSpace(solutionLead) ||
            IsGenericSolutionLead(solutionLead, language))
        {
            solutionLead = language == AppLanguage.Vietnamese
                ? $"Số {unit} chưa biết là:"
                : $"The unknown number of {unit} is:";
        }

        if (!SolutionLeadMentionsAnswerUnit(
                solutionLead,
                unit,
                language))
        {
            return LlmWordProblemValidationResult.Invalid(
                "SolutionLeadUnitMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trường solution_lead không nêu đúng đơn vị của x là “{unit}”. Hãy viết câu như “Số {unit} chưa biết là:” và không thay bằng một danh từ rộng hơn."
                    : $"solution_lead does not name x's answer_unit “{unit}”. Use a sentence such as “The unknown number of {unit} is:” without replacing it with a broader noun.");
        }

        return new(
            true,
            null,
            null,
            new MathWordProblem(
                problem,
                solutionLead,
                unit,
                subject));
    }

    private static bool ProblemContainsRequiredStoryItem(
        string problem,
        string requiredStoryItem,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return WordProblemUnitEquivalence.ContainsVietnameseUnit(
                problem,
                requiredStoryItem);
        }

        return problem.Contains(
            requiredStoryItem,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreAnswerUnitsEquivalent(
        string actualUnit,
        string expectedUnit,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return WordProblemUnitEquivalence
                .AreVietnameseUnitsEquivalent(
                    actualUnit,
                    expectedUnit);
        }

        return string.Equals(
            actualUnit,
            expectedUnit,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool SolutionLeadMentionsAnswerUnit(
        string solutionLead,
        string answerUnit,
        AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
        {
            return WordProblemUnitEquivalence.ContainsVietnameseUnit(
                solutionLead,
                answerUnit);
        }

        string normalizedLead =
            NormalizeSingleLine(solutionLead)
                .ToLowerInvariant();
        string normalizedUnit =
            NormalizeSingleLine(answerUnit)
                .ToLowerInvariant();

        return normalizedUnit.Length > 0 &&
               $" {normalizedLead} ".Contains(
                   $" {normalizedUnit} ",
                   StringComparison.Ordinal);
    }

    private static string? BuildSolutionLeadDisclosureFeedback(
        string solutionLead,
        BigInteger correctAnswer,
        AppLanguage language)
    {
        if (string.IsNullOrWhiteSpace(solutionLead))
        {
            return null;
        }

        string[] numberTokens =
            NumberRegex()
                .Matches(solutionLead)
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        string[] operatorTokens =
            SolutionLeadForbiddenOperatorRegex()
                .Matches(solutionLead)
                .Select(match => match.Value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        string[] textualDisclosureTokens =
            SolutionLeadTextualAnswerRegex()
                .Matches(solutionLead)
                .Select(match => match.Value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        int colonIndex = solutionLead.IndexOf(':');
        string contentAfterColon =
            colonIndex >= 0
                ? solutionLead[(colonIndex + 1)..].Trim()
                : string.Empty;

        if (numberTokens.Length == 0 &&
            operatorTokens.Length == 0 &&
            textualDisclosureTokens.Length == 0 &&
            contentAfterColon.Length == 0)
        {
            return null;
        }

        bool revealsCorrectAnswer =
            numberTokens.Any(value =>
                BigInteger.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger parsed) &&
                parsed == correctAnswer);
        string numberDetail =
            numberTokens.Length == 0
                ? language == AppLanguage.Vietnamese
                    ? "không có số"
                    : "no numeric token"
                : FormatQuotedList(numberTokens);
        string operatorDetail =
            operatorTokens.Length == 0
                ? language == AppLanguage.Vietnamese
                    ? "không có ký hiệu phép tính"
                    : "no calculation symbol"
                : FormatQuotedList(operatorTokens);
        var disclosureParts =
            new List<string>();

        if (textualDisclosureTokens.Length > 0)
        {
            disclosureParts.Add(
                FormatQuotedList(textualDisclosureTokens));
        }

        if (contentAfterColon.Length > 0)
        {
            disclosureParts.Add(
                language == AppLanguage.Vietnamese
                    ? $"nội dung đứng sau dấu hai chấm “{contentAfterColon}”"
                    : $"content after the colon “{contentAfterColon}”");
        }

        string textualDetail =
            disclosureParts.Count == 0
                ? language == AppLanguage.Vietnamese
                    ? "không có cụm từ tiết lộ bằng chữ"
                    : "no textual disclosure"
                : string.Join(
                    language == AppLanguage.Vietnamese
                        ? " và "
                        : " and ",
                    disclosureParts);

        return language == AppLanguage.Vietnamese
            ? $"Trường solution_lead đang là “{solutionLead}”. Validator phát hiện {numberDetail}, {operatorDetail} và {textualDetail}; " +
              (revealsCorrectAnswer
                  ? $"trong đó đã lộ trực tiếp đáp án {correctAnswer}. "
                  : "những nội dung này đã biến câu dẫn thành một phần của phép giải. ") +
              "Đây là lỗi cấm: solution_lead chỉ được nêu đại lượng cần tìm và kết thúc bằng dấu hai chấm, tuyệt đối không chứa số, phép tính, dấu bằng, kết quả hoặc đáp án. Ví dụ đúng: “Số quả chôm chôm mẹ còn lại là:”."
            : $"solution_lead is “{solutionLead}”. The validator found {numberDetail}, {operatorDetail}, and {textualDetail}; " +
              (revealsCorrectAnswer
                  ? $"it directly reveals the answer {correctAnswer}. "
                  : "this turns the lead-in sentence into part of the calculation. ") +
              "This is forbidden: solution_lead may only name the requested quantity and end with a colon. It must contain no number, calculation, equals sign, result, or answer.";
    }

    public LlmWordProblemValidationResult ValidateGeometry(
        LlmWordProblemDraft draft,
        GeometryQuizContract contract,
        AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(contract);

        string problem = NormalizeSingleLine(draft.ProblemText);
        string subject = NormalizeSingleLine(draft.SubjectName);
        string solutionLead = NormalizeSingleLine(draft.SolutionLead);

        if (problem.Length is < 20 or > 700 ||
            subject.Length > 100 ||
            solutionLead.Length > 240)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidTextLength",
                BuildTextLengthFeedback(
                    problem.Length,
                    20,
                    700,
                    subject.Length,
                    100,
                    0,
                    0,
                    solutionLead.Length,
                    240,
                    language));
        }

        problem = NormalizeClassLabels(
            problem,
            out string[] invalidClassLabels);

        if (invalidClassLabels.Length > 0)
        {
            return LlmWordProblemValidationResult.Invalid(
                "InvalidClassLabel",
                BuildClassLabelFeedback(
                    invalidClassLabels,
                    language));
        }

        if (!problem.Contains('?'))
        {
            problem = problem.TrimEnd('.', '!', ';', ':') + "?";
        }

        string problemWithoutClassLabels =
            ClassLabelRegex().Replace(problem, " ");

        int[] actualNumbers =
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

        int[] expectedNumbers =
            contract.Dimensions.Values
                .Select(value =>
                    value >= int.MinValue && value <= int.MaxValue
                        ? (int)value
                        : int.MinValue)
                .Order()
                .ToArray();

        if (!actualNumbers.Order().SequenceEqual(expectedNumbers))
        {
            string dimensionFacts = string.Join(
                ", ",
                contract.Dimensions.Select(pair =>
                    $"{pair.Key}={pair.Value} {contract.LengthUnitSymbol}"));

            return LlmWordProblemValidationResult.Invalid(
                "ProblemNumbersMismatch",
                BuildNumberMismatchFeedback(
                    expectedNumbers,
                    actualNumbers,
                    language,
                    language == AppLanguage.Vietnamese
                        ? $"các kích thước bắt buộc: {dimensionFacts}"
                        : $"the required dimensions: {dimensionFacts}"));
        }

        string lowerProblem = problem.ToLowerInvariant();

        if (!lowerProblem.Contains(
                contract.ShapeName.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            return LlmWordProblemValidationResult.Invalid(
                "GeometryShapeMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trong trường problem_text không tìm thấy tên hình bắt buộc “{contract.ShapeName}”. Đề phải nói rõ vật “{contract.ObjectName}” có dạng {contract.ShapeName}; không được đổi sang hình khác."
                    : $"The problem_text field does not contain the required shape “{contract.ShapeName}”. State clearly that “{contract.ObjectName}” has that shape; do not replace it with another shape.");
        }

        string requiredMeasurement =
            GetGeometryMeasurementPhrase(
                contract.Measurement,
                language);

        if (!HasExpectedGeometryMeasurement(
                lowerProblem,
                contract.Measurement,
                language))
        {
            string[] conflictingMeasurements =
                FindConflictingGeometryMeasurements(
                    lowerProblem,
                    contract.Measurement,
                    language);
            string conflictDetail =
                conflictingMeasurements.Length == 0
                    ? language == AppLanguage.Vietnamese
                        ? "Đề chưa nêu đại lượng cần tìm."
                        : "The requested measurement is missing."
                    : language == AppLanguage.Vietnamese
                        ? $"Đề lại dùng {FormatQuotedList(conflictingMeasurements)}, nên suy ra một công thức khác."
                        : $"The story instead uses {FormatQuotedList(conflictingMeasurements)}, which implies a different formula.";

            return LlmWordProblemValidationResult.Invalid(
                "GeometryMeasurementMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trong trường problem_text, contract yêu cầu hỏi “{requiredMeasurement}”. {conflictDetail} Hãy bỏ cụm mâu thuẫn và hỏi đúng “{requiredMeasurement}”."
                    : $"In problem_text, the contract requires “{requiredMeasurement}”. {conflictDetail} Remove the conflicting phrase and ask for “{requiredMeasurement}”.");
        }

        if (!lowerProblem.Contains(
                contract.ObjectName.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            return LlmWordProblemValidationResult.Invalid(
                "GeometryObjectMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trong trường problem_text không có đúng đồ vật bắt buộc “{contract.ObjectName}”. Hãy dùng nguyên tên đồ vật này và gắn các kích thước cho nó, không thay bằng đồ vật khác."
                    : $"The problem_text field does not contain the required object “{contract.ObjectName}”. Use this exact object and attach the dimensions to it; do not substitute another object.");
        }

        string unitPattern =
            $@"(?<![\p{{L}}]){Regex.Escape(contract.LengthUnitSymbol)}(?![\p{{L}}²³\d])";

        int actualUnitCount = Regex.Matches(
                problem,
                unitPattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant).Count;

        if (actualUnitCount < contract.Dimensions.Count)
        {
            return LlmWordProblemValidationResult.Invalid(
                "GeometryUnitMismatch",
                language == AppLanguage.Vietnamese
                    ? $"Trong trường problem_text, đơn vị “{contract.LengthUnitSymbol}” chỉ xuất hiện {actualUnitCount} lần nhưng có {contract.Dimensions.Count} kích thước. Hãy ghi “{contract.LengthUnitSymbol}” ngay sau từng dữ kiện kích thước."
                    : $"In problem_text, the unit “{contract.LengthUnitSymbol}” appears {actualUnitCount} time(s), but there are {contract.Dimensions.Count} dimensions. Put “{contract.LengthUnitSymbol}” immediately after every dimension value.");
        }

        if (string.IsNullOrWhiteSpace(subject) ||
            !lowerProblem.Contains(
                subject.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            subject = contract.ObjectName;
        }

        string? solutionLeadDisclosureFeedback =
            BuildSolutionLeadDisclosureFeedback(
                solutionLead,
                contract.CorrectAnswer,
                language);

        if (!string.IsNullOrWhiteSpace(
                solutionLeadDisclosureFeedback))
        {
            return LlmWordProblemValidationResult.Invalid(
                "SolutionLeadRevealsAnswer",
                solutionLeadDisclosureFeedback);
        }

        solutionLead =
            ElementaryWordProblemSolutionFormatter
                .NormalizeSolutionLeadPunctuation(solutionLead);

        if (string.IsNullOrWhiteSpace(solutionLead) ||
            IsGenericSolutionLead(solutionLead, language))
        {
            string measurement = GetGeometryMeasurementPhrase(
                contract.Measurement,
                language);

            solutionLead = language == AppLanguage.Vietnamese
                ? $"{char.ToUpperInvariant(measurement[0])}{measurement[1..]} của {contract.ObjectName} là:"
                : $"The {measurement} of {contract.ObjectName} is:";
        }

        return new(
            true,
            null,
            null,
            new MathWordProblem(
                problem,
                solutionLead,
                contract.AnswerUnit,
                subject));
    }

    private static string GetGeometryMeasurementPhrase(
        GeometryMeasurement measurement,
        AppLanguage language) =>
        (measurement, language) switch
        {
            (GeometryMeasurement.Perimeter, AppLanguage.Vietnamese) => "chu vi",
            (GeometryMeasurement.Area, AppLanguage.Vietnamese) => "diện tích",
            (GeometryMeasurement.TotalArea, AppLanguage.Vietnamese) => "diện tích toàn phần",
            (GeometryMeasurement.Volume, AppLanguage.Vietnamese) => "thể tích",
            (GeometryMeasurement.Perimeter, _) => "perimeter",
            (GeometryMeasurement.Area, _) => "area",
            (GeometryMeasurement.TotalArea, _) => "total surface area",
            (GeometryMeasurement.Volume, _) => "volume",
            _ => throw new ArgumentOutOfRangeException(nameof(measurement))
        };

    private static bool HasExpectedGeometryMeasurement(
        string problem,
        GeometryMeasurement expectedMeasurement,
        AppLanguage language)
    {
        string expected =
            GetGeometryMeasurementPhrase(
                expectedMeasurement,
                language);

        if (!problem.Contains(expected, StringComparison.Ordinal))
        {
            return false;
        }

        // “Diện tích”/“area” là chuỗi con của “diện tích toàn phần”/
        // “total surface area”. Khi contract chỉ yêu cầu diện tích phẳng,
        // không được coi cụm đại lượng 3D dài hơn là khớp.
        return expectedMeasurement != GeometryMeasurement.Area ||
               !problem.Contains(
                   GetGeometryMeasurementPhrase(
                       GeometryMeasurement.TotalArea,
                       language),
                   StringComparison.Ordinal);
    }

    private static string[] FindConflictingGeometryMeasurements(
        string problem,
        GeometryMeasurement expectedMeasurement,
        AppLanguage language) =>
        new[]
        {
            GeometryMeasurement.Perimeter,
            GeometryMeasurement.Area,
            GeometryMeasurement.TotalArea,
            GeometryMeasurement.Volume
        }
            .Where(measurement => measurement != expectedMeasurement)
            .Select(measurement =>
                GetGeometryMeasurementPhrase(
                    measurement,
                    language))
            .OrderByDescending(phrase => phrase.Length)
            .Where(phrase =>
                problem.Contains(
                    phrase,
                    StringComparison.Ordinal))
            .Aggregate(
                new List<string>(),
                (found, phrase) =>
                {
                    if (!found.Any(existing =>
                            existing.Contains(
                                phrase,
                                StringComparison.Ordinal)))
                    {
                        found.Add(phrase);
                    }

                    return found;
                })
            .ToArray();

    private static string BuildTextLengthFeedback(
        int problemLength,
        int minimumProblemLength,
        int maximumProblemLength,
        int subjectLength,
        int maximumSubjectLength,
        int unitLength,
        int maximumUnitLength,
        int solutionLeadLength,
        int maximumSolutionLeadLength,
        AppLanguage language)
    {
        var issues = new List<string>();

        if (problemLength < minimumProblemLength ||
            problemLength > maximumProblemLength)
        {
            issues.Add(
                language == AppLanguage.Vietnamese
                    ? $"problem_text dài {problemLength} ký tự, yêu cầu {minimumProblemLength}–{maximumProblemLength}"
                    : $"problem_text has {problemLength} characters; allowed range is {minimumProblemLength}–{maximumProblemLength}");
        }

        if (subjectLength > maximumSubjectLength)
        {
            issues.Add(
                language == AppLanguage.Vietnamese
                    ? $"subject_name dài {subjectLength} ký tự, tối đa {maximumSubjectLength}"
                    : $"subject_name has {subjectLength} characters; maximum is {maximumSubjectLength}");
        }

        if (maximumUnitLength > 0 &&
            unitLength > maximumUnitLength)
        {
            issues.Add(
                language == AppLanguage.Vietnamese
                    ? $"answer_unit dài {unitLength} ký tự, tối đa {maximumUnitLength}"
                    : $"answer_unit has {unitLength} characters; maximum is {maximumUnitLength}");
        }

        if (solutionLeadLength > maximumSolutionLeadLength)
        {
            issues.Add(
                language == AppLanguage.Vietnamese
                    ? $"solution_lead dài {solutionLeadLength} ký tự, tối đa {maximumSolutionLeadLength}"
                    : $"solution_lead has {solutionLeadLength} characters; maximum is {maximumSolutionLeadLength}");
        }

        return language == AppLanguage.Vietnamese
            ? $"Độ dài JSON không hợp lệ tại: {string.Join("; ", issues)}. Hãy rút gọn hoặc bổ sung đúng trường được nêu."
            : $"Invalid JSON field length: {string.Join("; ", issues)}. Shorten or complete the named field.";
    }

    private static string BuildClassLabelFeedback(
        IReadOnlyList<string> invalidLabels,
        AppLanguage language)
    {
        string labels = FormatQuotedList(invalidLabels);

        return language == AppLanguage.Vietnamese
            ? $"Trong trường problem_text có nhãn lớp không hợp lệ: {labels}. Chỉ được dùng khối 1–5 và lớp con 1–9 hoặc A–I, ví dụ “lớp 3/1” hoặc “lớp 3A”; hãy thay đúng ngay tại cụm này."
            : $"The problem_text field contains invalid class label(s): {labels}. Use only grades 1–5 and sections 1–9 or A–I, such as “Class 3/1” or “Class 3A”; replace the named phrase.";
    }

    private static string BuildNumberMismatchFeedback(
        IReadOnlyList<int> expectedNumbers,
        IReadOnlyList<int> actualNumbers,
        AppLanguage language,
        string expectedDescription)
    {
        int[] missing =
            FindMultisetDifference(
                expectedNumbers,
                actualNumbers);
        int[] unexpected =
            FindMultisetDifference(
                actualNumbers,
                expectedNumbers);
        string missingText = FormatNumberList(missing, language);
        string unexpectedText = FormatNumberList(unexpected, language);

        return language == AppLanguage.Vietnamese
            ? $"Sai dữ kiện số trong trường problem_text. Contract yêu cầu {expectedDescription}, tức danh sách {FormatNumberList(expectedNumbers, language)}, nhưng validator đọc được {FormatNumberList(actualNumbers, language)}. Số bị thiếu: {missingText}. Số sai hoặc thừa: {unexpectedText}. Hãy sửa đúng các số này, giữ đủ số lần xuất hiện và không thêm dữ kiện số mới."
            : $"Numeric facts are wrong in problem_text. The contract requires {expectedDescription}, i.e. {FormatNumberList(expectedNumbers, language)}, but the validator found {FormatNumberList(actualNumbers, language)}. Missing value(s): {missingText}. Wrong or extra value(s): {unexpectedText}. Correct those exact values, preserve duplicate counts, and add no new numeric fact.";
    }

    private static int[] FindMultisetDifference(
        IReadOnlyList<int> source,
        IReadOnlyList<int> valuesToRemove)
    {
        var remaining = valuesToRemove.ToList();
        var difference = new List<int>();

        foreach (int value in source)
        {
            int index = remaining.IndexOf(value);

            if (index >= 0)
            {
                remaining.RemoveAt(index);
            }
            else
            {
                difference.Add(value);
            }
        }

        return difference.ToArray();
    }

    private static string FormatNumberList(
        IReadOnlyList<int> values,
        AppLanguage language) =>
        values.Count == 0
            ? language == AppLanguage.Vietnamese
                ? "(không có)"
                : "(none)"
            : $"[{string.Join(", ", values)}]";

    private static string FormatQuotedList(
        IReadOnlyList<string> values) =>
        string.Join(
            ", ",
            values.Select(value => $"“{value}”"));

    private static string? BuildOperationConflictFeedback(
        string problem,
        ArithmeticOperation expectedOperation,
        int left,
        int right,
        AppLanguage language)
    {
        var conflicts =
            Enum.GetValues<ArithmeticOperation>()
                .Where(operation => operation != expectedOperation)
                .Where(operation =>
                    HasUnambiguousOperationMeaning(
                        problem,
                        operation,
                        language))
                .Select(operation =>
                    new
                    {
                        Operation = operation,
                        Phrases = FindMatchedOperationPhrases(
                            problem,
                            operation,
                            language)
                    })
                .ToArray();

        if (conflicts.Length == 0)
        {
            return null;
        }

        string conflictDetail = string.Join(
            "; ",
            conflicts.Select(conflict =>
                language == AppLanguage.Vietnamese
                    ? $"{FormatQuotedList(conflict.Phrases)} gợi {GetOperationDisplayName(conflict.Operation, language)}"
                    : $"{FormatQuotedList(conflict.Phrases)} implies {GetOperationDisplayName(conflict.Operation, language)}"));
        string expectedName =
            GetOperationDisplayName(
                expectedOperation,
                language);
        string suggestions =
            FormatQuotedList(
                GetPreferredOperationPhrases(
                    expectedOperation,
                    language));

        return language == AppLanguage.Vietnamese
            ? $"Mâu thuẫn cách dùng từ trong trường problem_text. Contract yêu cầu {expectedName} ({left} {GetOperationSymbol(expectedOperation)} {right}), nhưng {conflictDetail}. Vì các cụm này khiến người đọc suy ra sai phép toán, hãy bỏ hoặc thay chúng bằng cách diễn đạt rõ {expectedName}, chẳng hạn {suggestions}."
            : $"Conflicting wording in problem_text. The contract requires {expectedName} ({left} {GetOperationSymbol(expectedOperation)} {right}), but {conflictDetail}. These phrases make the reader infer the wrong operation; remove or replace them with clear {expectedName} wording such as {suggestions}.";
    }

    private static string? BuildFindXOperationConflictFeedback(
        string problem,
        FindXQuizContract contract,
        AppLanguage language)
    {
        var conflicts =
            Enum.GetValues<ArithmeticOperation>()
                .Where(operation => operation != contract.Operation)
                .Where(operation =>
                    HasUnambiguousOperationMeaning(
                        problem,
                        operation,
                        language))
                .Select(operation =>
                    new
                    {
                        Operation = operation,
                        Phrases = FindMatchedOperationPhrases(
                            problem,
                            operation,
                            language)
                    })
                .ToArray();

        if (conflicts.Length == 0)
        {
            return null;
        }

        string conflictDetail = string.Join(
            "; ",
            conflicts.Select(conflict =>
                language == AppLanguage.Vietnamese
                    ? $"{FormatQuotedList(conflict.Phrases)} gợi {GetOperationDisplayName(conflict.Operation, language)}"
                    : $"{FormatQuotedList(conflict.Phrases)} implies {GetOperationDisplayName(conflict.Operation, language)}"));
        string expectedName =
            GetOperationDisplayName(
                contract.Operation,
                language);
        string suggestions =
            FormatQuotedList(
                GetPreferredOperationPhrases(
                    contract.Operation,
                    language));

        return language == AppLanguage.Vietnamese
            ? $"Mâu thuẫn cách dùng từ trong problem_text. Contract Tìm x bắt buộc là “{contract.EquationText}” ({expectedName}), nhưng {conflictDetail}. Các cụm này làm học sinh đặt sai phương trình; hãy bỏ chúng và diễn đạt đúng vai trò số ban đầu, số thêm/bớt, số nhóm hoặc số mỗi nhóm, chẳng hạn {suggestions}."
            : $"Conflicting wording in problem_text. The Find-x contract is “{contract.EquationText}” ({expectedName}), but {conflictDetail}. These phrases make a student form the wrong equation; remove them and state the correct roles of the initial amount, change, group count, or items per group, using wording such as {suggestions}.";
    }

    private static string? BuildFindXRoleMismatchFeedback(
        string problem,
        FindXQuizContract contract,
        AppLanguage language)
    {
        int known = (int)contract.KnownValue;
        int result = (int)contract.ResultValue;
        bool vi = language == AppLanguage.Vietnamese;

        string[] initial = vi
            ? ["ban đầu", "lúc đầu", "đang có", "có"]
            : ["initially", "at first", "started with", "has", "had"];
        string[] added = vi
            ? ["thêm", "nhận", "được cho", "mua thêm", "có thêm"]
            : ["plus", "added", "received", "more", "bought", "joined"];
        string[] removed = vi
            ? ["bớt", "cho", "tặng", "lấy đi", "lấy ra", "bỏ đi", "dùng", "ăn", "bán"]
            : ["remove", "removed", "minus", "gave", "took away", "used", "ate", "sold", "left"];
        string[] remaining = vi
            ? ["còn", "còn lại"]
            : ["remain", "remaining", "leaving", "leaves", "left"];
        string[] total = vi
            ? ["tổng cộng", "tất cả", "có tất cả"]
            : ["in total", "altogether", "in all", "total"];
        string[] groups = vi
            ? ["nhóm", "phần"]
            : ["groups", "group", "parts"];
        string[] perGroup = vi
            ? ["mỗi nhóm", "mỗi phần", "mỗi"]
            : ["each group", "per group", "each"];

        bool valid =
            (contract.Operation, contract.UnknownIsLeftOperand) switch
            {
                (ArithmeticOperation.Add, true) =>
                    HasPhraseBeforeNumber(problem, known, added) &&
                    HasPhraseBeforeNumber(problem, result, total),
                (ArithmeticOperation.Add, false) =>
                    HasPhraseBeforeNumber(problem, known, initial) &&
                    HasPhraseBeforeNumber(problem, result, total),
                (ArithmeticOperation.Subtract, true) =>
                    HasPhraseBeforeNumber(problem, known, removed) &&
                    HasPhraseBeforeNumber(problem, result, remaining),
                (ArithmeticOperation.Subtract, false) =>
                    HasPhraseBeforeNumber(problem, known, initial) &&
                    HasPhraseBeforeNumber(problem, result, remaining),
                (ArithmeticOperation.Multiply, true) =>
                    HasPhraseBeforeNumber(problem, known, perGroup) &&
                    HasPhraseBeforeNumber(problem, result, total),
                (ArithmeticOperation.Multiply, false) =>
                    HasNumberBeforePhrase(problem, known, groups) &&
                    HasPhraseBeforeNumber(problem, result, total),
                (ArithmeticOperation.Divide, true) =>
                    HasNumberBeforePhrase(problem, known, groups) &&
                    HasPhraseBeforeNumber(problem, result, perGroup),
                (ArithmeticOperation.Divide, false) =>
                    HasPhraseBeforeNumber(problem, known, total) &&
                    HasPhraseBeforeNumber(problem, result, perGroup),
                _ => false
            };

        if (valid)
        {
            return null;
        }

        string relationship =
            (contract.Operation, contract.UnknownIsLeftOperand, vi) switch
            {
                (ArithmeticOperation.Add, true, true) => $"{known} là số thêm vào và {result} là tổng sau khi thêm",
                (ArithmeticOperation.Add, false, true) => $"{known} là số ban đầu và {result} là tổng sau khi thêm",
                (ArithmeticOperation.Subtract, true, true) => $"{known} là số bị bớt đi và {result} là số còn lại",
                (ArithmeticOperation.Subtract, false, true) => $"{known} là số ban đầu và {result} là số còn lại",
                (ArithmeticOperation.Multiply, true, true) => $"{known} là số đồ vật mỗi nhóm và {result} là tổng số đồ vật",
                (ArithmeticOperation.Multiply, false, true) => $"{known} là số nhóm và {result} là tổng số đồ vật",
                (ArithmeticOperation.Divide, true, true) => $"{known} là số nhóm và {result} là số đồ vật mỗi nhóm",
                (ArithmeticOperation.Divide, false, true) => $"{known} là tổng số đồ vật và {result} là số đồ vật mỗi nhóm",
                (ArithmeticOperation.Add, true, false) => $"{known} is the added amount and {result} is the total after adding",
                (ArithmeticOperation.Add, false, false) => $"{known} is the initial amount and {result} is the total after adding",
                (ArithmeticOperation.Subtract, true, false) => $"{known} is removed and {result} is the amount remaining",
                (ArithmeticOperation.Subtract, false, false) => $"{known} is the initial amount and {result} is the amount remaining",
                (ArithmeticOperation.Multiply, true, false) => $"{known} is the item count per group and {result} is the total item count",
                (ArithmeticOperation.Multiply, false, false) => $"{known} is the number of groups and {result} is the total item count",
                (ArithmeticOperation.Divide, true, false) => $"{known} is the number of groups and {result} is the item count per group",
                (ArithmeticOperation.Divide, false, false) => $"{known} is the total item count and {result} is the item count per group",
                _ => contract.EquationText
            };

        return vi
            ? $"Dữ kiện đúng số nhưng sai vai trò trong problem_text. Phương trình bắt buộc là “{contract.EquationText}”: {relationship}. Cách đặt từ hiện tại không gắn rõ các số với đúng vai trò nên có thể khiến học sinh lập phương trình ngược. Hãy viết lại đúng quan hệ này và không đổi số."
            : $"The numeric values are present, but their roles are wrong or unclear in problem_text. The required equation is “{contract.EquationText}”: {relationship}. The current wording can make a student form the inverse equation. Rewrite the relationship clearly without changing any number.";
    }

    private static bool HasPhraseBeforeNumber(
        string text,
        int number,
        IReadOnlyList<string> phrases)
    {
        MatchCollection matches = Regex.Matches(
            text,
            $@"(?<!\d){Regex.Escape(number.ToString(CultureInfo.InvariantCulture))}(?!\d)",
            RegexOptions.CultureInvariant);

        foreach (Match match in matches)
        {
            int start = Math.Max(0, match.Index - 38);
            string window = text[start..match.Index];

            if (phrases.Any(phrase =>
                    ContainsKeyword(window, phrase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNumberBeforePhrase(
        string text,
        int number,
        IReadOnlyList<string> phrases)
    {
        MatchCollection matches = Regex.Matches(
            text,
            $@"(?<!\d){Regex.Escape(number.ToString(CultureInfo.InvariantCulture))}(?!\d)",
            RegexOptions.CultureInvariant);

        foreach (Match match in matches)
        {
            int end = Math.Min(
                text.Length,
                match.Index + match.Length + 38);
            string window = text[(match.Index + match.Length)..end];

            if (phrases.Any(phrase =>
                    ContainsKeyword(window, phrase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] FindMatchedOperationPhrases(
        string problem,
        ArithmeticOperation operation,
        AppLanguage language) =>
        GetOperationKeywordCandidates(
                operation,
                language)
            .Where(phrase =>
                ContainsKeyword(
                    problem,
                    phrase))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(phrase => phrase.Length)
            .Aggregate(
                new List<string>(),
                (found, phrase) =>
                {
                    if (!found.Any(existing =>
                            existing.Contains(
                                phrase,
                                StringComparison.Ordinal)))
                    {
                        found.Add(phrase);
                    }

                    return found;
                })
            .ToArray();

    private static string[] GetOperationKeywordCandidates(
        ArithmeticOperation operation,
        AppLanguage language) =>
        (operation, language) switch
        {
            (ArithmeticOperation.Add, AppLanguage.Vietnamese) =>
                ["mua thêm", "hái thêm", "nhặt thêm", "có thêm", "được cho", "tổng cộng", "tất cả", "có bao nhiêu", "thêm", "nhận", "gộp", "và"],
            (ArithmeticOperation.Subtract, AppLanguage.Vietnamese) =>
                ["lấy đi", "lấy ra", "bỏ đi", "rời đi", "bay đi", "còn bao nhiêu", "còn lại", "tặng", "bớt", "mất", "dùng", "ăn", "bán", "cho"],
            (ArithmeticOperation.Multiply, AppLanguage.Vietnamese) =>
                ["tổng cộng", "có bao nhiêu", "tất cả", "đều có", "mỗi", "từng"],
            (ArithmeticOperation.Divide, AppLanguage.Vietnamese) =>
                ["chia đều", "đều cho", "xếp đều", "phân đều", "chia thành", "mỗi nhóm", "mỗi phần", "một nhóm", "một phần", "chia", "mỗi"],
            (ArithmeticOperation.Add, _) =>
                ["in total", "how many", "altogether", "received", "added", "bought", "found", "joined", "more", "and"],
            (ArithmeticOperation.Subtract, _) =>
                ["took away", "remain", "removed", "gave", "used", "lost", "ate", "sold", "left"],
            (ArithmeticOperation.Multiply, _) =>
                ["how many", "altogether", "in all", "each", "every"],
            (ArithmeticOperation.Divide, _) =>
                ["placed equally", "each group", "per group", "shared", "divided", "split", "each"],
            _ => []
        };

    private static string[] GetPreferredOperationPhrases(
        ArithmeticOperation operation,
        AppLanguage language) =>
        (operation, language) switch
        {
            (ArithmeticOperation.Add, AppLanguage.Vietnamese) =>
                ["thêm", "tổng cộng"],
            (ArithmeticOperation.Subtract, AppLanguage.Vietnamese) =>
                ["bớt đi", "còn lại"],
            (ArithmeticOperation.Multiply, AppLanguage.Vietnamese) =>
                ["mỗi nhóm đều có", "tất cả"],
            (ArithmeticOperation.Divide, AppLanguage.Vietnamese) =>
                ["chia đều", "mỗi nhóm"],
            (ArithmeticOperation.Add, _) =>
                ["added", "in total"],
            (ArithmeticOperation.Subtract, _) =>
                ["took away", "left"],
            (ArithmeticOperation.Multiply, _) =>
                ["each group has", "in all"],
            (ArithmeticOperation.Divide, _) =>
                ["shared equally", "each group"],
            _ => []
        };

    private static string GetOperationDisplayName(
        ArithmeticOperation operation,
        AppLanguage language) =>
        (operation, language) switch
        {
            (ArithmeticOperation.Add, AppLanguage.Vietnamese) => "phép cộng",
            (ArithmeticOperation.Subtract, AppLanguage.Vietnamese) => "phép trừ",
            (ArithmeticOperation.Multiply, AppLanguage.Vietnamese) => "phép nhân",
            (ArithmeticOperation.Divide, AppLanguage.Vietnamese) => "phép chia",
            (ArithmeticOperation.Add, _) => "addition",
            (ArithmeticOperation.Subtract, _) => "subtraction",
            (ArithmeticOperation.Multiply, _) => "multiplication",
            (ArithmeticOperation.Divide, _) => "division",
            _ => operation.ToString()
        };

    private static string GetOperationSymbol(
        ArithmeticOperation operation) =>
        operation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "−",
            ArithmeticOperation.Multiply => "×",
            ArithmeticOperation.Divide => "÷",
            _ => "?"
        };

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
                ContainsAny(problem, "gave", "used", "lost", "took away", "ate", "sold", "left", "remove", "removed") &&
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
            ContainsKeyword(
                value,
                candidate));

    private static bool ContainsKeyword(
        string value,
        string candidate)
    {
        int searchIndex = 0;

        while (searchIndex < value.Length)
        {
            int matchIndex = value.IndexOf(
                candidate,
                searchIndex,
                StringComparison.Ordinal);

            if (matchIndex < 0)
            {
                return false;
            }

            int endIndex = matchIndex + candidate.Length;
            bool startsAtBoundary =
                matchIndex == 0 ||
                !char.IsLetterOrDigit(value[matchIndex - 1]);
            bool endsAtBoundary =
                endIndex == value.Length ||
                !char.IsLetterOrDigit(value[endIndex]);

            if (startsAtBoundary && endsAtBoundary)
            {
                return true;
            }

            searchIndex = matchIndex + 1;
        }

        return false;
    }

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

    private static bool IsGenericSolutionLead(
        string solutionLead,
        AppLanguage language)
    {
        string lower =
            solutionLead
                .Trim()
                .TrimEnd('.', ',', '!', '?', ':', ';')
                .ToLowerInvariant();

        return language == AppLanguage.Vietnamese
            ? lower.StartsWith("ta thực hiện phép", StringComparison.Ordinal) ||
              lower.StartsWith("thực hiện phép", StringComparison.Ordinal) ||
              lower.StartsWith("ta làm phép", StringComparison.Ordinal) ||
              lower.StartsWith("ta tính", StringComparison.Ordinal) ||
              lower.StartsWith("phép tính cần thực hiện", StringComparison.Ordinal)
            : lower.StartsWith("we perform", StringComparison.Ordinal) ||
              lower.StartsWith("perform the", StringComparison.Ordinal) ||
              lower.StartsWith("use multiplication", StringComparison.Ordinal) ||
              lower.StartsWith("use addition", StringComparison.Ordinal) ||
              lower.StartsWith("use subtraction", StringComparison.Ordinal) ||
              lower.StartsWith("use division", StringComparison.Ordinal);
    }

    private static string BuildQuestionBasedSolutionLead(
        string problem,
        ArithmeticOperation operation,
        string unit,
        AppLanguage language)
    {
        if (language != AppLanguage.Vietnamese)
        {
            return BuildDefaultSolutionLead(
                operation,
                unit,
                language);
        }

        string[] clauses =
            problem.Split(
                ['.', '!', '?'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        for (int index = clauses.Length - 1;
             index >= 0;
             index--)
        {
            string clause =
                clauses[index].Trim();

            int questionCueIndex =
                Math.Max(
                    clause.LastIndexOf(
                        "vậy ",
                        StringComparison.OrdinalIgnoreCase),
                    clause.LastIndexOf(
                        "hỏi ",
                        StringComparison.OrdinalIgnoreCase));

            if (questionCueIndex > 0)
            {
                clause =
                    clause[questionCueIndex..];
            }

            clause = Regex.Replace(
                clause,
                @"^(?:vậy|hỏi)\s*[:,]?\s*",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

            int quantityIndex =
                clause.IndexOf(
                    "bao nhiêu",
                    StringComparison.OrdinalIgnoreCase);

            if (quantityIndex <= 0)
            {
                continue;
            }

            string subjectPhrase =
                clause[..quantityIndex]
                    .Trim(' ', ',', ':', ';');

            string unitPhrase =
                clause[(quantityIndex + "bao nhiêu".Length)..]
                    .Trim(' ', ',', '.', '!', '?', ':', ';');

            if (subjectPhrase.Length == 0 ||
                unitPhrase.Length == 0)
            {
                continue;
            }

            subjectPhrase =
                LowercaseLeadingGenericPhrase(
                    subjectPhrase);

            return $"Số {unitPhrase} {subjectPhrase} là:";
        }

        return BuildDefaultSolutionLead(
            operation,
            unit,
            language);
    }

    private static string LowercaseLeadingGenericPhrase(
        string value)
    {
        string[] genericStarts =
        [
            "Mỗi ", "Một ", "Các ", "Những "
        ];

        return genericStarts.Any(prefix =>
                value.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            ? char.ToLowerInvariant(value[0]) + value[1..]
            : value;
    }

    private static string NormalizeSingleLine(
        string? value) =>
        WhitespaceRegex().Replace(
            value?.Trim() ?? string.Empty,
            " ");

    private static string NormalizeClassLabels(
        string problem,
        out string[] invalidClassLabels)
    {
        var invalid = new List<string>();

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

                    invalid.Add(match.Value);
                    return match.Value;
                });

        invalidClassLabels = invalid
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized;
    }

    [GeneratedRegex(@"(?<!\d)-?\d+(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(
        @"=|[+×*÷/]|(?<=\d)\s*[-−]\s*(?=\d)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SolutionLeadForbiddenOperatorRegex();

    [GeneratedRegex(
        @"\b(?:đáp\s*(?:số|án)|answer)\b|\b(?:là|bằng)\s+(?:âm\s+)?(?:không|một|hai|ba|bốn|tư|năm|lăm|sáu|bảy|tám|chín|mười|mươi|trăm|nghìn|triệu|tỷ)\b|\b(?:is|equals?)\s+(?:negative\s+)?(?:zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred|thousand|million|billion|trillion)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SolutionLeadTextualAnswerRegex();

    [GeneratedRegex(
        @"(?<prefix>\b(?:lớp|class)\s*)(?<label>\d+(?:\s*/\s*\d+|[A-Za-z])?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassLabelRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

}
