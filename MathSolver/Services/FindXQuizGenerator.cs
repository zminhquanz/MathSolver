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
        ArithmeticQuizMode mode)
    {
        for (int attempt = 0;
             attempt < MaximumGenerationAttempts;
             attempt++)
        {
            FindXQuizContract contract =
                CreateContract();

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

    private FindXQuizContract CreateContract()
    {
        ArithmeticOperation operation =
            Operations[_random.Next(Operations.Length)];
        bool unknownIsLeftOperand =
            _random.Next(2) == 0;

        BigInteger knownValue;
        BigInteger resultValue;
        BigInteger correctAnswer;

        switch (operation)
        {
            case ArithmeticOperation.Add:
                correctAnswer = _random.Next(1, 101);
                knownValue = _random.Next(1, 101);
                resultValue = correctAnswer + knownValue;
                break;

            case ArithmeticOperation.Subtract
                when unknownIsLeftOperand:
                knownValue = _random.Next(1, 51);
                resultValue = _random.Next(0, 101);
                correctAnswer = resultValue + knownValue;
                break;

            case ArithmeticOperation.Subtract:
                correctAnswer = _random.Next(1, 51);
                resultValue = _random.Next(0, 101);
                knownValue = correctAnswer + resultValue;
                break;

            case ArithmeticOperation.Multiply:
                correctAnswer = _random.Next(1, 13);
                knownValue = _random.Next(1, 13);
                resultValue = correctAnswer * knownValue;
                break;

            case ArithmeticOperation.Divide
                when unknownIsLeftOperand:
                knownValue = _random.Next(1, 13);
                resultValue = _random.Next(1, 13);
                correctAnswer = resultValue * knownValue;
                break;

            case ArithmeticOperation.Divide:
                correctAnswer = _random.Next(1, 13);
                resultValue = _random.Next(1, 13);
                knownValue = correctAnswer * resultValue;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operation));
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
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation))
        };

    private bool IsVerifiedContract(
        FindXQuizContract contract)
    {
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
