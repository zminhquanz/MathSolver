using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckAreaMeasurementFoundation()
    {
        Require(AreaMeasurementTemplates.Contexts(AppLanguage.Vietnamese).Select(c => c.Id).SequenceEqual(
            AreaMeasurementTemplates.Contexts(AppLanguage.English).Select(c => c.Id)), "Language packs changed area context IDs.");
        foreach (var (category, large, small, expected) in new[] {
            ("length", "m", "dm", 10m), ("area", "m2", "dm2", 100m), ("area", "dm2", "cm2", 100m),
            ("area", "m2", "cm2", 10000m), ("area", "ha", "m2", 10000m), ("volume", "m3", "dm3", 1000m),
            ("volume", "dm3", "cm3", 1000m), ("volume", "m3", "cm3", 1000000m)
        }) Require(MeasurementQuantityRules.Convert(1, category, large, category, small) == expected, "Wrong dimensional conversion factor.");
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException) { return; }
            throw new InvalidOperationException("Invalid quantity/area target accepted.");
        }
        Reject(() => MeasurementQuantityRules.Convert(1, "area", "m", "area", "cm2"));
        Reject(() => MeasurementQuantityRules.Convert(1, "length", "m", "area", "cm2", true));
        Reject(() => MeasurementQuantityRules.Convert(1, "volume", "dm3", "capacity", "l"));
        Reject(() => MeasurementQuantityRules.Convert(1, "mass", "kg", "capacity", "l", true));
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            using var stream = typeof(PuzzleTests).Assembly.GetManifestResourceStream("QuizContent." + QuizContentCatalog.Culture(language) + ".json")!;
            var pack = QuizContentCatalog.ReadPack(stream);
            var badTexts = pack.Texts.Select(t => t.Id == AreaMeasurementTemplates.Prefix + "QuestionSmall"
                ? t with { Text = t.Text.Replace("diện tích", "chu vi").Replace("area", "perimeter") } : t).ToList();
            Reject(() => QuizContentValidation.CheckLists(new() { Version = pack.Version, Culture = pack.Culture,
                Texts = badTexts, Lists = pack.Lists }));
            var badLists = new Dictionary<string, System.Text.Json.JsonElement>(pack.Lists);
            using var badPhrases = System.Text.Json.JsonDocument.Parse(pack.Lists[AreaMeasurementTemplates.PhrasingList].GetRawText()
                .Replace("diện tích", "thể tích").Replace("area", "volume"));
            badLists[AreaMeasurementTemplates.PhrasingList] = badPhrases.RootElement.Clone();
            Reject(() => QuizContentValidation.CheckLists(new() { Version = pack.Version, Culture = pack.Culture,
                Texts = pack.Texts, Lists = badLists }));
        }
        Require(MeasurementQuantityRules.Convert(1, "volume", "dm3", "capacity", "l", true) == 1
            && MeasurementQuantityRules.Convert(1, "volume", "m3", "capacity", "ml", true) == 1000000
            && MeasurementQuantityRules.Convert(1, "capacity", "ml", "volume", "cm3", true) == 1, "Explicit volume/capacity bridge failed.");
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var context in AreaMeasurementTemplates.Contexts(language))
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 4; seed++)
        {
            var profile = new AreaMeasurementProfile(context.Id, tier, language);
            var generator = new ElementaryQuizGenerator(new(seed));
            var q = generator.GenerateAreaMeasurement(mode, profile);
            var c = q.ElementaryProblem!;
            AreaMeasurementQuestionValidator.Validate(c);
            Require(c.AreaMeasurement!.Context?.QuantityCategory == "area" && c.AreaMeasurement.Draft is not null,
                "The contract did not pin its physical quantity and reviewed wording.");
            var g = c.Reasoning!.Givens.ToDictionary(g => g.Role, g => decimal.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture));
            decimal factor = MeasurementQuantityRules.Convert(1, "area", context.LargeUnitId, "area", context.SmallUnitId);
            decimal expected = tier == CurriculumTier.TwoStars ? g["initial-small"] / factor
                : g["initial-large"] * factor + g.GetValueOrDefault("initial-small")
                    + g.GetValueOrDefault("added-large") * factor - g.GetValueOrDefault("removed-small");
            if (tier == CurriculumTier.FiveStars) expected /= factor;
            var answer = c.Answers.Single();
            Require((decimal)answer.Value.Numerator / (decimal)answer.Value.Denominator == expected && expected > 0, "Wrong independent area answer.");
            var unitId = tier is CurriculumTier.TwoStars or CurriculumTier.FiveStars ? context.LargeUnitId : context.SmallUnitId;
            Require(answer.Unit == MeasurementEngine.GetCategory("area").Units.Single(u => u.Id == unitId).Symbol, "Changed target unit.");
            Require(!c.ProblemText.Contains('{') && !c.SolutionText.Contains('{'), "Unrendered area role.");
            Require(ElementaryEssayValidator.CheckAnswers(q, c.AnswerText), "Correct area answer rejected.");
            Require(!ElementaryEssayValidator.CheckAnswers(q, expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + " m"), "Length accepted as area.");
            Require(c.ChoiceTexts!.Distinct().Count() == 4 && c.ChoiceTexts!.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1, "Invalid area choices.");
            if (mode == ArithmeticQuizMode.TrueFalse)
                Require(ElementaryEssayValidator.CheckAnswers(q, c.PresentedText!) == q.PresentedEquationIsCorrect, "Invalid area true/false result.");
            var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
            var result = grader.ValidateSubmission(q with { Mode = ArithmeticQuizMode.Essay }, c.AnswerText);
            Require(result.IsCorrect, $"Result-only area submission rejected: {context.Id}/{language}/{tier}/{mode}: {result}");
            var work = EssayCombinedInputParser.Parse(c.SolutionText, false, preserveAllCalculations: true);
            Require(grader.Validate(q with { Mode = ArithmeticQuizMode.Essay }, work.Solution, work.Equation, work.Answer).IsCorrect,
                "Independent area worked solution rejected: " + c.SolutionText);
            var fresh = generator.FreshAreaMeasurement(mode, c.AreaMeasurement!).ElementaryProblem!;
            Require(fresh.StoryContextId == c.StoryContextId && !fresh.Facts.SequenceEqual(c.Facts), "Fresh area data lost the context or retained numbers.");
            var d = AreaMeasurementTemplates.Draft(profile);
            var alternative = d with { Initial = AreaMeasurementTemplates.Choices(language, AreaMeasurementTemplates.InitialId(tier))[1] };
            var variant = generator.GenerateAreaMeasurement(mode, profile, alternative).ElementaryProblem!;
            AreaMeasurementQuestionValidator.Validate(variant);
            Require(AreaMeasurementTemplates.ProseHash(profile, d) != AreaMeasurementTemplates.ProseHash(profile, alternative), "Different words were not distinguished.");
            Require(AreaMeasurementTemplates.ProseHash(profile, d) == AreaMeasurementTemplates.ProseHash(fresh.AreaMeasurement!.Profile, d), "Random values bypassed wording deduplication.");
            var otherLeads = d with { SolutionLeads = AreaMeasurementTemplates.LeadIds(tier)
                .Select(id => AreaMeasurementTemplates.Choices(language, id)[1]).ToArray() };
            Require(AreaMeasurementTemplates.ProseHash(profile, d) == AreaMeasurementTemplates.ProseHash(profile, otherLeads),
                "Changing only solution leads created a new question identity.");
            Reject(() => generator.GenerateAreaMeasurement(mode, profile, d with { Question = d.Question.Replace("diện tích", "chu vi").Replace("area", "perimeter") }));
            Reject(() => generator.GenerateAreaMeasurement(mode, profile, d with { Question = d.Question.Replace("{small_unit}", "m").Replace("{large_unit}", "l") }));
            Reject(() => AreaMeasurementQuestionValidator.Validate(c with { Facts = c.Facts.Reverse().Append("999").ToArray() }));
            Reject(() => AreaMeasurementQuestionValidator.Validate(c with { Answers = [answer with { Unit = "l" }] }));
            Reject(() => generator.RecreateAreaMeasurement(mode, c.AreaMeasurement! with {
                Context = c.AreaMeasurement!.Context! with { QuantityCategory = "capacity" }
            }));
            Reject(() => generator.RecreateAreaMeasurement(mode, c.AreaMeasurement! with {
                Context = c.AreaMeasurement!.Context! with { SmallUnitId = "cm" }
            }));
            if ((int)tier >= 4)
                Reject(() => generator.GenerateAreaMeasurement(mode, profile, d with { Removed = d.Removed!.Replace("removed_small", "added_large") }));
            count++;
        }
        Console.WriteLine($"  {count} bilingual area profiles, independent conversions, three grading modes, fresh roles and rejected targets.");
    }
}
