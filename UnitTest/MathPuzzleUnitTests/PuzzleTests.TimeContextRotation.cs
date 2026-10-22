using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckTimeContextRotation()
    {
        string[] earlyContexts = ["library", "sports", "craft", "schedule"];
        string[] lateContexts = ["tourism", "events", "schedule"];
        string[] Allowed(ElementaryQuizType type, CurriculumTier tier, AppLanguage language) => type switch
        {
            _ when SameDayTimePilot.IsType(type) => SameDayTimePilot.Profiles(type, language).ToArray(),
            SameDayBreakPilot.Type => SameDayBreakPilot.Profiles(language).ToArray(),
            OvernightTimePilot.Type => OvernightTimePilot.Profiles(language).ToArray(),
            SameMonthCalendarPilot.Type => SameMonthCalendarPilot.Profiles(language).ToArray(),
            SameMonthCalendarPilot.InclusiveType => SameMonthCalendarPilot.Profiles(language, inclusive: true).ToArray(),
            SameMonthCalendarPilot.AdjacentType => SameMonthCalendarPilot.Profiles(language, adjacent: true).ToArray(),
            SameMonthCalendarPilot.AdjacentInclusiveType => SameMonthCalendarPilot.Profiles(language, inclusive: true, adjacent: true).ToArray(),
            SameMonthCalendarPilot.FebruaryCommonType => SameMonthCalendarPilot.Profiles(language, inclusive: true, adjacent: true, february: true).ToArray(),
            SameMonthCalendarPilot.FebruaryLeapType => SameMonthCalendarPilot.Profiles(language, inclusive: true, adjacent: true, leap: true).ToArray(),
            _ when ElementaryQuizGenerator.TimeStoryTypes.Contains(type) => ElementaryQuizGenerator.TimeStoryContextIds(tier),
            ElementaryQuizType.ReadClock or ElementaryQuizType.Calendar => tier >= CurriculumTier.ThreeStars ? lateContexts : earlyContexts,
            _ => throw new InvalidOperationException($"Missing time rotation expectations for {type}.")
        };
        CurriculumTier[] changes = [CurriculumTier.OneStar, CurriculumTier.ThreeStars,
            CurriculumTier.TwoStars, CurriculumTier.FiveStars, CurriculumTier.FourStars,
            CurriculumTier.OneStar, CurriculumTier.FiveStars, CurriculumTier.TwoStars];
        int count = 0;
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 16; seed++)
        {
            // The page keeps this generator alive when users change stars and language.
            var generator = new ElementaryQuizGenerator(new Random(seed));
            foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Time).Where(t => !ElementaryQuizGenerator.IsTimeFractionExtension(t) && !ElementaryQuizGenerator.IsMixedTime(t)))
            foreach (var language in Enum.GetValues<AppLanguage>())
            {
                foreach (var tier in changes)
                    Check(generator.Generate(mode, QuizProblemKind.Time, type, language, tier), tier, language, type);

                foreach (var tier in Enum.GetValues<CurriculumTier>())
                {
                    var allowed = Allowed(type, tier, language);
                    Require(allowed.Length > 0, $"No time profiles for {type}, {language}, {tier}.");
                    var seen = new HashSet<string>();
                    // Two cycles include a full cycle even if the first starts midway.
                    for (int index = 0; index < allowed.Length * 2; index++)
                    {
                        var question = generator.Generate(mode, QuizProblemKind.Time, type, language, tier);
                        Check(question, tier, language, type);
                        seen.Add(question.ElementaryProblem!.StoryContextId!);
                    }
                    Require(allowed.All(seen.Contains),
                        $"A time context was omitted after switching stars: {type}, {language}, {tier}; missing [{string.Join(", ", allowed.Except(seen))}].");
                }
            }
            foreach (var tier in changes)
            foreach (var language in Enum.GetValues<AppLanguage>())
                Check(generator.Generate(mode, QuizProblemKind.Time, null, language, tier), tier, language);
        }
        Console.WriteLine($"  Checked {count} time questions while switching stars, subtypes and languages on reused generators.");

        void Check(ArithmeticQuizQuestion question, CurriculumTier tier, AppLanguage language, ElementaryQuizType? requestedType = null)
        {
            var contract = question.ElementaryProblem!;
            Require(contract.Kind == QuizProblemKind.Time && contract.Language == language
                && (requestedType is null || contract.Type == requestedType),
                $"Changing stars changed the requested time skill or language: requested {requestedType}, {language}; got {contract.Type}, {contract.Language}.");
            if (ElementaryQuizGenerator.IsTimeFractionExtension(contract.Type) || ElementaryQuizGenerator.IsMixedTime(contract.Type))
            {
                Require(contract.Reasoning?.Tier == tier && new EssayAnswerValidator(new MathSolver.Services.Core.BasicArithmeticEngine())
                    .ValidateSubmission(question, contract.AnswerText).IsCorrect, "New time skill lost its tier or answer when changing stars.");
                count++;
                return;
            }
            var allowed = Allowed(contract.Type, tier, language);
            Require(allowed.Contains(contract.StoryContextId),
                $"Time story does not belong to the selected star range: {contract.Type}, {contract.Language}, {tier}, context '{contract.StoryContextId}', allowed [{string.Join(", ", allowed)}].");
            Require(contract.Reasoning?.Tier == tier && contract.Answers.Count > 0,
                "Switching time contexts lost the selected difficulty or answers.");
            if (SameDayTimePilot.IsType(contract.Type) || contract.Type is ElementaryQuizType.SameDayActiveDuration
                or ElementaryQuizType.OvernightElapsed or ElementaryQuizType.SameMonthElapsed or ElementaryQuizType.SameMonthInclusive
                or ElementaryQuizType.AdjacentMonthElapsed or ElementaryQuizType.AdjacentMonthInclusive
                or ElementaryQuizType.FebruaryCommonInclusive or ElementaryQuizType.FebruaryLeapInclusive)
                Require(new EssayAnswerValidator(new MathSolver.Services.Core.BasicArithmeticEngine())
                    .ValidateSubmission(question, contract.AnswerText).IsCorrect,
                    $"Time profile answer changed after rotating context: {contract.Type}, {language}, {tier}, {contract.StoryContextId}.");
            count++;
        }
    }
}
