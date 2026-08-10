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
public sealed record MathWordProblem(
    string ProblemText,
    string SolutionLead,
    string AnswerUnit,
    string SubjectName);

public enum LlmQuizProgressStage
{
    LoadingModel,
    ModelLoaded,
    Generating,
    Validating,
    Retrying,
    DisposingModel
}

public sealed record LlmQuizProgress(
    LlmQuizProgressStage Stage,
    int Attempt,
    int MaximumAttempts,
    string? ProblemPreview = null);

public sealed record LlmQuizGenerationResult(
    ArithmeticQuizQuestion? Question,
    int Attempts,
    string? ErrorCode,
    bool ModelWasLoaded)
{
    public bool IsSuccess =>
        Question is not null;
}
