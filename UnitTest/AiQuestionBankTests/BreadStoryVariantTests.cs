using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class BreadStoryVariantTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        int count = 0;
        var types = new[] { ("sandwich-bread", "sandwich", "bread_sandwich"), ("round-bread", "round", "bread"), ("banh-mi", "banh-mi", "bread_banh_mi") };
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            foreach (var (id, stem, art) in types)
            {
                var unit = QuestionUnits.Find(id);
                Check(unit is not null, "Missing authored bread unit " + id);
                Check(QuizStoryArtwork.Select(true, false, [unit!.Item(language)]) == $"quiz_story_{art}.png", "Unit/image identity " + id);
                foreach (var op in new[] { ArithmeticOperation.Subtract, ArithmeticOperation.Multiply, ArithmeticOperation.Divide })
                {
                    var c = ArithmeticQuestionCatalogue.Create(op, tier, language, new(31), "bakery", unitId: id);
                    Check(c.IsValid && c.Unit == unit.Item(language), "Valid typed arithmetic " + id + op);
                    Check(ArithmeticQuestionCatalogue.Refresh(c, new(32)).Unit == c.Unit, "Refresh preserves subtype");
                    count++;
                }
                foreach (var op in Enum.GetValues<ArithmeticOperation>())
                {
                    string appliedId = $"bakery-{stem}-" + (op switch { ArithmeticOperation.Add => "total", ArithmeticOperation.Subtract => "difference", ArithmeticOperation.Multiply => "groups", _ => "share" });
                    if (AppliedQuestionCatalogue.Available(new(QuestionKnowledgeGroup.Objects), op, tier).Any(s => s.Id == appliedId))
                    {
                    var applied = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), op, tier, language, new(8), appliedId);
                    var appliedDraft = AppliedQuestionCatalogue.Draft(applied);
                    Check(applied.IsValid && applied.Unit == unit.Item(language), "Typed applied facts " + appliedId);
                    Check(AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(appliedDraft), applied).Draft is not null, "Typed applied draft " + appliedId);
                    if (language == AppLanguage.English && op is ArithmeticOperation.Add or ArithmeticOperation.Multiply)
                    {
                        var one = applied with { Left = 1 };
                        string rendered = AppliedQuestionCatalogue.Draft(one).ToWordProblem(one).ProblemText;
                        Check(!rendered.Contains("1 " + unit.Plural), "Singular amount uses authored singular " + appliedId);
                    }
                    if (id == "banh-mi" && language == AppLanguage.English)
                        Check(!appliedDraft.GivenA.Contains("bakes"), "Filled bread is prepared, not baked with fillings");
                    count++;
                    }
                    string sceneId = $"fraction-bread-{stem}-{op}";
                    Check(FractionQuestionCatalogue.Find(sceneId) is not null, "Missing typed fraction scene " + sceneId);
                    var c = FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), op, tier, language, new(7), sceneId);
                    var draft = FractionQuestionCatalogue.Draft(c);
                    Check(c.IsValid, "Fraction facts valid " + sceneId);
                    Check(FractionQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft), c).Draft is not null, "Fraction draft valid " + sceneId);
                    Check(draft.ToWordProblem(c).StoryArtworkId == art, "Fraction source/image identity " + sceneId);
                    count++;
                }
            }
            var seenAdditionUnits = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++)
            {
                var c = AdditionQuestionCatalogue.Create(tier, language, new(seed), "bakery");
                Check(c.IsValid && c.Unit != QuestionUnits.Find("bread-rolls")!.Item(language), "New bakery selection is explicit");
                Check(QuestionUnits.Find(c) is not null, "New units can be looked up for rendering/AI validation");
                seenAdditionUnits.Add(c.Unit);
                var f = FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, tier, language, new(seed));
                Check(!f.SceneId.Equals("fraction-bread-Add"), "New fractions do not choose generic bread");
                count++;
            }
            Check(types.All(t => seenAdditionUnits.Contains(QuestionUnits.Find(t.Item1)!.Item(language))), "All three bread types participate in addition generation");
            // Persisted generic bread is still valid and can be regenerated explicitly.
            var old = ArithmeticQuestionCatalogue.Create(ArithmeticOperation.Subtract, tier, language, new(5), "bakery", unitId: "bread-rolls");
            Check(old.IsValid && ArithmeticQuestionCatalogue.Refresh(old, new(6)).Unit == old.Unit, "Legacy arithmetic compatibility");
            var oldFraction = FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, tier, language, new(9), "fraction-bread-Add");
            Check(oldFraction.IsValid && FractionQuestionCatalogue.Draft(oldFraction).ToWordProblem(oldFraction).StoryArtworkId == "bread", "Legacy fraction compatibility");
            Check(AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, tier, language, new(9), "bakery-total").IsValid, "Legacy applied compatibility");
        }
        Console.WriteLine($"PASS bread variants: {count} generated VI/EN fixtures across five tiers, typed identity, exact fractions, refresh and legacy contracts.");
    }
}
