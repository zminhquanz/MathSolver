using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Accepts a comparison sign or a complete comparison of the given values.</summary>
internal static partial class ComparisonEssayValidator
{
    internal static EssayAnswerValidationResult Validate(ElementaryQuizContract contract,
        string? equations, string? answerText)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string expected = contract.Answers[0].Text!;
        var (left, right) = contract.ComparisonOperands;
        var details = new List<string>();
        bool hasWork = !string.IsNullOrWhiteSpace(equations);
        bool hasAnswer = !string.IsNullOrWhiteSpace(answerText);
        bool equationOkay = Check(equations, QuizContentCatalog.Text(contract.Language, "ComparisonEssayValidator.Validate.001"));
        bool answerOkay = hasAnswer ? Check(answerText, QuizContentCatalog.Text(contract.Language, "ComparisonEssayValidator.Validate.002")) : hasWork && equationOkay;
        if (!hasWork && !hasAnswer)
            details.Add(QuizContentCatalog.Text(contract.Language, "ComparisonEssayValidator.Validate.003"));
        return new(true, equationOkay, answerOkay, EssayAnswerError.None,
            equationOkay ? EssayAnswerError.None : EssayAnswerError.WrongOperandsOrOperation,
            answerOkay ? EssayAnswerError.None : hasWork || hasAnswer ? EssayAnswerError.WrongAnswer : EssayAnswerError.InvalidAnswerFormat)
            { Details = details };

        bool Check(string? text, string section)
        {
            bool correct = true;
            string[] lines = (text ?? "").Split(['\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 && !string.IsNullOrWhiteSpace(text))
            {
                details.Add(QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "ComparisonEssayValidator.Validate.004", ("section", $"{section}")));
                return false;
            }
            foreach (string line in lines)
            {
                if (line == expected) continue;
                Match match = ComparisonRegex().Match(line);
                if (match.Success)
                {
                    string enteredLeft = match.Groups["left"].Value.Trim();
                    string enteredRight = match.Groups["right"].Value.Trim();
                    string symbol = match.Groups["symbol"].Value;
                    bool sameOrder = SameValue(enteredLeft, left) && SameValue(enteredRight, right);
                    bool reverseOrder = SameValue(enteredLeft, right) && SameValue(enteredRight, left);
                    string reversed = expected == "<" ? ">" : expected == ">" ? "<" : "=";
                    if (sameOrder && symbol == expected || reverseOrder && symbol == reversed) continue;
                    if (!sameOrder && !reverseOrder)
                        details.Add(QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "ComparisonEssayValidator.Validate.005", ("section", $"{section}"), ("line", $"{line}"), ("left", $"{left}"), ("right", $"{right}")));
                    else
                        details.Add(QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "ComparisonEssayValidator.Validate.006", ("section", $"{section}"), ("symbol", $"{symbol}"), ("sameOrder_expected_reversed", $"{(sameOrder ? expected : reversed)}")));
                }
                else
                    details.Add(QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "ComparisonEssayValidator.Validate.007", ("section", $"{section}"), ("line", $"{line}"), ("contract_FormatComparison_expected", $"{contract.FormatComparison(expected)}")));
                correct = false;
            }
            return correct;
        }
    }

    private static bool SameValue(string entered, string expected) => entered.Length <= 4096
        && EssayCalculationEvaluator.TryEvaluate(entered, out var value, out _, true)
        && EssayCalculationEvaluator.TryEvaluate(expected, out var given, out _, true) && value == given;

    [GeneratedRegex(@"^(?<left>[^<>=]+)(?<symbol>[<>=])(?<right>[^<>=]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ComparisonRegex();

}
