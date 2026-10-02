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
        bool equationOkay = Check(equations, vi ? "So sánh" : "Comparison");
        bool answerOkay = hasAnswer ? Check(answerText, vi ? "Đáp số" : "Answer") : hasWork && equationOkay;
        if (!hasWork && !hasAnswer)
            details.Add(vi ? "Chưa có đáp án. Hãy ghi dấu >, <, = hoặc cả dòng so sánh."
                : "No answer supplied. Enter >, <, = or the complete comparison.");
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
                details.Add(vi ? $"{section}: chưa có dấu so sánh hợp lệ." : $"{section}: no valid comparison sign supplied.");
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
                        details.Add(vi ? $"{section}: hai giá trị trong “{line}” chưa khớp dữ kiện {left} và {right}."
                            : $"{section}: the values in “{line}” do not match the given {left} and {right}.");
                    else
                        details.Add(vi ? $"{section}: dấu “{symbol}” chưa đúng; cần dấu “{(sameOrder ? expected : reversed)}”."
                            : $"{section}: “{symbol}” is incorrect; use “{(sameOrder ? expected : reversed)}”.");
                }
                else
                    details.Add(vi ? $"{section}: em ghi “{line}”; cần {contract.FormatComparison(expected)}."
                        : $"{section}: you wrote “{line}”; expected {contract.FormatComparison(expected)}.");
                correct = false;
            }
            return correct;
        }
    }

    private static bool SameValue(string entered, string expected) => NumericValueRegex().IsMatch(entered)
        && EssayCalculationEvaluator.TryEvaluate(entered, out var value, out _, true)
        && EssayCalculationEvaluator.TryEvaluate(expected, out var given, out _, true) && value == given;

    [GeneratedRegex(@"^(?<left>[^<>=]+)(?<symbol>[<>=])(?<right>[^<>=]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ComparisonRegex();

    [GeneratedRegex(@"^[+\-−]?\d+(?:[.,]\d+)?(?:\s*/\s*[+\-−]?\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericValueRegex();
}
