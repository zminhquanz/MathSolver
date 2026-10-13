using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckLargeNumberSense()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        var types = new[] { ElementaryQuizType.ReadNumber, ElementaryQuizType.WriteNumber, ElementaryQuizType.PlaceValue,
            ElementaryQuizType.AdjacentNumbers, ElementaryQuizType.NumberLine, ElementaryQuizType.Parity,
            ElementaryQuizType.OrderNumbers, ElementaryQuizType.RoundWholeNumber, ElementaryQuizType.EstimateSum,
            ElementaryQuizType.LetterExpression };
        int cases = 0; bool hundreds = false, small = false, upperBoundary = false;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            foreach (var row in QuizContentCatalog.LoadList<QuizNumberName>("Foundation.NumberNames.Extensions", QuizContentCatalog.Culture(language)))
            {
                Require(PracticeNumberNames.Format(row.Value, language) == row.Words, "Algorithm disagrees with reviewed vocabulary: " + row.Value);
                upperBoundary |= row.Value == PracticeNumberNames.Maximum;
            }
            foreach (var (value, vi, en) in new[] {
                (0, "không", "zero"), (15, "mười lăm", "fifteen"), (21, "hai mươi mốt", "twenty-one"),
                (105, "một trăm linh năm", "one hundred and five"),
                (1001, "một nghìn không trăm linh một", "one thousand and one"),
                (1_001_001, "một triệu không trăm linh một nghìn không trăm linh một", "one million one thousand and one") })
                Require(PracticeNumberNames.Format(value, language) == (language == AppLanguage.Vietnamese ? vi : en), "Incorrect zero, teen or class reading.");
            for (int seed = 0; seed < 100; seed++)
                foreach (int cap in new[] { 1, 10, 1_000_000, 100_000_000, 999_999_999 })
                    Require(PracticeNumberNames.Next(new Random(seed), CurriculumTier.FiveStars, cap) <= cap, "Zero-class sampling escaped its boundary.");
            foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            foreach (var type in types)
            for (int seed = 0; seed < 100; seed++)
            {
                var q = new ElementaryQuizGenerator(new Random(seed), expandedNumberPractice: true).Generate(mode, QuizProblemKind.NumberSense, type, language, tier);
                var c = q.ElementaryProblem!;
                int[] numbers = Regex.Matches(c.ProblemText, @"\d+", RegexOptions.CultureInvariant).Select(m => int.Parse(m.Value)).ToArray();
                Require(numbers.All(n => n <= PracticeNumberNames.Maximum), "A number-sense fact exceeds nine digits.");
                hundreds |= numbers.Any(n => n >= 100_000_000);
                small |= numbers.Any(n => n is > 0 and < 1000);
                upperBoundary |= numbers.Contains(999_999_999) || c.Answers.Any(a => a.Value.Numerator == 999_999_999);
                Require(c.Answers.All(a => a.Value.Numerator >= 0 && a.Value.Numerator <= PracticeNumberNames.Maximum), "Number-sense answer escaped the limit.");
                if (type == ElementaryQuizType.AdjacentNumbers)
                    Require(c.Answers[0].Value.Numerator == numbers[0] - 1 && c.Answers[1].Value.Numerator == numbers[0] + 1, "Adjacent-number error.");
                if (type == ElementaryQuizType.RoundWholeNumber)
                    Require(c.Answers[0].Value.Numerator == Round(numbers[0], numbers[1]), "Rounding did not follow the requested place.");
                if (type == ElementaryQuizType.EstimateSum)
                    Require(c.Answers[0].Value.Numerator == Round(numbers[0], numbers[2]) + Round(numbers[1], numbers[2]), "Estimation rounded the sum instead of each addend.");
                if (type == ElementaryQuizType.NumberLine)
                {
                    var visual = c.Visual!; int hidden = visual.HiddenValueIndices!.Single();
                    Require(visual.Values.All(n => n <= PracticeNumberNames.Maximum) && (decimal)c.Answers[0].Value.Numerator == visual.Values[hidden], "Large number line lost its hidden value.");
                }
                if (type == ElementaryQuizType.PlaceValue)
                {
                    var grammar = PracticeNumberNames.Grammar(language);
                    int power = Enumerable.Range(0, 9).OrderByDescending(p => grammar.PlaceNames[p].Length)
                        .First(p => c.ProblemText.Contains(grammar.PlaceNames[p], StringComparison.Ordinal));
                    int place = (int)Math.Pow(10, power);
                    Require(c.Answers[0].Value.Numerator == numbers[0] / place % 10 * place, "Incorrect digit value at a large place.");
                }
                if (type == ElementaryQuizType.OrderNumbers)
                {
                    int[] ordered = c.Answers[0].Text!.Split(" -> ").Select(int.Parse).ToArray();
                    bool ascending = c.ProblemText.Contains(QuizContentCatalog.Text(language, "Foundation.Ascending"));
                    Require(ordered.SequenceEqual(ascending ? numbers.Order() : numbers.OrderDescending()), "Ordering direction or original numbers changed.");
                }
                if (mode == ArithmeticQuizMode.Essay)
                {
                    string correct = c.Answers.Count == 1 ? ElementaryQuizContract.FormatAnswer(c.Answers[0]) : c.AnswerText;
                    Require(grader.ValidateSubmission(q, correct).IsCorrect, $"Large number answer rejected: {type}/{language}: {correct}");
                    Require(!grader.ValidateSubmission(q, type == ElementaryQuizType.ReadNumber ? "incorrect reading" : "-1").IsCorrect, "Accepted an incorrect large-number answer.");
                    if (type == ElementaryQuizType.ReadNumber)
                        foreach (var alias in c.Answers[0].Aliases ?? []) Require(grader.ValidateSubmission(q, alias).IsCorrect, "Reviewed reading variant rejected.");
                }
                cases++;
            }
        }
        Require(hundreds && small && upperBoundary, "Large number practice lost upper boundaries or smaller facts.");
        var engine = new BasicArithmeticEngine();
        var validator = new ArithmeticQuizValidator(engine);
        var expression = new IntegerArithmeticExpression(999_999_999, ArithmeticOperation.Multiply, 99);
        BigInteger product = engine.CalculateInteger(expression).Result;
        Require(product == 98_999_999_901, "The underlying exact arithmetic overflowed.");
        Require(!validator.Validate(new(expression, ArithmeticQuizMode.Essay, product, null, null, [])).IsValid,
            "The bank's existing multiplication boundary changed.");
        foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
            for (int seed = 0; seed < 300; seed++)
            {
                var q = new ArithmeticQuizGenerator(engine, new Random(seed)).Generate(ArithmeticQuizMode.Essay, ArithmeticOperation.Multiply, new(tier, false), expandedPractice: true);
                Require(q.CorrectAnswer <= PracticeArithmeticDifficulty.Maximum(tier, q.ArithmeticStyle), "Practice multiplication escaped its bounded product.");
                foreach (var operation in new[] { ArithmeticOperation.Add, ArithmeticOperation.Subtract })
                {
                    var arithmetic = new ArithmeticQuizGenerator(engine, new Random(seed)).Generate(ArithmeticQuizMode.Essay, operation, new(tier, false), expandedPractice: true);
                    Require(PracticeArithmeticDifficulty.HasAppropriateRegrouping((int)arithmetic.Expression.LeftOperand,
                        (int)arithmetic.Expression.RightOperand, operation, tier), "Four-star practice generated excessive or adjacent carries/borrows.");
                }
                var compare = new ElementaryQuizGenerator(new Random(seed), expandedNumberPractice: true)
                    .GenerateComparison(ArithmeticQuizMode.Essay, QuizProblemKind.Arithmetic, AppLanguage.Vietnamese, tier);
                Require(compare.ElementaryProblem!.Facts.All(f => BigInteger.Parse(f, CultureInfo.InvariantCulture) <= PracticeNumberNames.Maximum),
                    "Comparison exceeded the number-class scope.");
            }
        Console.WriteLine($"Checked {cases} nine-digit number-sense questions, bilingual readings, zero classes, aliases, rounding, ordering, lines and exact product boundaries.");

        static long Round(int value, int place) => ((long)value + place / 2) / place * place;
    }
}
