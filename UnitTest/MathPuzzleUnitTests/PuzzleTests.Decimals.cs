using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckDecimalPresentationAndGrading()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var formats = new Dictionary<(AppLanguage, CurriculumTier, ElementaryQuizType), HashSet<bool>>();
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var generator = new ElementaryQuizGenerator(new Random(3801));
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Decimal))
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            foreach (bool? wordProblems in type is (ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract
                or ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
                ? new bool?[] { null, false, true } : new bool?[] { null })
            for (int sample = 0; sample < 12; sample++)
            {
                var question = wordProblems.HasValue
                    ? generator.GenerateDecimalArithmetic(mode, type, language, tier, wordProblems.Value)
                    : generator.Generate(mode, QuizProblemKind.Decimal, type, language, tier);
                var c = question.ElementaryProblem!;
                bool story = c.StoryContextId is not null;
                Require(!wordProblems.HasValue || story == wordProblems.Value,
                    "Decimal practice switched away from the requested numeric/word-problem format.");
                string result = c.Answers[0].DisplayValue ?? c.AnswerText;
                decimal Number(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
                string Text(decimal value) => value.ToString("0.################", CultureInfo.InvariantCulture);
                decimal a = Number(c.Facts[0]);
                if (c.IsDecimalArithmetic)
                {
                    if (!wordProblems.HasValue)
                    {
                        var key = (language, tier, type);
                        if (!formats.TryGetValue(key, out var seen)) formats[key] = seen = [];
                        seen.Add(story);
                    }
                    Require(c.IsNumericDecimalCalculation == !story,
                        "A decimal story must not be rendered as a bare calculation.");
                    Require(c.RequiresSolution == story && (c.Answers[0].Unit.Length > 0) == story,
                        "Decimal essay requirements do not match the question format.");
                    decimal b = Number(c.Facts[1]);
                    decimal expected = type switch
                    {
                        ElementaryQuizType.DecimalAdd => a + b,
                        ElementaryQuizType.DecimalSubtract => a - b,
                        ElementaryQuizType.DecimalMultiply => a * b,
                        _ => a / b
                    };
                    if (story && (int)tier >= 3)
                    {
                        var g = c.Reasoning!.Givens.ToDictionary(given => given.Role, given => Number(given.Value));
                        decimal first = g["quantity"];
                        if (tier == CurriculumTier.FiveStars) first /= c.Answers[0].Unit == "m" ? 100 : 1000;
                        expected = type switch
                        {
                            ElementaryQuizType.DecimalAdd => first * 2 + g["difference"],
                            ElementaryQuizType.DecimalSubtract => first - g["used-first"] - g["used-second"],
                            ElementaryQuizType.DecimalMultiply => (first + g["extra-quantity"]) * g["portion-count"],
                            _ => (first + g["extra-quantity"]) / g["portion-count"]
                        };
                        expected += g.GetValueOrDefault("adjustment");
                    }
                    Require(Number(result) == expected, "Decimal answer differs from independent arithmetic.");
                    string symbol = type switch
                    {
                        ElementaryQuizType.DecimalAdd => "+",
                        ElementaryQuizType.DecimalSubtract => "−",
                        ElementaryQuizType.DecimalMultiply => "×",
                        _ => "÷"
                    };
                    string displayed = story ? "" : c.FormatDecimalCalculation(mode == ArithmeticQuizMode.TrueFalse ? c.PresentedText! : "?");
                    if (!story) {
                    Require(displayed.StartsWith($"{c.Facts[0]} {symbol} {c.Facts[1]} = ", StringComparison.Ordinal),
                        "Decimal calculation presentation changed operands or operation.");
                    Require(displayed.EndsWith(mode == ArithmeticQuizMode.TrueFalse ? c.PresentedText! : "?", StringComparison.Ordinal),
                        "Decimal presentation reveals the answer or loses the true/false proposal.");
                    }
                    string Calculation(string answer) => QuizMathExpressionFormatter.Format(c.Answers[0].Expression) + " = " + answer;
                    if (story)
                    {
                        Require(c.ProblemText.Contains(c.Facts[0]) && c.ProblemText.Contains(c.Facts[1])
                            && c.ProblemText.Contains(c.Answers[0].Unit), "Decimal story is missing given quantities or units.");
                        Require(!grader.Validate(question, null, Calculation(c.AnswerText), c.AnswerText).IsCorrect,
                            "A decimal story must still require a solution sentence.");
                    }
                    string solution = story ? c.Answers[0].Label + ":" : "";
                    string equation = Calculation(c.AnswerText);
                    if (language == AppLanguage.Vietnamese) equation = equation.Replace('.', ',');
                    Require(grader.Validate(question, solution, equation, c.AnswerText).IsCorrect,
                        $"Valid decimal work rejected: {type}/{language}/{tier}/{story}.");
                    Require(!grader.Validate(question, solution, null, c.AnswerText).IsCorrect,
                        "Decimal arithmetic must require a calculation even when no solution sentence is needed.");
                    if ((!story || (int)tier <= 2) && type is (ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalMultiply))
                        Require(grader.Validate(question, solution, $"{c.Facts[1]} {symbol} {c.Facts[0]} = {c.AnswerText}", c.AnswerText).IsCorrect,
                            "Decimal addition/multiplication must preserve commutative grading.");
                    Require(!grader.Validate(question, solution, Calculation(Text(expected + 0.1m)), c.AnswerText).IsCorrect,
                        "A correct final answer must not hide a wrong decimal calculation.");
                    if (story)
                        Require(!grader.Validate(question, solution, Calculation(result + " cm"), result + " cm").IsCorrect,
                            "A decimal story must reject the wrong measurement unit.");
                }
                else
                {
                    Require(!story && !c.IsNumericDecimalCalculation && !c.RequiresSolution
                        && c.Answers[0].Unit.Length == 0, "Rounding/comparison incorrectly became a word problem.");
                    if (type == ElementaryQuizType.DecimalRound)
                        Require(Number(result) == decimal.Round(a, c.RoundingDecimalPlaces!.Value, MidpointRounding.AwayFromZero),
                            "Rounding differs from the specified rule.");
                    else
                    {
                        decimal b = Number(c.Facts[1]);
                        Require(c.AnswerText == (a < b ? "<" : a > b ? ">" : "="), "Wrong decimal comparison.");
                    }
                    Require(grader.Validate(question, null, null, c.AnswerText).IsCorrect,
                        "Decimal rounding/comparison should accept a direct answer without invented calculations.");
                    string wrongAnswer = type == ElementaryQuizType.DecimalRound ? Text(Number(result) + 1)
                        : c.AnswerText == "<" ? ">" : "<";
                    Require(!grader.Validate(question, null, null, wrongAnswer).IsCorrect,
                        "Decimal rounding/comparison accepted a wrong direct answer.");
                }
                var parts = EssayCombinedInputParser.Parse(c.SolutionText, c.RequiresSolution, true);
                Require(grader.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    "Decimal model solution no longer passes grading.");
                Require(c.ChoiceTexts!.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1
                    && ElementaryEssayValidator.CheckAnswers(question, c.PresentedText) == question.PresentedEquationIsCorrect,
                    "Decimal choices or true/false proposal do not match grading.");
                count++;
            }
        }
        Require(formats.Count == 40 && formats.Values.All(seen => seen.Count == 2),
            "Every decimal arithmetic subtype must retain numeric and story practice at all five stars.");
        Console.WriteLine($"  Checked {count} decimal questions across all six subtypes, explicit numeric/story practice, five stars, both languages and all answer modes.");
    }
}
