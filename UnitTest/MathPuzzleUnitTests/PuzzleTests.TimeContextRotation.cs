using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckTimeContextRotation()
    {
        string[] earlyContexts = ["library", "sports", "craft", "schedule"];
        string[] lateContexts = ["tourism", "events", "schedule"];
        CurriculumTier[] changes = [CurriculumTier.OneStar, CurriculumTier.ThreeStars,
            CurriculumTier.TwoStars, CurriculumTier.FiveStars, CurriculumTier.FourStars,
            CurriculumTier.OneStar, CurriculumTier.FiveStars, CurriculumTier.TwoStars];
        int count = 0;
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 16; seed++)
        {
            // The page keeps this generator alive when users change stars and language.
            var generator = new ElementaryQuizGenerator(new Random(seed));
            foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Time))
            foreach (var language in Enum.GetValues<AppLanguage>())
            {
                foreach (var tier in changes)
                    Check(generator.Generate(mode, QuizProblemKind.Time, type, language, tier), tier);

                foreach (var tier in Enum.GetValues<CurriculumTier>())
                {
                    var allowed = tier >= CurriculumTier.ThreeStars ? lateContexts : earlyContexts;
                    var seen = new HashSet<string>();
                    // Two cycles include a full cycle even if the first starts midway.
                    for (int index = 0; index < allowed.Length * 2; index++)
                    {
                        var question = generator.Generate(mode, QuizProblemKind.Time, type, language, tier);
                        Check(question, tier);
                        seen.Add(question.ElementaryProblem!.StoryContextId!);
                    }
                    Require(allowed.All(seen.Contains), "A time context was omitted after switching stars.");
                }
            }
            foreach (var tier in changes)
            foreach (var language in Enum.GetValues<AppLanguage>())
                Check(generator.Generate(mode, QuizProblemKind.Time, null, language, tier), tier);
        }
        Console.WriteLine($"  Checked {count} time questions while switching stars, subtypes and languages on reused generators.");

        void Check(ArithmeticQuizQuestion question, CurriculumTier tier)
        {
            var contract = question.ElementaryProblem!;
            var allowed = tier >= CurriculumTier.ThreeStars ? lateContexts : earlyContexts;
            Require(allowed.Contains(contract.StoryContextId), "Time story does not belong to the selected star range.");
            Require(contract.Reasoning?.Tier == tier && contract.Answers.Count > 0,
                "Switching time contexts lost the selected difficulty or answers.");
            count++;
        }
    }
}
