using MathSolver.Services;
using System.Runtime.ExceptionServices;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckFractionLivePreview()
    {
        string[] expressions =
        [
            "(1/2 + 1/3) × 3/4",
            "{[3/4 − (1/2 + 1/8)]} ÷ 1/4",
            "-3/4 + (2/5)",
            "(4×6)/(5×7)",
            "2 1/3 × 2/5",
            "1/2 ÷ 3/4 = 2/3"
        ];
        var exceptions = new List<string>();
        EventHandler<FirstChanceExceptionEventArgs> onException = (_, e) =>
        {
            if (e.Exception is FormatException &&
                e.Exception.StackTrace?.Contains(nameof(TextbookFractionParser), StringComparison.Ordinal) == true)
                exceptions.Add(e.Exception.StackTrace);
        };
        AppDomain.CurrentDomain.FirstChanceException += onException;
        try
        {
            foreach (string expression in expressions)
            for (int length = 0; length <= expression.Length; length++)
            {
                // All insertion/deletion prefixes can reach the preview on TextChanged.
                string input = expression[..length];
                bool valid = TextbookFractionParser.TryParse(input, out var node);
                Require(valid || node is null, "A failed parse retained a partial display tree.");
                var fragments = TextbookFractionParser.ParseLine(input);
                Require(string.Concat(fragments.Select(fragment => fragment.Text)) == input,
                    "Live preview lost characters while the user was typing.");
            }
            foreach (string invalid in new[] { "", "(", "1/", "1//2", "(1/2 +)", "[1/2 + 1/3)",
                new string('(', 40) + "1/2" + new string(')', 40), new string('-', 40) + "1/2", new string('1', 4100) })
                Require(!TextbookFractionParser.TryParse(invalid, out var node) && node is null,
                    "Malformed or excessive preview input was accepted.");
            Require(exceptions.Count == 0,
                $"Typing ordinary/incomplete fractions threw {exceptions.Count} first-chance FormatException(s).");
            foreach (string expression in expressions)
            {
                Require(TextbookFractionParser.TryParse(expression, out _), "Valid expression could not be rendered: " + expression);
                Require(TextbookFractionParser.ParseLine(expression).Any(fragment => fragment.Math is not null),
                    "A complete expression lost textbook fraction formatting: " + expression);
            }
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= onException; }
        Console.WriteLine("  Typed/deleted fraction prefixes and invalid input produced no first-chance formatting exceptions.");
    }
}
