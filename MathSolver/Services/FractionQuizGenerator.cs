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
        FractionOperation? requestedOperation = null)
    {
        FractionOperation operation =
            requestedOperation ??
            SupportedOperations[_random.Next(SupportedOperations.Length)];

        if (!SupportedOperations.Contains(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedOperation));
        }

        ReducedFraction left;
        ReducedFraction right;
        ReducedFraction answer;

        do
        {
            left = CreateOperand();
            right = CreateOperand();

            if (operation == FractionOperation.Subtract &&
                Compare(left, right) < 0)
            {
                (left, right) = (right, left);
            }

            answer = Calculate(left, right, operation);
        }
        while (operation == FractionOperation.Divide &&
               right.Numerator.IsZero);

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

    private ReducedFraction CreateOperand()
    {
        int denominator = _random.Next(2, 13);
        int numerator = _random.Next(1, denominator);
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
