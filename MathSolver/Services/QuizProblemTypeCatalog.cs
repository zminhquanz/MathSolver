using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>
/// Catalog duy nhất cho danh sách dạng đề và cách phân giải mục Hỗn hợp.
/// Khi bổ sung dạng đề, đăng ký thêm một QuizProblemOption cụ thể với
/// IncludeInMixed = true để cả nguồn Thuật toán và AI tự dùng chung nó.
/// </summary>
public sealed class QuizProblemTypeCatalog
{
    private static readonly QuizProblemOption[] RegisteredOptions =
    [
        new(
            "Quiz.OperationMixed",
            FixedRequest: null),
        new(
            "Quiz.OperationAddition",
            new(
                QuizProblemKind.Arithmetic,
                ArithmeticOperation.Add),
            IncludeInMixed: true),
        new(
            "Quiz.OperationSubtraction",
            new(
                QuizProblemKind.Arithmetic,
                ArithmeticOperation.Subtract),
            IncludeInMixed: true),
        new(
            "Quiz.OperationMultiplication",
            new(
                QuizProblemKind.Arithmetic,
                ArithmeticOperation.Multiply),
            IncludeInMixed: true),
        new(
            "Quiz.OperationDivision",
            new(
                QuizProblemKind.Arithmetic,
                ArithmeticOperation.Divide),
            IncludeInMixed: true),
        new(
            "Quiz.ProblemGeometry",
            new(
                QuizProblemKind.Geometry),
            IncludeInMixed: true),
        new(
            "Quiz.ProblemFindX",
            new(
                QuizProblemKind.FindX),
            IncludeInMixed: true)
    ];

    private static readonly QuizProblemRequest[] MixedRequests =
        RegisteredOptions
            .Where(option =>
                option.IncludeInMixed &&
                option.FixedRequest.HasValue)
            .Select(option =>
                option.FixedRequest!.Value)
            .ToArray();

    private static readonly IReadOnlyList<QuizProblemOption>
        ReadOnlyOptions =
            Array.AsReadOnly(RegisteredOptions);

    private readonly Random _random;

    public QuizProblemTypeCatalog(
        Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public IReadOnlyList<QuizProblemOption> Options =>
        ReadOnlyOptions;

    public QuizProblemRequest Resolve(
        int selectedIndex)
    {
        QuizProblemOption option =
            GetOption(selectedIndex);

        if (option.FixedRequest is
            QuizProblemRequest fixedRequest)
        {
            return fixedRequest;
        }

        if (MixedRequests.Length == 0)
        {
            throw new InvalidOperationException(
                "No quiz problem type is registered for mixed generation.");
        }

        return MixedRequests[
            _random.Next(MixedRequests.Length)];
    }

    public QuizProblemRequest? GetFixedRequest(
        int selectedIndex) =>
        GetOption(selectedIndex).FixedRequest;

    private static QuizProblemOption GetOption(
        int selectedIndex)
    {
        if ((uint)selectedIndex >=
            (uint)RegisteredOptions.Length)
        {
            return RegisteredOptions[0];
        }

        return RegisteredOptions[selectedIndex];
    }
}
