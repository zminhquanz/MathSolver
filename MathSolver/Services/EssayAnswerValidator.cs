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
}

/// <summary>
/// Chấm đủ ba phần của bài tự luận: câu lời giải, phép tính và đáp số. Câu lời
/// giải không cần trùng từng chữ với mẫu, nhưng bắt buộc phải có và phải nêu
/// đúng đại lượng/đơn vị mà đề bài yêu cầu.
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

        (bool solutionIsCorrect, EssayAnswerError solutionError) =
            ValidateSolution(
                question,
                solutionText);

        (bool equationIsCorrect, EssayAnswerError equationError) =
            question.FractionProblem is FractionQuizContract fraction
                ? ValidateFractionEquation(
                    fraction,
                    equationText)
                : question.GeometryProblem is GeometryQuizContract geometry
                ? ValidateGeometryEquation(
                    geometry,
                    equationText)
                : ValidateEquation(
                    question,
                    equationText);

        (bool answerIsCorrect, EssayAnswerError answerError) =
            question.FractionProblem is FractionQuizContract fractionAnswer
                ? ValidateFractionAnswer(
                    question,
                    fractionAnswer,
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
            answerError);
    }

    private static (bool IsCorrect, EssayAnswerError Error)
        ValidateSolution(
            ArithmeticQuizQuestion question,
            string? solutionText)
    {
        // Nguồn Thuật toán không hiển thị ô lời giải bằng câu văn. Chỉ bài
        // toán đố do AI tạo mới bắt buộc học sinh điền phần này.
        if (question.WordProblem is not MathWordProblem wordProblem)
        {
            return (true, EssayAnswerError.None);
        }

        string solution =
            NormalizeComparisonText(
                solutionText ?? string.Empty);

        if (solution.Length == 0)
        {
            return (false, EssayAnswerError.MissingSolution);
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

        string expectedUnit =
            NormalizeUnit(
                wordProblem.AnswerUnit);

        bool mentionsExpectedQuantity =
            WordProblemUnitEquivalence.ContainsVietnameseUnit(
                solution,
                expectedUnit) ||
            ContainsNormalizedPhrase(
                solution,
                NormalizeEnglishUnitToSingular(expectedUnit));

        return mentionsExpectedQuantity
            ? (true, EssayAnswerError.None)
            : (false, EssayAnswerError.WrongSolutionContent);
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
            GeometryMeasurement.Volume =>
                ["thể tích", "volume"],
            _ => []
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
            FractionQuizContract contract,
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

        if (entered != contract.CorrectAnswer)
        {
            return (false, EssayAnswerError.WrongAnswer);
        }

        string expectedUnit = NormalizeUnit(question.WordProblem?.AnswerUnit);
        string enteredUnit = NormalizeUnit(match.Groups["unit"].Value);
        if (expectedUnit.Length > 0 &&
            (enteredUnit.Length == 0 ||
             !UnitsMatch(
                 enteredUnit,
                 expectedUnit,
                 question.WordProblem?.ProblemText)))
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
                match.Groups["unit"].Value);

        string expectedUnit =
            NormalizeUnit(
                question.WordProblem?.AnswerUnit);

        // Bài toán đố có đơn vị thì đáp số phải ghi đúng đơn vị như một bài
        // giải tiểu học. Câu do thuật toán tạo chỉ là biểu thức nên không ép
        // đơn vị khi WordProblem không tồn tại.
        if (expectedUnit.Length > 0 &&
            (enteredUnit.Length == 0 ||
             !UnitsMatch(
                 enteredUnit,
                 expectedUnit,
                 question.WordProblem?.ProblemText)))
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
        @"^\s*(?:(?:đáp\s*số|answer)\s*:?)?\s*(?<value>\+?\d(?:[\d.,\u00A0\u202F ]*\d)?)(?:\s+(?<unit>.*?))?\s*[.!]?\s*$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex AnswerRegex();
}
