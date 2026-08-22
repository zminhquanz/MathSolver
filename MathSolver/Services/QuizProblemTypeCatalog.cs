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
            new(QuizProblemKind.FindX)),
        new(
            "Quiz.ProblemProportion",
            new(QuizProblemKind.Proportion)),
        new(
            "Quiz.ProblemMotion",
            new(QuizProblemKind.Motion)),
        new(
            "Quiz.ProblemAverage",
            new(QuizProblemKind.Average)),
        new(
            "Quiz.ProblemPercentage",
            new(QuizProblemKind.Percentage))
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
        new(QuizProblemKind.FindX),
        new(QuizProblemKind.Proportion, ProportionType: ProportionQuizType.Direct),
        new(QuizProblemKind.Proportion, ProportionType: ProportionQuizType.Inverse),
        new(QuizProblemKind.Motion),
        new(QuizProblemKind.Average),
        new(QuizProblemKind.Percentage)
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
        FractionOperation fractionOperation,
        ProportionQuizType proportionType,
        AverageQuizType? averageType,
        PercentageQuizType? percentageType,
        ArithmeticOperation? findXOperation,
        GeometryQuizShape? geometryShape,
        MotionQuizType? motionType)
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
                QuizProblemKind.Proportion =>
                    fixedRequest with
                    {
                        ProportionType = proportionType
                    },
                QuizProblemKind.Average =>
                    fixedRequest with
                    {
                        AverageType = averageType
                    },
                QuizProblemKind.Percentage =>
                    fixedRequest with
                    {
                        PercentageType = percentageType
                    },
                QuizProblemKind.FindX =>
                    fixedRequest with
                    {
                        FindXOperation = findXOperation
                    },
                QuizProblemKind.Geometry =>
                    fixedRequest with
                    {
                        GeometryShape = geometryShape
                    },
                QuizProblemKind.Motion =>
                    fixedRequest with
                    {
                        MotionType = motionType
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
