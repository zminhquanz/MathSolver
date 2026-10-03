using MathSolver.Models;
using MathSolver.Services;
using System.Numerics;
using static MathSolver.Services.TextbookFractionParser;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckFractionPresentation()
    {
        ReducedFraction Evaluate(Node node) => node switch
        {
            Number number => new(BigInteger.Parse(number.Text.Replace(",", "")), 1),
            Group group => Evaluate(group.Content),
            Unary unary => new((unary.Operator == "+" ? 1 : -1) * Evaluate(unary.Content).Numerator, Evaluate(unary.Content).Denominator),
            Mixed mixed => new(BigInteger.Parse(mixed.Whole.Text) * BigInteger.Parse(mixed.Denominator.Text) + BigInteger.Parse(mixed.Numerator.Text), BigInteger.Parse(mixed.Denominator.Text)),
            Binary binary => Apply(binary.Operator, Evaluate(binary.Left), Evaluate(binary.Right)),
            _ => throw new InvalidOperationException()
        };
        ReducedFraction Apply(string op, ReducedFraction a, ReducedFraction b) => op switch
        {
            "+" => new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator),
            "-" or "−" => new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator),
            "*" or "×" => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator),
            "/" or "÷" => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator),
            _ => throw new InvalidOperationException()
        };
        foreach (var (text, expected) in new (string, ReducedFraction)[]
        {
            ("(1/2+1/3)", new(5, 6)), ("(40+2)/(45-3)", new(1, 1)),
            ("80*(1-1/4)*7/8", new(105, 2)), ("80/((1-1/4)*7/8)", new(2560, 21)),
            ("(17/8+1/9-1/10)", new(769, 360)), ("6/8", new(3, 4)),
            ("2 1/3", new(7, 3)), ("-2 1/3", new(-7, 3)), ("1 / 2 + 1 / 3", new(5, 6)),
            ("-3/4", new(-3, 4)), ("(4×6)/(5×7)", new(24, 35))
        })
        {
            Require(TryParse(text, out var parsed) && ContainsFraction(parsed!) && Evaluate(parsed!) == expected,
                "Textbook fraction layout changed the expression: " + text);
            var fragments = ParseLine("A. “" + text + "”.");
            Require(fragments.Count(fragment => fragment.Math is not null) == 1 &&
                string.Concat(fragments.Select(fragment => fragment.Text)) == "A. “" + text + "”.",
                "Display segmentation lost punctuation or a compact/grouped fraction.");
        }
        Require(TryParse("6/8", out var unreduced) && unreduced is Binary { Left: Number { Text: "6" }, Right: Number { Text: "8" } },
            "Display must preserve unreduced operands rather than silently solve the exercise.");
        Require(TryParse("1/2 ? (1/3+1/4)", out var comparison) && comparison is Binary { Operator: "?" },
            "Unanswered fraction comparisons must retain their placeholder.");
        Require(ParseLine("Ngày 03/10/2026, vận tốc 10 m/s.").All(fragment => fragment.Math is null),
            "Dates and unit slashes must remain plain text.");
        foreach (string paragraph in new[] { "Giá trị (phân số 1/2+1/3).", "Đáp số “Phân số”: em ghi 2/3; đúng là 3/4.",
            "Phép tính: (1/2+1/3)=7/6; kết quả chưa đúng." })
        {
            var parts = ParseLine(paragraph);
            Require(parts.Any(part => part.Math is not null) && string.Concat(parts.Select(part => part.Text)) == paragraph,
                "Parenthetical prose or grading feedback lost its fractions/punctuation.");
        }
        Require(!TryParse("(1/2+)", out _) && !TryParse("1//2", out _) &&
            !TryParse(new string('(', 40) + "1/2" + new string(')', 40), out _),
            "Incomplete or excessive input must fail gracefully.");
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.FractionSkills))
        for (int seed = 0; seed < 12; seed++)
        {
            var contract = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.FractionSkills, type, language, tier).ElementaryProblem!;
            foreach (var step in contract.Reasoning!.Steps)
            {
                Require(TryParse(step.Expression, out var node) && Evaluate(node!) == step.Value,
                    $"{type}/{tier}: rendered numerator/denominator changes the calculation.");
                if (step.Expression.Contains('/'))
                    Require(ParseLine(step.Expression + " = " + step.DisplayValue).Any(fragment => fragment.Math is not null),
                        "A sample equation fell back to slash notation: " + step.Expression);
            }
            foreach (string text in new[] { contract.ProblemText, contract.SolutionText, contract.AnswerText,
                contract.PresentedText ?? "", contract.IsComparison ? contract.FormatComparison("?") : "" }.Concat(contract.ChoiceTexts!))
            foreach (string line in text.Split('\n'))
            {
                var fragments = ParseLine(line);
                Require(string.Concat(fragments.Select(fragment => fragment.Text)) == line,
                    "Fraction display lost story wording, answer labels or units.");
                foreach (var fragment in fragments.Where(fragment => fragment.Math is null))
                    Require(!System.Text.RegularExpressions.Regex.IsMatch(fragment.Text, @"\d\s*/\s*\d"),
                        $"{type}/{tier}: fraction not recognized in display: {line}");
            }
            count++;
        }
        Console.WriteLine($"  Checked {count} bilingual fraction-skill presentations, exact display trees, compact/grouped fractions, mixed numbers, choices, feedback syntax and punctuation.");
    }
}
