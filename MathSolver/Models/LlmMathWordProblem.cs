using System.Text.RegularExpressions;

namespace MathSolver.Models;

public enum QuizGenerationSource
{
    Algorithm,
    LocalLlm
}

/// <summary>
/// Phần ngôn ngữ tự nhiên do LLM viết sau khi dữ kiện đã được engine xác nhận.
/// Biểu thức và đáp án vẫn nằm trong ArithmeticQuizQuestion, không lấy từ LLM.
/// </summary>
public sealed partial record MathWordProblem(
    string ProblemText,
    string SolutionLead,
    string AnswerUnit,
    string SubjectName)
{
    public string ProblemText { get; init; } =
        NormalizeVietnameseWording(ProblemText);

    public string SolutionLead { get; init; } =
        NormalizeVietnameseWording(SolutionLead);

    public string AnswerUnit { get; init; } =
        NormalizeVietnameseWording(AnswerUnit);

    public string SubjectName { get; init; } =
        NormalizeVietnameseWording(SubjectName);

    /// <summary>
    /// Biên tập cách gọi đúng nghĩa nhưng chưa tự nhiên mà model nhỏ đôi khi
    /// sinh ra. Bộ chấm vẫn chấp nhận các từ chỉ loại tương đương; quy tắc
    /// này chỉ chuẩn hóa nội dung tiếng Việt hiển thị cho học sinh.
    /// </summary>
    private static string NormalizeVietnameseWording(
        string value) =>
        VietnamesePenClassifierRegex().Replace(
            value ?? string.Empty,
            match =>
                char.IsUpper(match.Value[0])
                    ? "Cây bút"
                    : "cây bút");

    [GeneratedRegex(
        @"\bcái\s+bút\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VietnamesePenClassifierRegex();
}

public enum LlmQuizProgressStage
{
    LoadingModel,
    ModelLoaded,
    Generating,
    Validating,
    Retrying,
    DisposingModel
}

public enum LlmQuizDiagnosticEvent
{
    AttemptStarted,
    JsonReceived,
    ParseSucceeded,
    ParseFailed,
    ValidationSucceeded,
    ValidationFailed,
    RetryScheduled,
    GenerationFailed,
    RuntimeError
}

public sealed record LlmQuizDiagnostic(
    LlmQuizDiagnosticEvent Event,
    int Attempt,
    int MaximumAttempts,
    string? Detail = null,
    int CharacterCount = 0);

public sealed record LlmQuizAttemptReport(
    int Attempt,
    int MaximumAttempts,
    string RawModelOutput,
    IReadOnlyList<LlmQuizDiagnostic> Diagnostics);

public sealed record LlmQuizProgress(
    LlmQuizProgressStage Stage,
    int Attempt,
    int MaximumAttempts,
    string? ProblemPreview = null,
    int GeneratedTokenCount = 0,
    double TokensPerSecond = 0d,
    string? RawModelOutput = null,
    LlmQuizDiagnostic? Diagnostic = null);

public sealed record LlmQuizGenerationResult(
    ArithmeticQuizQuestion? Question,
    int Attempts,
    string? ErrorCode,
    bool ModelWasLoaded,
    int GeneratedTokenCount = 0,
    double TokensPerSecond = 0d,
    IReadOnlyList<LlmQuizAttemptReport>? AttemptReports = null)
{
    public bool IsSuccess =>
        Question is not null;
}
