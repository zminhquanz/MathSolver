using MathSolver.Models;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh phương trình tìm x một bước. FindXEngine là nguồn sự thật duy nhất
/// cho nghiệm và phép thay ngược; generator chỉ chọn dữ kiện nhỏ, tự nhiên.
/// </summary>
public sealed class FindXQuizGenerator
{
    private const int MaximumGenerationAttempts = 64;

    private static readonly ArithmeticOperation[] Operations =
    [
        ArithmeticOperation.Add,
        ArithmeticOperation.Subtract,
        ArithmeticOperation.Multiply,
        ArithmeticOperation.Divide
    ];

    private readonly FindXEngine _engine;
    private readonly Random _random;

    public FindXQuizGenerator(
        FindXEngine engine,
        Random? random = null)
    {
        _engine = engine ??
            throw new ArgumentNullException(nameof(engine));
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion Generate(
        ArithmeticQuizMode mode,
        ArithmeticOperation? requestedOperation = null,
        QuizCurriculumContext? curriculumContext = null)
    {
        QuizCurriculumLayer.FindXRules? curriculumRules =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetFindXRules(
                    curriculumContext.Value)
                : null;
        for (int attempt = 0;
             attempt < MaximumGenerationAttempts;
             attempt++)
        {
            FindXQuizContract contract =
                CreateContract(
                    requestedOperation,
                    curriculumRules);

            if (!IsVerifiedContract(contract))
            {
                continue;
            }

            return CreateQuestion(
                mode,
                contract);
        }

        throw new InvalidOperationException(
            "Could not generate a verified Find X quiz contract.");
    }

    private FindXQuizContract CreateContract(
        ArithmeticOperation? requestedOperation,
        QuizCurriculumLayer.FindXRules? curriculumRules)
    {
        IReadOnlyList<ArithmeticOperation> allowedOperations =
            curriculumRules?.AllowedOperations ??
            Operations;

        ArithmeticOperation operation =
            requestedOperation.HasValue &&
            allowedOperations.Contains(requestedOperation.Value)
                ? requestedOperation.Value
                : allowedOperations[_random.Next(allowedOperations.Count)];
        bool unknownIsLeftOperand =
            _random.Next(2) == 0;

        if (curriculumRules is null)
        {
            return CreateLegacyContract(operation, unknownIsLeftOperand);
        }

        CurriculumTier tier = curriculumRules.Tier;
        BigInteger knownValue;
        BigInteger resultValue;
        BigInteger correctAnswer;

        switch (operation)
        {
            case ArithmeticOperation.Add:
            {
                int x = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier);
                int known = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier);

                correctAnswer = x;
                knownValue = known;
                resultValue = (BigInteger)x + known;
                break;
            }

            case ArithmeticOperation.Subtract when unknownIsLeftOperand:
            {
                int x = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier);
                int known = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: x);

                correctAnswer = x;
                knownValue = known;
                resultValue = x - known;
                break;
            }

            case ArithmeticOperation.Subtract:
            {
                int known = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier);
                int x = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: known);

                correctAnswer = x;
                knownValue = known;
                resultValue = known - x;
                break;
            }

            case ArithmeticOperation.Multiply:
            {
                int x = QuizCurriculumLayer.NextPrimaryOperand(
                    _random,
                    tier,
                    maximumOverride: curriculumRules.MaximumFactor);
                int safeKnownMaximum = Math.Min(
                    curriculumRules.MaximumFactor,
                    int.MaxValue / Math.Max(1, x));
                int known = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: safeKnownMaximum);

                correctAnswer = x;
                knownValue = known;
                resultValue = (BigInteger)x * known;
                break;
            }

            case ArithmeticOperation.Divide when unknownIsLeftOperand:
            {
                int known = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: curriculumRules.MaximumFactor);
                int x = QuizCurriculumLayer.NextPrimaryMultiple(
                    _random,
                    tier,
                    known,
                    curriculumRules.MaximumAddSubtractValue);

                knownValue = known;
                correctAnswer = x;
                resultValue = x / known;
                break;
            }

            case ArithmeticOperation.Divide:
            {
                int x = QuizCurriculumLayer.NextSecondaryOperand(
                    _random,
                    tier,
                    maximumOverride: curriculumRules.MaximumFactor);
                int known = QuizCurriculumLayer.NextPrimaryMultiple(
                    _random,
                    tier,
                    x,
                    curriculumRules.MaximumAddSubtractValue);

                correctAnswer = x;
                knownValue = known;
                resultValue = known / x;
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        IntegerArithmeticExpression solutionExpression =
            CreateSolutionExpression(
                knownValue,
                resultValue,
                operation,
                unknownIsLeftOperand);

        return new(
            knownValue,
            resultValue,
            operation,
            unknownIsLeftOperand,
            correctAnswer,
            solutionExpression);
    }

    private FindXQuizContract CreateLegacyContract(
        ArithmeticOperation operation,
        bool unknownIsLeftOperand)
    {
        int maximum = 100;
        BigInteger knownValue;
        BigInteger resultValue;
        BigInteger correctAnswer;

        switch (operation)
        {
            case ArithmeticOperation.Add:
            {
                int x = _random.Next(1, maximum + 1);
                int known = _random.Next(1, maximum + 1);
                correctAnswer = x;
                knownValue = known;
                resultValue = x + known;
                break;
            }
            case ArithmeticOperation.Subtract when unknownIsLeftOperand:
            {
                int x = _random.Next(1, maximum + 1);
                int known = _random.Next(0, x + 1);
                correctAnswer = x;
                knownValue = known;
                resultValue = x - known;
                break;
            }
            case ArithmeticOperation.Subtract:
            {
                int known = _random.Next(1, maximum + 1);
                int x = _random.Next(0, known + 1);
                correctAnswer = x;
                knownValue = known;
                resultValue = known - x;
                break;
            }
            case ArithmeticOperation.Multiply:
            {
                int x = _random.Next(1, 13);
                int known = _random.Next(1, 13);
                correctAnswer = x;
                knownValue = known;
                resultValue = x * known;
                break;
            }
            case ArithmeticOperation.Divide when unknownIsLeftOperand:
            {
                int known = _random.Next(1, 13);
                int quotient = _random.Next(1, 13);
                knownValue = known;
                correctAnswer = known * quotient;
                resultValue = quotient;
                break;
            }
            case ArithmeticOperation.Divide:
            {
                int x = _random.Next(1, 13);
                int quotient = _random.Next(1, 13);
                correctAnswer = x;
                knownValue = x * quotient;
                resultValue = quotient;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return new(
            knownValue,
            resultValue,
            operation,
            unknownIsLeftOperand,
            correctAnswer,
            CreateSolutionExpression(
                knownValue,
                resultValue,
                operation,
                unknownIsLeftOperand));
    }

    private static IntegerArithmeticExpression CreateSolutionExpression(
        BigInteger knownValue,
        BigInteger resultValue,
        ArithmeticOperation operation,
        bool unknownIsLeftOperand) =>
        (operation, unknownIsLeftOperand) switch
        {
            (ArithmeticOperation.Add, _) =>
                new(
                    resultValue,
                    ArithmeticOperation.Subtract,
                    knownValue),
            (ArithmeticOperation.Subtract, true) =>
                new(
                    resultValue,
                    ArithmeticOperation.Add,
                    knownValue),
            (ArithmeticOperation.Subtract, false) =>
                new(
                    knownValue,
                    ArithmeticOperation.Subtract,
                    resultValue),
            (ArithmeticOperation.Multiply, _) =>
                new(
                    resultValue,
                    ArithmeticOperation.Divide,
                    knownValue),
            (ArithmeticOperation.Divide, true) =>
                new(
                    resultValue,
                    ArithmeticOperation.Multiply,
                    knownValue),
            (ArithmeticOperation.Divide, false) =>
                new(
                    knownValue,
                    ArithmeticOperation.Divide,
                    resultValue),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private bool IsVerifiedContract(
        FindXQuizContract contract)
    {
        if (contract.Operation == ArithmeticOperation.Multiply &&
            (contract.KnownValue < int.MinValue ||
             contract.KnownValue > int.MaxValue ||
             contract.ResultValue < int.MinValue ||
             contract.ResultValue > int.MaxValue ||
             contract.CorrectAnswer < int.MinValue ||
             contract.CorrectAnswer > int.MaxValue))
        {
            return false;
        }

        FindXIntegerResult solved =
            _engine.SolveInteger(
                contract.KnownValue,
                contract.ResultValue,
                contract.Operation,
                contract.UnknownIsLeftOperand);

        if (solved.Kind != FindXCoreSolutionKind.Unique ||
            solved.Denominator != BigInteger.One ||
            solved.Numerator != contract.CorrectAnswer)
        {
            return false;
        }

        (BigInteger numerator, BigInteger denominator) =
            _engine.EvaluateIntegerLeftSide(
                solved.Numerator,
                solved.Denominator,
                contract.KnownValue,
                contract.Operation,
                contract.UnknownIsLeftOperand);

        return denominator == BigInteger.One &&
               numerator == contract.ResultValue;
    }

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        FindXQuizContract contract)
    {
        BigInteger answer = contract.CorrectAnswer;

        return mode switch
        {
            ArithmeticQuizMode.TrueFalse =>
                CreateTrueFalseQuestion(contract),
            ArithmeticQuizMode.MultipleChoice =>
                new(
                    contract.SolutionExpression,
                    mode,
                    answer,
                    null,
                    null,
                    CreateChoices(contract),
                    FindXProblem: contract),
            ArithmeticQuizMode.Essay =>
                new(
                    contract.SolutionExpression,
                    mode,
                    answer,
                    null,
                    null,
                    [],
                    FindXProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalseQuestion(
        FindXQuizContract contract)
    {
        bool presentCorrectAnswer =
            _random.Next(2) == 0;
        BigInteger presentedAnswer =
            presentCorrectAnswer
                ? contract.CorrectAnswer
                : CreateDistractors(contract, 1)[0];

        return new(
            contract.SolutionExpression,
            ArithmeticQuizMode.TrueFalse,
            contract.CorrectAnswer,
            presentedAnswer,
            IsCandidateSolution(contract, presentedAnswer),
            [],
            FindXProblem: contract);
    }

    private IReadOnlyList<BigInteger> CreateChoices(
        FindXQuizContract contract)
    {
        var choices = new List<BigInteger>
        {
            contract.CorrectAnswer
        };

        choices.AddRange(
            CreateDistractors(contract, 3));

        for (int index = choices.Count - 1;
             index > 0;
             index--)
        {
            int swapIndex = _random.Next(index + 1);
            (choices[index], choices[swapIndex]) =
                (choices[swapIndex], choices[index]);
        }

        return choices;
    }

    private IReadOnlyList<BigInteger> CreateDistractors(
        FindXQuizContract contract,
        int count)
    {
        var distractors = new HashSet<BigInteger>();
        int[] offsets = [-10, -5, -3, -2, -1, 1, 2, 3, 5, 10];
        int startIndex = _random.Next(offsets.Length);

        for (int index = 0;
             index < offsets.Length && distractors.Count < count;
             index++)
        {
            BigInteger candidate =
                contract.CorrectAnswer +
                offsets[(startIndex + index) % offsets.Length];

            if (candidate.Sign < 0 ||
                IsCandidateSolution(contract, candidate))
            {
                continue;
            }

            distractors.Add(candidate);
        }

        for (int offset = 11;
             distractors.Count < count;
             offset++)
        {
            BigInteger candidate =
                contract.CorrectAnswer + offset;

            if (!IsCandidateSolution(contract, candidate))
            {
                distractors.Add(candidate);
            }
        }

        return distractors.ToArray();
    }

    private bool IsCandidateSolution(
        FindXQuizContract contract,
        BigInteger candidate)
    {
        if (contract.Operation == ArithmeticOperation.Divide &&
            !contract.UnknownIsLeftOperand &&
            candidate.IsZero)
        {
            return false;
        }

        try
        {
            (BigInteger numerator, BigInteger denominator) =
                _engine.EvaluateIntegerLeftSide(
                    candidate,
                    BigInteger.One,
                    contract.KnownValue,
                    contract.Operation,
                    contract.UnknownIsLeftOperand);

            return denominator == BigInteger.One &&
                   numerator == contract.ResultValue;
        }
        catch (DivideByZeroException)
        {
            return false;
        }
    }
}
