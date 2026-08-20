#if !WINDOWS
using MathSolver.Models;
using MathSolver.Services.Core;

namespace MathSolver.Services;

/// <summary>
/// Compile-time placeholder for non-Windows targets. The current Android UI
/// does not expose AI/LLM and no GGUF model is loaded or accessed here.
/// A future Android LiteRT-LM implementation can replace this platform path
/// without changing the shared quiz contracts/validator.
/// </summary>
public sealed class LocalLlmQuizGenerator
{
    public static int CpuThreadCount => 0;
    public static int CpuBatchThreadCount => 0;
    public const int MaximumAttempts = 3;
    public const int MaximumOutputTokens = 320;
    public const int ModelUnloadGracePeriodSeconds = 60;
    public const uint ContextSize = 2048;

    public LocalLlmQuizGenerator(
        ArithmeticQuizGenerator quizGenerator,
        BasicArithmeticEngine engine,
        FractionQuizGenerator fractionQuizGenerator,
        GeometryQuizGenerator geometryQuizGenerator,
        FindXQuizGenerator findXQuizGenerator,
        ProportionQuizGenerator proportionQuizGenerator,
        MotionQuizGenerator motionQuizGenerator)
    {
        ArgumentNullException.ThrowIfNull(quizGenerator);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(fractionQuizGenerator);
        ArgumentNullException.ThrowIfNull(geometryQuizGenerator);
        ArgumentNullException.ThrowIfNull(findXQuizGenerator);
        ArgumentNullException.ThrowIfNull(proportionQuizGenerator);
        ArgumentNullException.ThrowIfNull(motionQuizGenerator);
    }

    public Task UnloadModelAsync(
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public void CancelScheduledModelUnload()
    {
    }

    public bool IsModelLoaded(string? modelPath) => false;

    public void ScheduleModelUnload(
        Func<Task>? onModelUnloadedAsync = null)
    {
        // No native model exists on non-Windows targets.
    }

    public Task<LlmQuizGenerationResult> GenerateAsync(
        string modelPath,
        ArithmeticQuizMode mode,
        QuizProblemRequest problemRequest,
        AppLanguage language,
        IProgress<LlmQuizProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new LlmQuizGenerationResult(
                Question: null,
                Attempts: 0,
                ErrorCode: "PlatformNotSupported",
                ModelWasLoaded: false));
}
#endif
