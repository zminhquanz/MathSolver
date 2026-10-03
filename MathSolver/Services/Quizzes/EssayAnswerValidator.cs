using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public enum EssayAnswerError
{
    None,
    InvalidEquationFormat,
    WrongOperandsOrOperation,
    WrongEquationResult,
    WrongEquationUnit,
    MissingSolution,
    WrongSolutionContent,
    InvalidAnswerFormat,
    WrongAnswer,
    WrongAnswerUnit
}

public sealed record EssayAnswerValidationResult(
    bool SolutionIsCorrect,
    bool EquationIsCorrect,
    bool AnswerIsCorrect,
    EssayAnswerError SolutionError,
    EssayAnswerError EquationError,
    EssayAnswerError AnswerError)
{
    public bool IsCorrect =>
        SolutionIsCorrect &&
        EquationIsCorrect &&
        AnswerIsCorrect;

    public IReadOnlyList<EssayStepValidationResult> Steps { get; init; } = [];
    public IReadOnlyList<string> Details { get; init; } = [];
}

public sealed record EssayStepValidationResult(
    int Number, string Equation, bool IsCorrect, EssayAnswerError Error,
    string? ComputedValue = null, string? WrittenValue = null, string? EnteredUnit = null, string? ExpectedUnit = null);

/// <summary>
/// Chấm phép tính và đáp số của bài tự luận. Câu lời giải chỉ bắt buộc với
/// bài toán có ngữ cảnh lời văn/đại lượng; các phép tính số thuần túy không
/// cần phần này.
/// </summary>
public sealed partial class EssayAnswerValidator
{
    private readonly BasicArithmeticEngine _engine;

    public EssayAnswerValidator(
        BasicArithmeticEngine engine)
    {
        _engine =
            engine ??
            throw new ArgumentNullException(
                nameof(engine));
    }

    public EssayAnswerValidationResult Validate(
        ArithmeticQuizQuestion question,
        string? solutionText,
        string? equationText,
        string? answerText)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (question.ElementaryProblem is not null)
            return ElementaryEssayValidator.Validate(question, solutionText, equationText, answerText);

        (bool solutionIsCorrect, EssayAnswerError solutionError) =
            ValidateSolution(
                question,
                solutionText);

        (string calculation, string enteredEquationUnit) =
            SplitEquationResult(question, equationText);

        IReadOnlyList<EssayStepValidationResult> steps = [];
        bool indirect = question.AverageProblem?.Type == AverageQuizType.IndirectData;
        bool geometryWork = question.GeometryProblem?.Reasoning is not null;
        (bool equationIsCorrect, EssayAnswerError equationError) = indirect
            ? AverageIndirectEssayValidator.Validate(question, equationText, out steps)
            : geometryWork ? GeometryWorkedEssayValidator.Validate(question, equationText, out steps)
            : ValidateCalculatedEquation(question, calculation);

        if (!indirect && !geometryWork && equationIsCorrect &&
            !IsExpectedUnit(question, enteredEquationUnit))
        {
            equationIsCorrect = false;
            equationError = EssayAnswerError.WrongEquationUnit;
        }

        (bool answerIsCorrect, EssayAnswerError answerError) =
            question.UsesFractionFormatting
                ? ValidateFractionAnswer(
                    question,
                    question.ExactAnswer,
                    answerText)
                : ValidateAnswer(
                    question,
                    answerText);

        return new(
            solutionIsCorrect,
            equationIsCorrect,
            answerIsCorrect,
            solutionError,
            equationError,
            answerError) { Steps = steps };
    }

    public static bool RequiresSolution(ArithmeticQuizQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (question.ElementaryProblem is not null) return question.ElementaryProblem.RequiresSolution;

        return question.WordProblem is not null ||
               question.GeometryProblem is not null ||
               question.ProportionProblem is not null ||
               question.MotionProblem is not null ||
               question.AverageProblem is not null ||
               question.PercentageProblem is not null;
    }

    public static string GetExpectedUnit(ArithmeticQuizQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return question.WordProblem?.AnswerUnit ??
               question.ElementaryProblem?.Answers.FirstOrDefault()?.Unit ??
               question.GeometryProblem?.AnswerUnit ??
               question.ProportionProblem?.AnswerUnit ??
               question.MotionProblem?.AnswerUnit ??
               question.AverageProblem?.AnswerUnit ??
               question.PercentageProblem?.AnswerUnit ??
               string.Empty;
    }

    internal static (string Calculation, string Unit) SplitEquationResult(
        ArithmeticQuizQuestion question,
        string? equationText,
        bool allowFractionResult = false)
    {
        string text = equationText ?? string.Empty;
        int equalsIndex = text.LastIndexOf('=');
        if (equalsIndex < 0)
            return (text, string.Empty);

        string resultText = text[(equalsIndex + 1)..];
        Match match = question.UsesFractionFormatting || allowFractionResult
            ? FractionEquationResultRegex().Match(resultText)
            : AnswerRegex().Match(resultText);
        if (!match.Success)
            return (text, string.Empty);

        string enteredUnit = !question.UsesFractionFormatting &&
            match.Groups["percent"].Success
            ? "%"
            : match.Groups["unit"].Value;
        return (text[..(equalsIndex + 1)] + match.Groups["value"].Value,
            NormalizeUnit(enteredUnit));
    }

    private static bool IsExpectedUnit(
        ArithmeticQuizQuestion question,
        string enteredUnit)
    {
        string expectedUnit = NormalizeUnit(GetExpectedUnit(question));
        return expectedUnit.Length == 0 ||
               enteredUnit.Length > 0 &&
               UnitsMatch(
                   enteredUnit,
                   expectedUnit,
                   question.WordProblem?.ProblemText ??
                   question.ProportionProblem?.ProblemText ??
                   question.MotionProblem?.ProblemText ??
                   question.AverageProblem?.ProblemText ??
                   question.PercentageProblem?.ProblemText);
    }

    internal static bool IsExpectedUnitForFeedback(
        ArithmeticQuizQuestion question,
        string enteredUnit) =>
        IsExpectedUnit(question, NormalizeUnit(enteredUnit));

    internal static (bool IsCorrect, EssayAnswerError Error)
        ValidateSolution(
            ArithmeticQuizQuestion question,
            string? solutionText)
    {
        if (!RequiresSolution(question))
        {
            return (true, EssayAnswerError.None);
        }

        string solution = NormalizeComparisonText(
            NormalizeUnit(Regex.Replace(
                solutionText ?? string.Empty,
                @"(?<=\d)(?=\p{L})",
                " ",
                RegexOptions.CultureInvariant)));

        if (solution.Length == 0)
        {
            return (false, EssayAnswerError.MissingSolution);
        }

        if (MentionsSolutionCue(question, solution, solutionText))
        {
            return (true, EssayAnswerError.None);
        }

        if (question.GeometryProblem is GeometryQuizContract geometry)
        {
            bool mentionsGeometryQuantity =
                GetGeometryQuantityPhrases(
                        geometry.Measurement)
                    .Any(phrase =>
                        ContainsNormalizedPhrase(
                            solution,
                            phrase));

            return mentionsGeometryQuantity
                ? (true, EssayAnswerError.None)
                : (false, EssayAnswerError.WrongSolutionContent);
        }

        if (question.MotionProblem is MotionQuizContract motion &&
            GetMotionQuantityPhrases(motion.QuestionKind)
                .Any(phrase => ContainsNormalizedPhrase(solution, phrase)))
        {
            return (true, EssayAnswerError.None);
        }

        string expectedUnit =
            NormalizeUnit(
                question.WordProblem?.AnswerUnit ??
                question.ProportionProblem?.AnswerUnit ??
                question.MotionProblem?.AnswerUnit ??
                question.AverageProblem?.AnswerUnit ??
                question.PercentageProblem?.AnswerUnit);

        bool mentionsExpectedQuantity =
            expectedUnit == "%" &&
            ((solutionText ?? string.Empty).Contains('%') ||
             ContainsNormalizedPhrase(solution, "phần trăm") ||
             ContainsNormalizedPhrase(solution, "percent") ||
             ContainsNormalizedPhrase(solution, "percentage")) ||
            WordProblemUnitEquivalence.ContainsVietnameseUnit(
                solution,
                expectedUnit) ||
            ContainsNormalizedPhrase(solution, expectedUnit) ||
            ContainsNormalizedPhrase(
                solution,
                NormalizeEnglishUnitToSingular(expectedUnit));

        return mentionsExpectedQuantity
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongSolutionContent);
    }

    private static bool MentionsSolutionCue(
        ArithmeticQuizQuestion question,
        string normalizedSolution,
        string? originalSolution)
    {
        string expectedUnit = NormalizeUnit(GetExpectedUnit(question));
        if (expectedUnit == "%" &&
            ((originalSolution ?? string.Empty).Contains('%') ||
             ContainsNormalizedPhrase(normalizedSolution, "phần trăm") ||
             ContainsNormalizedPhrase(normalizedSolution, "percentage") ||
             ContainsNormalizedPhrase(normalizedSolution, "tỉ lệ") ||
             ContainsNormalizedPhrase(normalizedSolution, "tỷ lệ") ||
             ContainsNormalizedPhrase(normalizedSolution, "share")))
        {
            return true;
        }

        if (WordProblemUnitEquivalence.ContainsVietnameseUnit(
                normalizedSolution, expectedUnit) ||
            ContainsNormalizedPhrase(normalizedSolution, expectedUnit) ||
            ContainsNormalizedPhrase(
                normalizedSolution,
                NormalizeEnglishUnitToSingular(expectedUnit)) ||
            (GetMetricUnitSymbol(expectedUnit, allowDescriptiveSuffix: true)
                 is string metricSymbol &&
             ContainsNormalizedPhrase(normalizedSolution, metricSymbol)))
        {
            return true;
        }

        string[] unitWords = NormalizeComparisonText(expectedUnit).Split(
            ' ', StringSplitOptions.RemoveEmptyEntries);
        if (unitWords.Length > 1 &&
            ((unitWords[0] is "kg" or "gam" or "g" or "tấn" or
                "lít" or "mét" or "km" or "cm" or "mm" &&
              ContainsNormalizedPhrase(normalizedSolution, unitWords[0])) ||
             unitWords[^1] is not ("vuông" or "khối" or "of") &&
             ContainsNormalizedPhrase(normalizedSolution, unitWords[^1])))
        {
            return true;
        }

        string? subject = question.ProportionProblem?.SubjectName ??
                          question.MotionProblem?.SubjectName ??
                          question.AverageProblem?.SubjectName ??
                          question.PercentageProblem?.SubjectName;

        return ContainsNormalizedPhrase(normalizedSolution, subject ?? string.Empty) ||
               question.ProportionProblem is
                   { Scenario: ProportionScenarioKind.PaintArea } &&
               (ContainsNormalizedPhrase(normalizedSolution, "tường") ||
                ContainsNormalizedPhrase(normalizedSolution, "wall")) ||
               question.GeometryProblem is GeometryQuizContract geometry &&
               (ContainsNormalizedPhrase(normalizedSolution, geometry.ObjectName) ||
                ContainsNormalizedPhrase(normalizedSolution, geometry.ShapeName));
    }

    private static IReadOnlyList<string> GetGeometryQuantityPhrases(
        GeometryMeasurement measurement) =>
        measurement switch
        {
            GeometryMeasurement.Perimeter =>
                ["chu vi", "perimeter"],
            GeometryMeasurement.Area =>
                ["diện tích", "area"],
            GeometryMeasurement.TotalArea =>
                ["diện tích toàn phần", "total surface area"],
            GeometryMeasurement.LateralArea =>
                ["diện tích xung quanh", "lateral surface area"],
            GeometryMeasurement.Volume =>
                ["thể tích", "volume"],
            _ => []
        };

    private static IReadOnlyList<string> GetMotionQuantityPhrases(
        MotionQuestionKind kind) => kind switch
        {
            MotionQuestionKind.BasicDistance or
            MotionQuestionKind.BasicRestDistance =>
                ["quãng đường", "distance"],
            MotionQuestionKind.BasicTime or
            MotionQuestionKind.CatchUpTime or
            MotionQuestionKind.MeetingTime =>
                ["thời gian", "time"],
            _ => ["vận tốc", "tốc độ", "speed"]
        };

    private static bool ContainsNormalizedPhrase(
        string normalizedText,
        string phrase)
    {
        string normalizedPhrase =
            NormalizeComparisonText(phrase);

        return normalizedPhrase.Length > 0 &&
               $" {normalizedText} ".Contains(
                   $" {normalizedPhrase} ",
                   StringComparison.Ordinal);
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateCalculatedEquation(
            ArithmeticQuizQuestion question,
            string? equationText)
    {
        string text = (equationText ?? string.Empty).Trim();
        if (text.Length == 0 || !text.Contains('='))
            return (false, EssayAnswerError.InvalidEquationFormat);

        string[] parts = text.Split('=', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Any(string.IsNullOrWhiteSpace))
            return (false, EssayAnswerError.InvalidEquationFormat);

        int firstCalculationPart = 0;
        if (parts.Length > 2 && parts[0].All(character =>
                char.IsLetter(character) || char.IsWhiteSpace(character)))
        {
            firstCalculationPart = 1;
        }

        EssayCalculationEvaluator.Value? equationValue = null;
        bool containsOperation = false;
        for (int index = firstCalculationPart; index < parts.Length; index++)
        {
            if (!EssayCalculationEvaluator.TryEvaluate(
                    parts[index], out EssayCalculationEvaluator.Value value,
                    out bool partHasOperation))
            {
                return (false, EssayAnswerError.InvalidEquationFormat);
            }

            containsOperation |= partHasOperation;
            if (equationValue is not null && equationValue.Value != value)
                return (false, EssayAnswerError.WrongEquationResult);
            equationValue = value;
        }

        if (!containsOperation || equationValue is null)
            return (false, EssayAnswerError.InvalidEquationFormat);

        if (question.ExpressionProblem is ExpressionQuizContract expressionProblem)
        {
            string entered = NormalizeNumericExpression(parts[firstCalculationPart]);
            string given = NormalizeNumericExpression(expressionProblem.ExpressionText);
            if (entered != given)
                return (false, EssayAnswerError.WrongOperandsOrOperation);
        }

        EssayCalculationEvaluator.Value expected =
            EssayCalculationEvaluator.Value.Create(question.ExactAnswer.Numerator,
                question.ExactAnswer.Denominator);

        return equationValue.Value == expected
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongEquationResult);
    }

    private static string NormalizeNumericExpression(string text) =>
        string.Concat(text.Where(character => !char.IsWhiteSpace(character)).Select(character => character switch
        {
            '−' => '-', '×' or 'x' or 'X' or '·' => '*', '÷' or ':' => '/',
            '[' or '{' => '(', ']' or '}' => ')', _ => character
        }));

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateFractionEquation(
            FractionQuizContract contract,
            string? equationText)
    {
        string compact = (equationText ?? string.Empty)
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace('−', '-')
            .Replace('x', '×')
            .Replace('X', '×')
            .Replace('*', '×')
            .Replace(':', '÷');

        char symbol = contract.Operation switch
        {
            FractionOperation.Add => '+',
            FractionOperation.Subtract => '-',
            FractionOperation.Multiply => '×',
            FractionOperation.Divide => '÷',
            _ => '\0'
        };

        int operationIndex = compact.IndexOf(symbol);
        int equalsIndex = compact.IndexOf('=');
        if (operationIndex <= 0 ||
            equalsIndex <= operationIndex + 1 ||
            compact.LastIndexOf('=') != equalsIndex ||
            !ReducedFraction.TryParse(
                compact[..operationIndex],
                out ReducedFraction enteredLeft) ||
            !ReducedFraction.TryParse(
                compact[(operationIndex + 1)..equalsIndex],
                out ReducedFraction enteredRight) ||
            !ReducedFraction.TryParse(
                compact[(equalsIndex + 1)..],
                out ReducedFraction enteredAnswer))
        {
            return (false, EssayAnswerError.InvalidEquationFormat);
        }

        bool commutative =
            contract.Operation is FractionOperation.Add or FractionOperation.Multiply;
        bool operandsMatch =
            enteredLeft == contract.LeftOperand &&
            enteredRight == contract.RightOperand ||
            commutative &&
            enteredLeft == contract.RightOperand &&
            enteredRight == contract.LeftOperand;

        if (!operandsMatch)
        {
            return (false, EssayAnswerError.WrongOperandsOrOperation);
        }

        return enteredAnswer == contract.CorrectAnswer
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongEquationResult);
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateFractionAnswer(
            ArithmeticQuizQuestion question,
            ReducedFraction expectedAnswer,
            string? answerText)
    {
        string value = (answerText ?? string.Empty).Trim();
        Match match = Regex.Match(
            value,
            @"^\s*(?<value>[+-]?\d+(?:\s*/\s*[+-]?\d+)?)\s*(?<unit>.*?)\s*$",
            RegexOptions.CultureInvariant);

        if (!match.Success ||
            !ReducedFraction.TryParse(
                match.Groups["value"].Value,
                out ReducedFraction entered))
        {
            return (false, EssayAnswerError.InvalidAnswerFormat);
        }

        if (entered != expectedAnswer)
        {
            return (false, EssayAnswerError.WrongAnswer);
        }

        string enteredUnit = NormalizeUnit(match.Groups["unit"].Value);
        if (!IsExpectedUnit(question, enteredUnit))
        {
            return (false, EssayAnswerError.WrongAnswerUnit);
        }

        return (true, EssayAnswerError.None);
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateGeometryEquation(
            GeometryQuizContract contract,
            string? equationText)
    {
        string entered = NormalizeGeometryEquation(equationText);

        if (entered.Length == 0 ||
            !entered.Contains('='))
        {
            return (false, EssayAnswerError.InvalidEquationFormat);
        }

        HashSet<string> accepted =
            BuildAcceptedGeometryEquations(contract)
                .Select(NormalizeGeometryEquation)
                .ToHashSet(StringComparer.Ordinal);

        return accepted.Contains(entered)
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongOperandsOrOperation);
    }

    private static IEnumerable<string> BuildAcceptedGeometryEquations(
        GeometryQuizContract contract)
    {
        yield return contract.EquationText;

        IReadOnlyDictionary<string, BigInteger> value = contract.Dimensions;
        string answer = contract.CorrectAnswer.ToString(
            CultureInfo.InvariantCulture);

        if (contract.ShapeId == "rectangle")
        {
            string a = value["a"].ToString(CultureInfo.InvariantCulture);
            string b = value["b"].ToString(CultureInfo.InvariantCulture);

            if (contract.Measurement == GeometryMeasurement.Perimeter)
            {
                yield return $"2 × ({a} + {b}) = {answer}";
                yield return $"({b} + {a}) × 2 = {answer}";
            }
            else if (contract.Measurement == GeometryMeasurement.Area)
            {
                yield return $"{b} × {a} = {answer}";
            }
        }
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateProportionEquation(
            ProportionQuizContract contract,
            string? equationText)
    {
        string entered = NormalizeProportionEquation(equationText);
        if (entered.Length == 0 || !entered.Contains('='))
        {
            return (false, EssayAnswerError.InvalidEquationFormat);
        }

        string answer = contract.CorrectAnswer.ToString(CultureInfo.InvariantCulture);
        var accepted = new HashSet<string>(StringComparer.Ordinal);

        if (contract.IsDirect)
        {
            int unitRate = contract.B / contract.A;
            accepted.Add(NormalizeProportionEquation($"{contract.B} ÷ {contract.A} × {contract.C} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{contract.B} × {contract.C} ÷ {contract.A} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{unitRate} × {contract.C} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{contract.C} × {unitRate} = {answer}"));
        }
        else if (contract.AsksForAdditionalPeople)
        {
            int total = contract.A * contract.B;
            int newPeople = total / contract.C;
            accepted.Add(NormalizeProportionEquation($"{contract.A} × {contract.B} ÷ {contract.C} − {contract.A} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{contract.B} × {contract.A} ÷ {contract.C} − {contract.A} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{newPeople} − {contract.A} = {answer}"));
        }
        else
        {
            int total = contract.A * contract.B;
            accepted.Add(NormalizeProportionEquation($"{contract.A} × {contract.B} ÷ {contract.C} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{contract.B} × {contract.A} ÷ {contract.C} = {answer}"));
            accepted.Add(NormalizeProportionEquation($"{total} ÷ {contract.C} = {answer}"));
        }

        return accepted.Contains(entered)
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongOperandsOrOperation);
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateMotionEquation(
            MotionQuizContract contract,
            string? equationText)
    {
        string entered = NormalizeMotionEquation(equationText);
        if (entered.Length == 0 || !entered.Contains('='))
        {
            return (false, EssayAnswerError.InvalidEquationFormat);
        }

        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            NormalizeMotionEquation(contract.EquationText)
        };

        string symbol = contract.RepresentativeOperation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "-",
            ArithmeticOperation.Multiply => "×",
            ArithmeticOperation.Divide => "÷",
            _ => string.Empty
        };

        accepted.Add(
            NormalizeMotionEquation(
                $"{contract.RepresentativeLeft} {symbol} {contract.RepresentativeRight} = {contract.CorrectAnswer}"));

        return accepted.Contains(entered)
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongOperandsOrOperation);
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateContractEquation(
            string equationText,
            BigInteger representativeLeft,
            ArithmeticOperation representativeOperation,
            BigInteger representativeRight,
            BigInteger correctAnswer,
            string? enteredEquation)
    {
        string entered = NormalizeProportionEquation(enteredEquation);
        if (entered.Length == 0 || !entered.Contains('='))
        {
            return (false, EssayAnswerError.InvalidEquationFormat);
        }

        string symbol = representativeOperation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "-",
            ArithmeticOperation.Multiply => "×",
            ArithmeticOperation.Divide => "÷",
            _ => string.Empty
        };

        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            NormalizeProportionEquation(equationText),
            NormalizeProportionEquation(
                $"{representativeLeft} {symbol} {representativeRight} = {correctAnswer}")
        };

        return accepted.Contains(entered)
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongOperandsOrOperation);
    }

    private static string NormalizeMotionEquation(string? equationText) =>
        (equationText ?? string.Empty)
            .Trim()
            .Replace('x', '×')
            .Replace('X', '×')
            .Replace('*', '×')
            .Replace(':', '÷')
            .Replace('/', '÷')
            .Replace('−', '-')
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);

    private static string NormalizeProportionEquation(string? equationText) =>
        (equationText ?? string.Empty)
            .Trim()
            .Replace('x', '×')
            .Replace('X', '×')
            .Replace('*', '×')
            .Replace(':', '÷')
            .Replace('/', '÷')
            .Replace('−', '-')
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);

    private static string NormalizeGeometryEquation(
        string? equationText)
    {
        string normalized = (equationText ?? string.Empty)
            .Trim()
            .Replace('x', '×')
            .Replace('X', '×')
            .Replace('*', '×')
            .Replace('−', '-')
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace("\u202F", string.Empty, StringComparison.Ordinal);

        int firstEquals = normalized.IndexOf('=');
        int lastEquals = normalized.LastIndexOf('=');

        if (firstEquals > 0 &&
            firstEquals != lastEquals &&
            normalized[..firstEquals].All(char.IsLetter))
        {
            normalized = normalized[(firstEquals + 1)..];
        }

        return normalized;
    }

    private (bool IsCorrect, EssayAnswerError Error) ValidateEquation(
        ArithmeticQuizQuestion question,
        string? equationText)
    {
        Match match =
            EquationRegex().Match(
                equationText ?? string.Empty);

        if (!match.Success ||
            !TryParseInteger(
                match.Groups["left"].Value,
                out BigInteger enteredLeft) ||
            !TryParseInteger(
                match.Groups["right"].Value,
                out BigInteger enteredRight) ||
            !TryParseInteger(
                match.Groups["result"].Value,
                out BigInteger enteredResult) ||
            !TryParseOperation(
                match.Groups["operation"].Value[0],
                out ArithmeticOperation enteredOperation))
        {
            return (false, EssayAnswerError.InvalidEquationFormat);
        }

        IntegerArithmeticExpression expected =
            question.Expression;

        bool operandsMatch =
            enteredOperation == expected.Operation &&
            (enteredLeft == expected.LeftOperand &&
             enteredRight == expected.RightOperand ||
             IsCommutative(enteredOperation) &&
             enteredLeft == expected.RightOperand &&
             enteredRight == expected.LeftOperand);

        if (!operandsMatch)
        {
            return (false, EssayAnswerError.WrongOperandsOrOperation);
        }

        var enteredExpression =
            new IntegerArithmeticExpression(
                enteredLeft,
                enteredOperation,
                enteredRight);

        bool equationBalances;

        try
        {
            equationBalances =
                _engine.IsEquationCorrect(
                    enteredExpression,
                    enteredResult);
        }
        catch (DivideByZeroException)
        {
            equationBalances = false;
        }

        if (!equationBalances ||
            enteredResult != question.CorrectAnswer)
        {
            return (false, EssayAnswerError.WrongEquationResult);
        }

        return (true, EssayAnswerError.None);
    }

    private static (bool IsCorrect, EssayAnswerError Error) ValidateAnswer(
        ArithmeticQuizQuestion question,
        string? answerText)
    {
        Match match =
            AnswerRegex().Match(
                answerText ?? string.Empty);

        if (!match.Success ||
            !TryParseInteger(
                match.Groups["value"].Value,
                out BigInteger enteredAnswer))
        {
            return (false, EssayAnswerError.InvalidAnswerFormat);
        }

        if (enteredAnswer != question.CorrectAnswer)
        {
            return (false, EssayAnswerError.WrongAnswer);
        }

        string enteredUnit =
            NormalizeUnit(
                match.Groups["percent"].Success
                    ? "%"
                    : match.Groups["unit"].Value);

        if (!IsExpectedUnit(question, enteredUnit))
        {
            return (false, EssayAnswerError.WrongAnswerUnit);
        }

        return (true, EssayAnswerError.None);
    }

    private static bool TryParseInteger(
        string token,
        out BigInteger value)
    {
        value = BigInteger.Zero;

        string trimmed = token.Trim();

        if (!GroupedIntegerRegex().IsMatch(trimmed))
        {
            return false;
        }

        var normalized = new StringBuilder(trimmed.Length);

        foreach (char character in trimmed)
        {
            if (char.IsDigit(character) ||
                character == '+')
            {
                normalized.Append(character);
            }
        }

        return BigInteger.TryParse(
            normalized.ToString(),
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseOperation(
        char symbol,
        out ArithmeticOperation operation)
    {
        operation = symbol switch
        {
            '+' => ArithmeticOperation.Add,
            '-' or '−' => ArithmeticOperation.Subtract,
            '*' or '×' or 'x' or 'X' => ArithmeticOperation.Multiply,
            '/' or '÷' or ':' => ArithmeticOperation.Divide,
            _ => default
        };

        return symbol is '+' or '-' or '−' or
            '*' or '×' or 'x' or 'X' or
            '/' or '÷' or ':';
    }

    private static bool IsCommutative(
        ArithmeticOperation operation) =>
        operation is ArithmeticOperation.Add or
            ArithmeticOperation.Multiply;

    private static string NormalizeUnit(
        string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return string.Empty;
        }

        string normalized =
            unit.Trim()
                .TrimEnd('.', '!', '?', ':', ';')
                .ToLowerInvariant();

        normalized = Regex.Replace(
            normalized,
            @"\b(km|dm|cm|mm|m)\s*(?:\^\s*)?2\b",
            "$1²",
            RegexOptions.CultureInvariant);

        normalized = Regex.Replace(
            normalized,
            @"\b(km|dm|cm|mm|m)\s*(?:\^\s*)?3\b",
            "$1³",
            RegexOptions.CultureInvariant);

        return string.Join(
            ' ',
            normalized.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool UnitsMatch(
        string enteredUnit,
        string expectedUnit,
        string? problemText)
    {
        if (string.Equals(
                enteredUnit,
                expectedUnit,
                StringComparison.Ordinal))
        {
            return true;
        }

        if (MetricUnitsMatch(enteredUnit, expectedUnit))
        {
            return true;
        }

        // Từ chỉ loại tiếng Việt không làm thay đổi danh từ được đếm.
        // Ví dụ: cây bút = cái bút = chiếc bút; quy tắc vẫn giữ nguyên
        // phần tên cụ thể như bút chì, bút bi hoặc sổ tay.
        if (WordProblemUnitEquivalence
            .AreVietnameseUnitsEquivalent(
                enteredUnit,
                expectedUnit))
        {
            return true;
        }

        string singularEntered =
            NormalizeEnglishUnitToSingular(
                enteredUnit);

        string singularExpected =
            NormalizeEnglishUnitToSingular(
                expectedUnit);

        string unclassifiedEntered =
            RemoveVietnameseClassifier(
                singularEntered);

        string unclassifiedExpected =
            RemoveVietnameseClassifier(
                singularExpected);

        return string.Equals(
                   singularEntered,
                   singularExpected,
                   StringComparison.Ordinal) ||
               string.Equals(
                   unclassifiedEntered,
                   unclassifiedExpected,
                   StringComparison.Ordinal) ||
               expectedUnit.EndsWith(
                   $" {enteredUnit}",
                   StringComparison.Ordinal) ||
               enteredUnit.EndsWith(
                   $" {expectedUnit}",
                   StringComparison.Ordinal) ||
               IsContextualUnitExpansion(
                   singularEntered,
                   singularExpected,
                   problemText);
    }

    private static bool MetricUnitsMatch(
        string enteredUnit,
        string expectedUnit)
    {
        string? enteredSymbol = GetMetricUnitSymbol(
            enteredUnit, allowDescriptiveSuffix: false);
        return enteredSymbol is not null &&
               enteredSymbol == GetMetricUnitSymbol(
                   expectedUnit, allowDescriptiveSuffix: true);
    }

    private static string? GetMetricUnitSymbol(
        string unit,
        bool allowDescriptiveSuffix)
    {
        int spaceIndex = unit.IndexOf(' ');
        string unitName = spaceIndex < 0 ? unit : unit[..spaceIndex];
        string? symbol = unitName switch
        {
            "m" or "mét" or "met" or "meter" or "meters" or
                "metre" or "metres" => "m",
            "km" or "kilomet" or "kilômét" or "ki-lô-mét" or
                "kilometer" or "kilometers" or "kilometre" or
                "kilometres" => "km",
            "g" or "gam" or "gram" or "grams" or
                "gramme" or "grammes" => "g",
            "kg" or "kilogam" or "kilôgam" or "ki-lô-gam" or
                "kilogram" or "kilograms" or "kilogramme" or
                "kilogrammes" => "kg",
            "l" or "lít" or "lit" or "liter" or "liters" or
                "litre" or "litres" => "l",
            _ => null
        };

        if (spaceIndex < 0)
        {
            return symbol;
        }

        if (!allowDescriptiveSuffix || symbol is null)
        {
            return null;
        }

        string suffix = unit[(spaceIndex + 1)..];
        return suffix.StartsWith("vuông", StringComparison.Ordinal) ||
               suffix.StartsWith("khối", StringComparison.Ordinal) ||
               suffix.StartsWith("mỗi ", StringComparison.Ordinal) ||
               suffix.StartsWith("trên ", StringComparison.Ordinal) ||
               suffix.StartsWith("square ", StringComparison.Ordinal) ||
               suffix.StartsWith("cubic ", StringComparison.Ordinal) ||
               suffix.StartsWith("per ", StringComparison.Ordinal)
            ? null
            : symbol;
    }

    /// <summary>
    /// Cho phép cụm đơn vị trong đáp số cụ thể hơn hoặc khái quát hơn đơn vị
    /// chuẩn khi chính cụm đầy đủ có xuất hiện trong đề bài. Ví dụ đề có
    /// "cây rau", answer_unit là "cây" thì cả "cây" và "cây rau" đều đúng;
    /// "cây bút" vẫn sai vì không xuất hiện trong đề.
    /// </summary>
    private static bool IsContextualUnitExpansion(
        string enteredUnit,
        string expectedUnit,
        string? problemText)
    {
        if (string.IsNullOrWhiteSpace(problemText))
        {
            return false;
        }

        string expandedUnit;

        if (enteredUnit.StartsWith(
                $"{expectedUnit} ",
                StringComparison.Ordinal))
        {
            expandedUnit = enteredUnit;
        }
        else if (expectedUnit.StartsWith(
                     $"{enteredUnit} ",
                     StringComparison.Ordinal))
        {
            expandedUnit = expectedUnit;
        }
        else
        {
            return false;
        }

        string normalizedProblem =
            NormalizeComparisonText(problemText);

        return $" {normalizedProblem} ".Contains(
            $" {expandedUnit} ",
            StringComparison.Ordinal);
    }

    private static string NormalizeComparisonText(
        string value)
    {
        var normalized = new StringBuilder(value.Length);

        foreach (char character in value.ToLowerInvariant())
        {
            normalized.Append(
                char.IsLetterOrDigit(character) ||
                char.IsNumber(character) ||
                char.IsWhiteSpace(character)
                    ? character
                    : ' ');
        }

        return string.Join(
            ' ',
            normalized.ToString().Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Chuẩn hóa đơn vị tiếng Anh về số ít. Với cụm "... of ...", danh từ
    /// đếm được nằm ở đầu (sheets of paper); các cụm còn lại đổi từ cuối.
    /// Danh sách bất quy tắc chỉ bao phủ catalog toán đố để tránh suy diễn
    /// ngôn ngữ quá rộng trong validator.
    /// </summary>
    private static string NormalizeEnglishUnitToSingular(
        string value)
    {
        int ofIndex = value.IndexOf(
            " of ",
            StringComparison.Ordinal);

        if (ofIndex > 0)
        {
            return string.Concat(
                SingularizeEnglishWord(value[..ofIndex]),
                value[ofIndex..]);
        }

        int lastSpace = value.LastIndexOf(' ');

        if (lastSpace < 0)
        {
            return SingularizeEnglishWord(value);
        }

        return string.Concat(
            value[..(lastSpace + 1)],
            SingularizeEnglishWord(value[(lastSpace + 1)..]));
    }

    private static string SingularizeEnglishWord(
        string word)
    {
        string irregular =
            word switch
            {
                "cacti" => "cactus",
                "mice" => "mouse",
                "cookies" => "cookie",
                "brownies" => "brownie",
                "budgies" => "budgie",
                _ => string.Empty
            };

        if (irregular.Length > 0)
        {
            return irregular;
        }

        if (word.Length > 3 &&
            word.EndsWith(
                "ies",
                StringComparison.Ordinal))
        {
            return string.Concat(
                word[..^3],
                "y");
        }

        if (word.Length > 3 &&
            (word.EndsWith(
                 "sses",
                 StringComparison.Ordinal) ||
             word.EndsWith(
                 "xes",
                 StringComparison.Ordinal) ||
             word.EndsWith(
                 "zes",
                 StringComparison.Ordinal) ||
             word.EndsWith(
                 "ches",
                 StringComparison.Ordinal) ||
             word.EndsWith(
                 "shes",
                 StringComparison.Ordinal) ||
             word.EndsWith(
                 "oes",
                 StringComparison.Ordinal)))
        {
            return word[..^2];
        }

        return word.Length > 2 &&
               word.EndsWith('s') &&
               !word.EndsWith(
                   "ss",
                   StringComparison.Ordinal)
            ? word[..^1]
            : word;
    }

    private static string RemoveVietnameseClassifier(
        string value)
    {
        string[] classifiers =
        [
            "cái", "chiếc", "cây", "quyển", "cuốn", "quả", "trái",
            "con", "chú", "tờ", "viên", "bông", "hộp", "chai",
            "cục", "lọ", "hũ", "chậu", "tập", "bộ", "khối",
            "sợi", "thanh", "miếng", "tấm", "đoàn"
        ];

        foreach (string classifier in classifiers)
        {
            string prefix = $"{classifier} ";

            if (value.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return value[prefix.Length..];
            }
        }

        return value;
    }

    [GeneratedRegex(
        @"^\s*(?<left>\+?\d(?:[\d.,\u00A0\u202F ]*\d)?)\s*(?<operation>[+\-−×xX*÷/:])\s*(?<right>\+?\d(?:[\d.,\u00A0\u202F ]*\d)?)\s*=\s*(?<result>\+?\d(?:[\d.,\u00A0\u202F ]*\d)?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EquationRegex();

    [GeneratedRegex(
        @"^\+?(?:\d+|\d{1,3}(?:[.,\u00A0\u202F ]\d{3})+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GroupedIntegerRegex();

    [GeneratedRegex(
        @"^\s*(?:(?:đáp\s*số|answer)\s*:?)?\s*(?<value>\+?\d(?:[\d.,\u00A0\u202F ]*\d)?)(?:(?<percent>%)|\s*(?<unit>[^\d\s].*?))?\s*[.!]?\s*$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex AnswerRegex();

    [GeneratedRegex(
        @"^\s*(?<value>[+-]?\d+(?:\s*/\s*[+-]?\d+)?)\s*(?<unit>.*?)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FractionEquationResultRegex();
}
