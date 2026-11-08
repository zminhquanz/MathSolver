using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class FoodThemeTests
{
    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (id,op) in new[]{("pho",ArithmeticOperation.Subtract),("lemon-tea-glasses",ArithmeticOperation.Multiply),("strawberries",ArithmeticOperation.Add)})
        {
            var s=FoodThemeCatalogue.Subjects.Single(s=>s.Unit.Id==id);
            yield return ($"{s.Group}/{id}/{op}/{language}",FoodThemeInterchangeTests.Contract(s,op,language,BankQuestionFamily.Arithmetic));
        }
    }
    public static void WriteManifest() => File.WriteAllText("artifacts/food-theme-subjects.json", System.Text.Json.JsonSerializer.Serialize(
        FoodThemeCatalogue.Subjects.Select(s => new { Group=s.Group.ToString(), s.Unit, s.ArtworkId, s.FractionVietnamese, s.FractionEnglish, s.FractionArtworkId }),
        new System.Text.Json.JsonSerializerOptions { WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void CheckArtwork(BasicQuestionContract contract, BasicQuestionDraft draft, string expected)
    {
        Check(contract.IsValid, "Invalid source: " + contract.SceneId);
        var word = draft.ToWordProblem(contract);
        Check(word.StoryArtworkId == expected, $"Wrong subject image for {contract.SceneId}: {word.StoryArtworkId} != {expected}");
        Check(QuizStoryArtwork.Select(true, word.FactTable is not null, [word.AnswerUnit],
            itemUnit: word.ItemUnit, artworkId: word.StoryArtworkId) == $"quiz_story_{expected}.png",
            "Image hidden or incorrect: " + contract.SceneId);
    }
    private static int CheckAnimalArtwork()
    {
        var animals = new (string UnitId, string ArtworkId)[]
        {
            ("chickens", "chicken"), ("ducks", "duck"), ("birds", "bird"), ("fish", "fish"),
            ("cows", "cow"), ("horses", "horse"), ("deer", "deer"), ("dogs", "dog"),
            ("rabbits", "rabbit"), ("turtles", "turtle"), ("tortoises", "tortoise"), ("geese", "goose")
        };
        int checkedCount = 0;
        foreach (var (id, art) in animals)
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var op in Enum.GetValues<ArithmeticOperation>())
        {
            var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Animals);
            var scene = AppliedQuestionCatalogue.All.Single(s => s.Group == QuestionKnowledgeGroup.Animals
                && s.UnitId == id && s.Operation == op);
            Check(QuestionUnits.Find(id) is not null, "Unknown animal unit: " + id);
            Check(QuizStoryArtworkCatalogue.AppliedSceneArtwork(scene) == art, "Wrong animal identity: " + id);
            Check(QuizStoryArtworkCatalogue.AppliedSceneArtwork(scene with { Group = QuestionKnowledgeGroup.Objects }) is null,
                "Animal art escaped its knowledge group: " + id);
            var c = AppliedQuestionCatalogue.Create(profile, op, CurriculumTier.ThreeStars, language, new(29), scene.Id);
            CheckArtwork(c, AppliedQuestionCatalogue.Draft(c), art);
            foreach (var findX in FindXQuestionCatalogue.All.Where(s => s.SourceScene.Id == scene.Id))
            {
                var x = FindXQuestionCatalogue.Create(profile, findX.EquationOperation,
                    CurriculumTier.ThreeStars, language, new(31), findX.Role, findX.Id);
                CheckArtwork(x, FindXQuestionCatalogue.Draft(x), art);
            }
            checkedCount++;
        }
        return checkedCount;
    }
    public static void Run()
    {
        int count = 0;
        int animalCount = CheckAnimalArtwork();
        foreach (var subject in FoodThemeCatalogue.Subjects)
        foreach (var language in Enum.GetValues<AppLanguage>())
            Check(QuizStoryArtwork.Select(true,false,[subject.Unit.Item(language)]) == $"quiz_story_{subject.ArtworkId}.png", "Exact counted subject picture: " + subject.Unit.Id);
        foreach (var (name, value) in new[] { ("Food",12),("Drinks",13),("Fruit",14) })
        {
            Check(Enum.TryParse<QuestionKnowledgeGroup>(name, out var group) && (int)group == value, "Missing stable knowledge group: " + name);
            Check(QuestionLearningProfile.Groups().Contains(group), "Group missing from shared practice/AI/PDF/Excel choices");
            foreach (var language in Enum.GetValues<AppLanguage>())
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            foreach (var op in Enum.GetValues<ArithmeticOperation>())
            {
                var profile = new QuestionLearningProfile(group);
                var c = AppliedQuestionCatalogue.Create(profile, op, tier, language, new(3));
                Check(c.IsValid && c.KnowledgeGroup == group, "Concrete theme contract " + name);
                var draft = AppliedQuestionCatalogue.Draft(c);
                var source = AppliedQuestionCatalogue.Find(c.SceneId)!;
                var subject = FoodThemeCatalogue.Subjects.Single(s => s.Group == group && s.Unit.Id == source.UnitId);
                CheckArtwork(c, draft, subject.ArtworkId);
                Check(draft.ToWordProblem(c).Quantity==WordProblemQuantity.Count,"A counted food/drink/fruit story must not be graded as distance");
                Check(AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Standard AI template syntax/meaning");
                Check(OpenQuestionProse.Supports(c), "New theme inherits conservative open prose support");
                Check(OpenQuestionProse.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Known correct theme expression must verify");
                Check(!OpenQuestionProse.Validate(QuestionBankStore.SerializeDraft(draft with { Question = draft.Question + (language == AppLanguage.English ? " Do not find this amount." : " Không tìm lượng này.") }), c).IsValid, "Changed/negated target must not auto-verify");
                var f = FractionQuestionCatalogue.Create(profile, op, tier, language, new(5));
                Check(f.IsValid && f.KnowledgeGroup == group, "Exact fraction theme");
                if(op==ArithmeticOperation.Subtract)
                    Check(BasicQuestionPrompt.Build(f).Contains(language==AppLanguage.Vietnamese?"hai lượng riêng":"two separate amounts"),"Fraction comparison must not receive removal/remaining guidance");
                if(language==AppLanguage.English)
                    Check(FractionQuestionCatalogue.Render("{a} {unit_a}",f).Contains(" of a"),"English proper fractions need a singular object/volume phrase");
                CheckArtwork(f, FractionQuestionCatalogue.Draft(f),
                    QuizStoryArtworkCatalogue.FractionArtwork(f.SceneId)!);
                var roles = FindXQuestionCatalogue.Available(profile, op, tier).ToArray();
                Check(roles.Length > 0, "Find X theme roles available");
                var x = FindXQuestionCatalogue.Create(profile, op, tier, language, new(7));
                Check(x.IsValid && x.KnowledgeGroup == group, "Find X theme contract");
                var xScene = FindXQuestionCatalogue.Find(x.SceneId)!.SourceScene;
                var xSubject = FoodThemeCatalogue.Subjects.Single(s => s.Group == group && s.Unit.Id == xScene.UnitId);
                CheckArtwork(x, FindXQuestionCatalogue.Draft(x), xSubject.ArtworkId);
                count++;
            }
        }
        Console.WriteLine($"PASS animal images: {animalCount} bilingual species/operation fixtures with Find X; food/drink/fruit images: {count} bilingual group/operation/tier fixtures across arithmetic, fractions, Find X and AI relation checks.");
    }
}
