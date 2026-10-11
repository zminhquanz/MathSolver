using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckPromptPunctuation()
    {
        int cases = 0;
        var errors = new HashSet<string>();
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            AppLanguageManager.CurrentLanguage = language;
            var mode = Enum.GetValues<ArithmeticQuizMode>()[seed % 3];
            foreach (var (kind, subtype) in AllSubtypes())
                Check(Generate(kind, subtype, mode, language, seed, tier), $"{language}/{tier}/{kind}/{subtype}");
            foreach (var type in Enum.GetValues<ExpressionQuizType>())
                Check(new ExpressionQuizGenerator(new Random(seed)).Generate(mode, type, tier), $"{language}/{tier}/Expression/{type}");
            foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
            foreach (var type in ElementaryQuizGenerator.Types(kind))
                Check(new ElementaryQuizGenerator(new Random(seed)).Generate(mode, kind, type, language, tier), $"{language}/{tier}/{kind}/{type}");

            void Check(ArithmeticQuizQuestion question, string label)
            {
                cases++;
                string?[] prompts = [question.WordProblem?.ProblemText, question.ElementaryProblem?.ProblemText,
                    question.ProportionProblem?.ProblemText, question.MotionProblem?.ProblemText,
                    question.AverageProblem?.ProblemText, question.PercentageProblem?.ProblemText,
                    question.ElementaryProblem?.Visual?.AccessibleDescription];
                foreach (var prompt in prompts)
                    if (prompt?.Contains(';') == true) errors.Add(label + ": " + prompt);
                var diagram = QuizDiagramBuilder.Build(question, language);
                if (diagram?.Caption.Contains(';') == true)
                    errors.Add(label + ": " + diagram.Caption);
            }
        }
        Require(errors.Count == 0, "Question prompts still contain semicolons:\n" + string.Join("\n", errors.Take(20)));
        Console.WriteLine($"  Checked {cases} generated questions, all families and subtypes, five stars, both languages and three answer modes.");
    }
}
