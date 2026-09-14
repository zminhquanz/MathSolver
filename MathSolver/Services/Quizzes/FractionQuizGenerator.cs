using MathSolver.Models;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh hợp đồng phân số cho cả nguồn Thuật toán và AI. Đáp án luôn được
/// FractionCalculationEngine tính và rút gọn trước khi đưa sang giao diện.
/// </summary>
public sealed class FractionQuizGenerator
{
    private static readonly FractionOperation[] SupportedOperations =
    [
        FractionOperation.Add,
        FractionOperation.Subtract,
        FractionOperation.Multiply,
        FractionOperation.Divide
    ];

    private readonly FractionCalculationEngine _engine;
    private readonly Random _random;

    public FractionQuizGenerator(
        FractionCalculationEngine engine,
        Random? random = null)
    {
        _engine = engine ??
            throw new ArgumentNullException(nameof(engine));
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion Generate(
        ArithmeticQuizMode mode,
        FractionOperation? requestedOperation = null,
        QuizCurriculumContext? curriculumContext = null)
    {
        QuizCurriculumLayer.FractionRules? curriculumRules =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetFractionRules(
                    curriculumContext.Value)
                : null;

        if (curriculumRules is { IsAvailable: false })
        {
            throw new InvalidOperationException(
                "Fractions are not available at the selected curriculum tier.");
        }

        IReadOnlyList<FractionOperation> allowedOperations =
            curriculumRules?.AllowedOperations ??
            SupportedOperations;

        FractionOperation operation =
            requestedOperation.HasValue &&
            allowedOperations.Contains(requestedOperation.Value)
                ? requestedOperation.Value
                : allowedOperations[_random.Next(allowedOperations.Count)];

        ReducedFraction left;
        ReducedFraction right;
        ReducedFraction answer;

        if (curriculumRules?.RequireSameDenominatorForAddSubtract == true &&
            operation is FractionOperation.Add or FractionOperation.Subtract)
        {
            int denominator = CreateDenominator(
                curriculumRules,
                secondaryOperand: false);
            left = CreateOperand(
                curriculumRules,
                secondaryOperand: false,
                denominator);
            right = CreateOperand(
                curriculumRules,
                secondaryOperand: true,
                denominator);
        }
        else
        {
            // Phân số thứ nhất theo đúng bậc chữ số của tier; phân số thứ hai
            // chọn bậc chữ số độc lập từ ★ đến tier hiện tại. Vì vậy ở mức
            // cao vẫn có thể gặp dạng 18.258/24.731 + 2/7 thay vì ép cả hai
            // phân số đều có tử/mẫu rất lớn.
            left = CreateOperand(
                curriculumRules,
                secondaryOperand: false);
            right = CreateOperand(
                curriculumRules,
                secondaryOperand: true);
        }

        if (operation == FractionOperation.Subtract &&
            Compare(left, right) < 0)
        {
            (left, right) = (right, left);
        }

        answer = Calculate(left, right, operation);

        bool? equationIsCorrect = null;
        ReducedFraction? presented = null;
        IReadOnlyList<ReducedFraction> choices = [];

        if (mode == ArithmeticQuizMode.TrueFalse)
        {
            bool showCorrect = _random.Next(2) == 0;
            presented = showCorrect
                ? answer
                : CreateDistractors(answer, 1)[0];
            equationIsCorrect = presented.Value == answer;
        }
        else if (mode == ArithmeticQuizMode.MultipleChoice)
        {
            var mutableChoices = new List<ReducedFraction> { answer };
            mutableChoices.AddRange(CreateDistractors(answer, 3));
            Shuffle(mutableChoices);
            choices = mutableChoices;
        }

        ArithmeticOperation placeholderOperation =
            operation switch
            {
                FractionOperation.Add => ArithmeticOperation.Add,
                FractionOperation.Subtract => ArithmeticOperation.Subtract,
                FractionOperation.Multiply => ArithmeticOperation.Multiply,
                FractionOperation.Divide => ArithmeticOperation.Divide,
                _ => ArithmeticOperation.Add
            };

        return new ArithmeticQuizQuestion(
            new IntegerArithmeticExpression(
                BigInteger.Zero,
                placeholderOperation,
                BigInteger.One),
            mode,
            BigInteger.Zero,
            null,
            equationIsCorrect,
            [],
            FractionProblem: new FractionQuizContract(
                left,
                operation,
                right,
                answer,
                presented,
                choices));
    }

    private int CreateDenominator(
        QuizCurriculumLayer.FractionRules? curriculumRules,
        bool secondaryOperand)
    {
        if (curriculumRules is null)
        {
            return _random.Next(2, 13);
        }

        int maximum = Math.Max(2, curriculumRules.MaximumDenominator);

        return secondaryOperand
            ? QuizCurriculumLayer.NextSecondaryOperand(
                _random,
                curriculumRules.Tier,
                minimumAllowed: 2,
                maximumOverride: maximum)
            : QuizCurriculumLayer.NextPrimaryOperand(
                _random,
                curriculumRules.Tier,
                minimumAllowed: 2,
                maximumOverride: maximum);
    }

    private ReducedFraction CreateOperand(
        QuizCurriculumLayer.FractionRules? curriculumRules,
        bool secondaryOperand,
        int? forcedDenominator = null)
    {
        int denominator =
            forcedDenominator ??
            CreateDenominator(
                curriculumRules,
                secondaryOperand);

        int maximumNumerator = Math.Min(
            denominator - 1,
            curriculumRules?.MaximumNumerator ?? denominator - 1);

        int numerator = _random.Next(1, maximumNumerator + 1);
        return new ReducedFraction(numerator, denominator);
    }

    private ReducedFraction Calculate(
        ReducedFraction left,
        ReducedFraction right,
        FractionOperation operation)
    {
        FractionCalculationResult result = _engine.Calculate(
            left.Numerator,
            left.Denominator,
            right.Numerator,
            right.Denominator,
            operation);

        if (!result.IsSuccess ||
            !ReducedFraction.TryParse(result.ResultExpression, out ReducedFraction answer))
        {
            throw new InvalidOperationException(
                result.ErrorMessage.Length > 0
                    ? result.ErrorMessage
                    : "Fraction engine returned an invalid result.");
        }

        return answer;
    }

    private IReadOnlyList<ReducedFraction> CreateDistractors(
        ReducedFraction answer,
        int count)
    {
        var result = new HashSet<ReducedFraction>();
        int[] offsets = [-3, -2, -1, 1, 2, 3];

        for (int pass = 0; result.Count < count; pass++)
        {
            int offset = offsets[pass % offsets.Length];
            var candidate = new ReducedFraction(
                answer.Numerator + offset,
                answer.Denominator);

            if (candidate != answer)
            {
                result.Add(candidate);
            }
        }

        return result.ToArray();
    }

    private static int Compare(
        ReducedFraction left,
        ReducedFraction right) =>
        (left.Numerator * right.Denominator)
            .CompareTo(right.Numerator * left.Denominator);

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) =
                (values[swapIndex], values[index]);
        }
    }
}
