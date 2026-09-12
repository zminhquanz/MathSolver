using MathSolver.Models;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh câu hỏi số nguyên nhỏ cho học sinh. Mọi đáp án đều được tính lại bằng
/// BasicArithmeticEngine, sau đó được ArithmeticQuizValidator kiểm tra trước
/// khi trả về giao diện.
/// </summary>
public sealed class ArithmeticQuizGenerator
{
    private const int MaximumGenerationAttempts = 64;

    private static readonly ArithmeticOperation[] AllOperations =
    [
        ArithmeticOperation.Add,
        ArithmeticOperation.Subtract,
        ArithmeticOperation.Multiply,
        ArithmeticOperation.Divide
    ];

    private readonly BasicArithmeticEngine _engine;
    private readonly ArithmeticQuizValidator _validator;
    private readonly Random _random;

    public ArithmeticQuizGenerator(
        BasicArithmeticEngine engine,
        Random? random = null)
    {
        _engine =
            engine ??
            throw new ArgumentNullException(
                nameof(engine));

        _validator =
            new ArithmeticQuizValidator(
                engine);

        _random =
            random ??
            Random.Shared;
    }

    public ArithmeticQuizQuestion Generate(
        ArithmeticQuizMode mode,
        ArithmeticOperation? requestedOperation = null,
        QuizCurriculumContext? curriculumContext = null)
    {
        QuizCurriculumLayer.ArithmeticRules? curriculumRules =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetArithmeticRules(
                    curriculumContext.Value)
                : null;
        for (int attempt = 0;
             attempt < MaximumGenerationAttempts;
             attempt++)
        {
            IReadOnlyList<ArithmeticOperation> allowedOperations =
                curriculumRules?.AllowedOperations ??
                AllOperations;

            ArithmeticOperation operation =
                requestedOperation.HasValue &&
                allowedOperations.Contains(requestedOperation.Value)
                    ? requestedOperation.Value
                    : allowedOperations[
                        _random.Next(allowedOperations.Count)];

            IntegerArithmeticExpression expression =
                CreateExpression(
                    operation,
                    curriculumRules);

            IntegerArithmeticResult calculation =
                _engine.CalculateInteger(
                    expression);

            ArithmeticQuizQuestion question =
                mode switch
                {
                    ArithmeticQuizMode.TrueFalse =>
                        CreateTrueFalseQuestion(
                            expression,
                            calculation.Result),

                    ArithmeticQuizMode.MultipleChoice =>
                        CreateMultipleChoiceQuestion(
                            expression,
                            calculation.Result),

                    ArithmeticQuizMode.Essay =>
                        CreateEssayQuestion(
                            expression,
                            calculation.Result),

                    _ => throw new ArgumentOutOfRangeException(
                        nameof(mode),
                        mode,
                        "Unsupported quiz mode.")
                };

            if (_validator.Validate(
                    question).IsValid)
            {
                return question;
            }
        }

        throw new InvalidOperationException(
            "Could not generate a valid arithmetic quiz question.");
    }

    private IntegerArithmeticExpression CreateExpression(
        ArithmeticOperation operation,
        QuizCurriculumLayer.ArithmeticRules? curriculumRules)
    {
        int left;
        int right;

        if (curriculumRules is null)
        {
            return CreateLegacyExpression(operation);
        }

        CurriculumTier tier = curriculumRules.Tier;

        switch (operation)
        {
            case ArithmeticOperation.Add:
                left = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier);
                right = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier);
                break;

            case ArithmeticOperation.Subtract:
                left = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier);
                right = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: left);
                break;

            case ArithmeticOperation.Multiply:
                left = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier);

                // b được random theo bậc chữ số từ ★ đến tier hiện tại,
                // nhưng contract Toán đố phải luôn nằm trong Int32 để C#
                // validator có thể đối chiếu tuyệt đối an toàn.
                int safeRightMaximum = Math.Min(
                    curriculumRules.MaximumMultiplicationFactor,
                    int.MaxValue / Math.Max(1, left));
                right = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: safeRightMaximum);
                break;

            case ArithmeticOperation.Divide:
                int factorMaximum = Math.Max(
                    1,
                    Math.Min(
                        curriculumRules.MaximumDivisionFactor,
                        curriculumRules.MaximumValue));

                // Chọn divisor theo bậc chữ số độc lập rồi chọn dividend là
                // một bội chính xác trong đúng bậc của tier. Ví dụ ở ★★★★★
                // hoàn toàn có thể sinh 18.258 ÷ 2 = 9.129.
                right = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: factorMaximum);
                left = QuizCurriculumLayer.NextPrimaryMultiple(
                    _random,
                    tier,
                    right,
                    curriculumRules.MaximumValue);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unsupported arithmetic operation.");
        }

        return new IntegerArithmeticExpression(
            left,
            operation,
            right);
    }

    private IntegerArithmeticExpression CreateLegacyExpression(
        ArithmeticOperation operation)
    {
        int left;
        int right;

        switch (operation)
        {
            case ArithmeticOperation.Add:
                left = _random.Next(0, 101);
                right = _random.Next(0, 101);
                break;

            case ArithmeticOperation.Subtract:
                left = _random.Next(0, 101);
                right = _random.Next(0, left + 1);
                break;

            case ArithmeticOperation.Multiply:
                left = _random.Next(0, 13);
                right = _random.Next(0, 13);
                break;

            case ArithmeticOperation.Divide:
                right = _random.Next(1, 13);
                int quotient = _random.Next(0, 13);
                left = right * quotient;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unsupported arithmetic operation.");
        }

        return new IntegerArithmeticExpression(left, operation, right);
    }

    private ArithmeticQuizQuestion CreateTrueFalseQuestion(
        IntegerArithmeticExpression expression,
        BigInteger correctAnswer)
    {
        bool presentCorrectEquation =
            _random.Next(2) == 0;

        BigInteger presentedAnswer =
            presentCorrectEquation
                ? correctAnswer
                : CreateDistractors(
                    expression,
                    correctAnswer,
                    1)[0];

        bool equationIsCorrect =
            _engine.IsEquationCorrect(
                expression,
                presentedAnswer);

        return new ArithmeticQuizQuestion(
            expression,
            ArithmeticQuizMode.TrueFalse,
            correctAnswer,
            presentedAnswer,
            equationIsCorrect,
            []);
    }

    private ArithmeticQuizQuestion CreateMultipleChoiceQuestion(
        IntegerArithmeticExpression expression,
        BigInteger correctAnswer)
    {
        var choices =
            new List<BigInteger>
            {
                correctAnswer
            };

        choices.AddRange(
            CreateDistractors(
                expression,
                correctAnswer,
                3));

        Shuffle(
            choices);

        return new ArithmeticQuizQuestion(
            expression,
            ArithmeticQuizMode.MultipleChoice,
            correctAnswer,
            null,
            null,
            choices);
    }

    private static ArithmeticQuizQuestion CreateEssayQuestion(
        IntegerArithmeticExpression expression,
        BigInteger correctAnswer)
    {
        return new ArithmeticQuizQuestion(
            expression,
            ArithmeticQuizMode.Essay,
            correctAnswer,
            null,
            null,
            []);
    }

    private IReadOnlyList<BigInteger> CreateDistractors(
        IntegerArithmeticExpression expression,
        BigInteger correctAnswer,
        int count)
    {
        var distractors =
            new HashSet<BigInteger>();

        int[] offsets =
        [
            -10, -5, -3, -2, -1,
            1, 2, 3, 5, 10
        ];

        int startIndex =
            _random.Next(
                offsets.Length);

        for (int index = 0;
             index < offsets.Length &&
             distractors.Count < count;
             index++)
        {
            int offset =
                offsets[
                    (startIndex + index) %
                    offsets.Length];

            BigInteger candidate =
                correctAnswer +
                offset;

            if (candidate.Sign < 0 ||
                _engine.IsEquationCorrect(
                    expression,
                    candidate))
            {
                continue;
            }

            distractors.Add(
                candidate);
        }

        for (int offset = 11;
             distractors.Count < count;
             offset++)
        {
            BigInteger candidate =
                correctAnswer +
                offset;

            if (!_engine.IsEquationCorrect(
                    expression,
                    candidate))
            {
                distractors.Add(
                    candidate);
            }
        }

        return distractors.ToArray();
    }

    private void Shuffle<T>(
        IList<T> values)
    {
        for (int index = values.Count - 1;
             index > 0;
             index--)
        {
            int swapIndex =
                _random.Next(
                    index + 1);

            (values[index], values[swapIndex]) =
                (values[swapIndex], values[index]);
        }
    }
}

/// <summary>
/// Kiểm tra toàn bộ bất biến của câu hỏi trước khi giao diện nhận câu hỏi đó.
/// </summary>
public sealed class ArithmeticQuizValidator
{
    private readonly BasicArithmeticEngine _engine;

    public ArithmeticQuizValidator(
        BasicArithmeticEngine engine)
    {
        _engine =
            engine ??
            throw new ArgumentNullException(
                nameof(engine));
    }

    public ArithmeticQuizValidationResult Validate(
        ArithmeticQuizQuestion question)
    {
        ArgumentNullException.ThrowIfNull(
            question);

        IntegerArithmeticResult calculation;

        try
        {
            calculation =
                _engine.CalculateInteger(
                    question.Expression);
        }
        catch (DivideByZeroException)
        {
            return new(false, "DivisionByZero");
        }

        if (calculation.IsDivision &&
            !calculation.IsExactDivision)
        {
            return new(false, "NonExactDivision");
        }

        if (question.Expression.Operation == ArithmeticOperation.Multiply &&
            (question.Expression.LeftOperand < int.MinValue ||
             question.Expression.LeftOperand > int.MaxValue ||
             question.Expression.RightOperand < int.MinValue ||
             question.Expression.RightOperand > int.MaxValue ||
             calculation.Result < int.MinValue ||
             calculation.Result > int.MaxValue))
        {
            return new(false, "MultiplicationOutOfInt32Range");
        }

        if (calculation.Result !=
            question.CorrectAnswer)
        {
            return new(false, "IncorrectAnswerKey");
        }

        if (question.Mode ==
            ArithmeticQuizMode.TrueFalse)
        {
            if (!question.PresentedAnswer.HasValue ||
                !question.PresentedEquationIsCorrect.HasValue ||
                question.Choices.Count != 0)
            {
                return new(false, "InvalidTrueFalseShape");
            }

            BigInteger presentedAnswer =
                question.PresentedAnswer.Value;

            bool expectedTruth =
                question.PresentedEquationIsCorrect.Value;

            bool actualTruth =
                _engine.IsEquationCorrect(
                    question.Expression,
                    presentedAnswer);

            return actualTruth == expectedTruth
                ? ArithmeticQuizValidationResult.Valid
                : new(false, "TruthFlagMismatch");
        }

        if (question.Mode ==
            ArithmeticQuizMode.Essay)
        {
            return question.PresentedAnswer is null &&
                   question.PresentedEquationIsCorrect is null &&
                   question.Choices.Count == 0
                ? ArithmeticQuizValidationResult.Valid
                : new(false, "InvalidEssayShape");
        }

        if (question.Mode !=
                ArithmeticQuizMode.MultipleChoice ||
            question.PresentedAnswer is not null ||
            question.PresentedEquationIsCorrect is not null ||
            question.Choices.Count != 4 ||
            question.Choices.Distinct().Count() != 4 ||
            question.Choices.Count(
                choice =>
                    choice == question.CorrectAnswer) != 1)
        {
            return new(false, "InvalidMultipleChoiceShape");
        }

        return ArithmeticQuizValidationResult.Valid;
    }
}
