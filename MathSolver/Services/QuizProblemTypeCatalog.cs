using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>
/// Catalog duy nhất cho nhóm dạng đề và cách phân giải mục Hỗn hợp.
/// Cơ bản và Phân số là nhóm hai tầng; phép tính con được giao vào Resolve.
/// </summary>
public sealed class QuizProblemTypeCatalog
{
    private static readonly QuizProblemOption[] RegisteredOptions =
    [
        new(
            "Quiz.OperationMixed",
            FixedRequest: null),
        new(
            "Quiz.ProblemBasic",
            new(QuizProblemKind.Arithmetic)),
        new(
            "Quiz.ProblemFraction",
            new(QuizProblemKind.Fraction)),
        new(
            "Quiz.ProblemGeometry",
            new(QuizProblemKind.Geometry)),
        new(
            "Quiz.ProblemFindX",
            new(QuizProblemKind.FindX))
    ];

    private static readonly QuizProblemRequest[] MixedRequests =
    [
        new(QuizProblemKind.Arithmetic, ArithmeticOperation.Add),
        new(QuizProblemKind.Arithmetic, ArithmeticOperation.Subtract),
        new(QuizProblemKind.Arithmetic, ArithmeticOperation.Multiply),
        new(QuizProblemKind.Arithmetic, ArithmeticOperation.Divide),
        new(QuizProblemKind.Fraction, FractionOperation: FractionOperation.Add),
        new(QuizProblemKind.Fraction, FractionOperation: FractionOperation.Subtract),
        new(QuizProblemKind.Fraction, FractionOperation: FractionOperation.Multiply),
        new(QuizProblemKind.Fraction, FractionOperation: FractionOperation.Divide),
        new(QuizProblemKind.Geometry),
        new(QuizProblemKind.FindX)
    ];

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
        int selectedIndex,
        ArithmeticOperation basicOperation,
        FractionOperation fractionOperation)
    {
        QuizProblemOption option =
            GetOption(selectedIndex);

        if (option.FixedRequest is
            QuizProblemRequest fixedRequest)
        {
            return fixedRequest.Kind switch
            {
                QuizProblemKind.Arithmetic =>
                    fixedRequest with
                    {
                        ArithmeticOperation = basicOperation
                    },
                QuizProblemKind.Fraction =>
                    fixedRequest with
                    {
                        FractionOperation = fractionOperation
                    },
                _ => fixedRequest
            };
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
