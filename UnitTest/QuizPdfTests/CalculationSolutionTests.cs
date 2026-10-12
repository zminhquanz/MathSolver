using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class CalculationSolutionTests
{
    internal static async Task Run(PracticeQuestionFactory factory, string directory)
    {
        var simple = new ArithmeticQuizQuestion(new(1, ArithmeticOperation.Add, 1), ArithmeticQuizMode.Essay, 2, null, null, []);
        Check(QuizWorksheetBuilder.Format(simple, AppLanguage.Vietnamese).Solution == "1 + 1 = 2", "Simple arithmetic formatting.");
        var fixtures = new List<ArithmeticQuizQuestion> { simple };
        foreach (var (known, result, operation, left, answer, inverse, expected) in new[]
        {
            (1, 2, ArithmeticOperation.Add, true, 1, new IntegerArithmeticExpression(2, ArithmeticOperation.Subtract, 1), "x + 1 = 2\nx = 2 − 1\nx = 1"),
            (1, 2, ArithmeticOperation.Add, false, 1, new IntegerArithmeticExpression(2, ArithmeticOperation.Subtract, 1), "1 + x = 2\nx = 2 − 1\nx = 1"),
            (3, 5, ArithmeticOperation.Subtract, true, 8, new IntegerArithmeticExpression(5, ArithmeticOperation.Add, 3), "x − 3 = 5\nx = 5 + 3\nx = 8"),
            (8, 5, ArithmeticOperation.Subtract, false, 3, new IntegerArithmeticExpression(8, ArithmeticOperation.Subtract, 5), "8 − x = 5\nx = 8 − 5\nx = 3"),
            (3, 12, ArithmeticOperation.Multiply, true, 4, new IntegerArithmeticExpression(12, ArithmeticOperation.Divide, 3), "x × 3 = 12\nx = 12 ÷ 3\nx = 4"),
            (3, 12, ArithmeticOperation.Multiply, false, 4, new IntegerArithmeticExpression(12, ArithmeticOperation.Divide, 3), "3 × x = 12\nx = 12 ÷ 3\nx = 4"),
            (3, 4, ArithmeticOperation.Divide, true, 12, new IntegerArithmeticExpression(4, ArithmeticOperation.Multiply, 3), "x ÷ 3 = 4\nx = 4 × 3\nx = 12"),
            (12, 3, ArithmeticOperation.Divide, false, 4, new IntegerArithmeticExpression(12, ArithmeticOperation.Divide, 3), "12 ÷ x = 3\nx = 12 ÷ 3\nx = 4")
        })
        {
            var question = simple with { Expression = inverse, CorrectAnswer = answer,
                FindXProblem = new(known, result, operation, left, answer, inverse) };
            Check(QuizWorksheetBuilder.Format(question, AppLanguage.Vietnamese).Solution == expected, "Find x inverse step or operand order.");
            fixtures.Add(question);
        }

        foreach (var (left, right, operation, answer, conversion, calculation) in new[]
        {
            (new ReducedFraction(1, 2), new ReducedFraction(1, 3), FractionOperation.Add, new ReducedFraction(5, 6),
                "1/2 = (1 × 3)/(2 × 3) = 3/6", "1/2 + 1/3 = 3/6 + 2/6 = (3 + 2)/6 = 5/6"),
            (new ReducedFraction(5, 6), new ReducedFraction(1, 4), FractionOperation.Subtract, new ReducedFraction(7, 12),
                "1/4 = (1 × 3)/(4 × 3) = 3/12", "5/6 − 1/4 = 10/12 − 3/12 = (10 − 3)/12 = 7/12"),
            (new ReducedFraction(1, 3), new ReducedFraction(1, 3), FractionOperation.Add, new ReducedFraction(2, 3),
                "", "1/3 + 1/3 = (1 + 1)/3 = 2/3"),
            (new ReducedFraction(1, 6), new ReducedFraction(1, 3), FractionOperation.Add, new ReducedFraction(1, 2),
                "1/3 = (1 × 2)/(3 × 2) = 2/6", "1/6 + 1/3 = 1/6 + 2/6 = (1 + 2)/6 = 3/6 = 1/2"),
            (new ReducedFraction(1, 2), new ReducedFraction(1, 2), FractionOperation.Subtract, new ReducedFraction(0, 1),
                "", "1/2 − 1/2 = (1 − 1)/2 = 0/2 = 0"),
            (new ReducedFraction(2, 3), new ReducedFraction(3, 4), FractionOperation.Multiply, new ReducedFraction(1, 2),
                "", "2/3 × 3/4 = 1/2"),
            (new ReducedFraction(2, 3), new ReducedFraction(1, 6), FractionOperation.Divide, new ReducedFraction(4, 1),
                "", "2/3 ÷ 1/6 = 4")
        })
        {
            var question = simple with { FractionProblem = new(left, operation, right, answer, null, []) };
            string solution = QuizWorksheetBuilder.Format(question, AppLanguage.Vietnamese).Solution;
            Check(solution.Contains(calculation), "Fraction arithmetic or reduction missing.");
            Check(conversion.Length == 0 ? !solution.Contains("×") || operation == FractionOperation.Multiply : solution.Contains(conversion), "Common denominator conversion incorrect.");
            fixtures.Add(question);
        }

        int generated = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var printed = fixtures.Select(q => QuizWorksheetBuilder.Format(q, language)).ToList();
            foreach (var kind in new[] { QuizProblemKind.Arithmetic, QuizProblemKind.FindX, QuizProblemKind.Fraction, QuizProblemKind.Expression, QuizProblemKind.Decimal })
            for (int stars = 1; stars <= 5; stars++)
            for (int variant = 0; variant < 4; variant++)
            {
                var request = new QuizProblemRequest(kind, ArithmeticOperation: (ArithmeticOperation)variant,
                    FindXOperation: (ArithmeticOperation)variant, FractionOperation: (FractionOperation)variant,
                    ElementaryType: (ElementaryQuizType)((int)ElementaryQuizType.DecimalAdd + variant));
                var question = await factory.CreateAsync(request, ArithmeticQuizMode.Essay, new((CurriculumTier)stars, false), language,
                    new(QuestionKnowledgeGroup.Objects), PracticeQuestionFormat.Numeric, false);
                var item = QuizWorksheetBuilder.Format(question, language);
                Check(QuizPrintCalculationSolution.AppliesTo(question), "Numeric practice incorrectly classified as story.");
                Check(!item.Solution.Contains("Đáp số:") && !item.Solution.Contains("Answer:"), "Numeric solution contains an answer label.");
                if (question.FindXProblem is not null) Check(item.Solution.Split('\n').Length == 3, "Find x step missing.");
                if (question.FractionProblem is { } f && f.Operation is FractionOperation.Add or FractionOperation.Subtract && f.LeftOperand.Denominator != f.RightOperand.Denominator)
                    Check(item.Solution.Contains("×"), "Generated fraction lacks denominator conversion.");
                if (printed.Count < 95) printed.Add(item);
                generated++;
            }
            var comparison = await factory.CreateAsync(new(QuizProblemKind.Arithmetic, IsComparison: true), ArithmeticQuizMode.Essay,
                new(CurriculumTier.FiveStars, false), language, new(QuestionKnowledgeGroup.Objects), PracticeQuestionFormat.Numeric, false);
            var comparisonItem = QuizWorksheetBuilder.Format(comparison, language);
            Check(comparisonItem.Solution == comparison.ElementaryProblem!.FormatComparison(comparison.ElementaryProblem.Answers[0].Text!), "Comparison should only show the complete relation.");
            printed.Add(comparisonItem);
            var sheet = new QuizWorksheet("CALC", "Calculations", "Numeric practice", language, CurriculumTier.FiveStars, ArithmeticQuizMode.Essay, printed.AsReadOnly());
            byte[] bytes = QuizPdfWriter.Write(sheet, true);
            File.WriteAllBytes(Path.Combine(directory, $"calculation-only-{language}.pdf"), bytes);
            string text = PdfTestReader.ExtractText(bytes);
            Check(!text.Contains("Đáp án:") && !text.Contains("Đáp số:") && !text.Contains("Answer:"), "PDF added an answer label to numeric practice.");
            Check(text.Contains("1 + 1 = 2") && text.Contains("x = 2 − 1"), "PDF lost calculation steps.");
            File.WriteAllText(Path.Combine(directory, $"calculation-only-{language}.txt"), text);

            var story = await factory.CreateAsync(new(QuizProblemKind.Arithmetic, ArithmeticOperation.Add), ArithmeticQuizMode.Essay,
                new(CurriculumTier.ThreeStars, false), language, new(QuestionKnowledgeGroup.Objects), PracticeQuestionFormat.WordProblem, false);
            var storyItem = QuizWorksheetBuilder.Format(story, language);
            Check(!QuizPrintCalculationSolution.AppliesTo(story), "Story lost answer formatting.");
            var fractionStory = fixtures[9] with { WordProblem = story.WordProblem };
            var fractionStoryItem = QuizWorksheetBuilder.Format(fractionStory, language);
            Check(fractionStoryItem.Solution.Contains("3/6 + 2/6") && fractionStoryItem.Solution.Contains(language == AppLanguage.Vietnamese ? "Đáp số:" : "Answer:"), "Fraction story lost conversion or final answer.");
            var mixed = sheet with { Questions = Array.AsReadOnly(new[] { storyItem, printed[0], fractionStoryItem }) };
            bytes = QuizPdfWriter.Write(mixed, true);
            text = PdfTestReader.ExtractText(bytes);
            Check(text.Contains(language == AppLanguage.Vietnamese ? "Đáp án:" : "Answer:"), "PDF omitted the story's answer.");
            File.WriteAllBytes(Path.Combine(directory, $"mixed-answer-styles-{language}.pdf"), bytes);
        }
        Console.WriteLine($"PASS calculation answers: {fixtures.Count} exact fixtures, {generated} numeric cases, comparisons, word problems and extracted PDF text in both languages.");
    }

    // Decode PDFsharp's actual drawing text using each embedded font's Unicode map.
}
