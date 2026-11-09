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
    internal static void CheckFoodVenue(BasicQuestionContract contract, BasicQuestionDraft draft)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Food) return;
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string eatery = vi ? "quán ăn của " : "'s eatery";
        string restaurant = vi ? "nhà hàng của " : "'s restaurant";
        Check(contract.Subject.Contains(eatery, StringComparison.OrdinalIgnoreCase),
            $"Prepared food has no named owner's eatery: {contract.SceneId}/{contract.Language}: {contract.Subject}");
        if (!string.IsNullOrWhiteSpace(contract.OtherSubject))
            Check(contract.OtherSubject.Contains(restaurant, StringComparison.OrdinalIgnoreCase),
                $"Prepared food has no named second restaurant: {contract.SceneId}/{contract.Language}: {contract.OtherSubject}");
        string story = draft.ToWordProblem(contract).ProblemText;
        Check(story.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase),
            $"Rendered prepared-food story omits owner venue: {contract.SceneId}/{contract.Language}: {story}");
        string prompt = BasicQuestionPrompt.Build(contract);
        string instruction = vi ? "Tất cả món ăn đã chế biến được phục vụ" : "All prepared dishes are served";
        Check(prompt.Split(instruction, StringSplitOptions.None).Length == 2,
            $"Prepared-food AI guidance missing or duplicated: {contract.SceneId}/{contract.Language}");
    }
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
            ("cats", "cat"), ("pigs", "pig"),
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
    private static int CheckNewDishArtwork()
    {
        var dishes = new (string UnitId, string ArtworkId)[]
        {
            ("com-tam", "com_tam"), ("op-la", "op_la"),
            ("thit-kho-trung", "thit_kho_trung"), ("banh-xeo", "banh_xeo"),
            ("bun-thit-nuong-cha-gio", "bun_thit_nuong_cha_gio"),
            ("hu-tieu-nam-vang", "hu_tieu_nam_vang"), ("mi-quang", "mi_quang"),
            ("bun-bo-hue", "bun_bo_hue"), ("banh-cuon", "banh_cuon"),
            ("kimchi", "kimchi"), ("sushi", "sushi")
        };
        int verified = 0;
        foreach (var (id, artwork) in dishes)
        {
            var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == id);
            Check(subject.Group == QuestionKnowledgeGroup.Food && subject.ArtworkId == artwork,
                "Incorrect food group or image mapping: " + id);
            foreach (var language in Enum.GetValues<AppLanguage>())
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
            {
                var contract = FoodThemeInterchangeTests.Contract(subject, operation, language, family);
                var draft = BasicQuestionTemplates.Example(contract);
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), contract).IsValid,
                    $"Invalid authored/AI example: {id}/{family}/{operation}/{language}");
                CheckFoodVenue(contract, draft);
                CheckArtwork(contract, draft, artwork);
                verified++;
            }
        }
        return verified;
    }
    public static void Run()
    {
        int count = 0;
        int animalCount = CheckAnimalArtwork();
        int dishCount = CheckNewDishArtwork();
        foreach (var subject in FoodThemeCatalogue.Subjects)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var matched = QuizStoryArtwork.Select(true,false,[subject.Unit.Item(language)]);
            Check(matched == $"quiz_story_{subject.ArtworkId}.png", $"Exact counted subject picture: {subject.Unit.Id}/{language}: expected {subject.ArtworkId}, got {matched ?? "<none>"} for '{subject.Unit.Item(language)}'");
        }
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
                CheckFoodVenue(c, draft);
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
                CheckFoodVenue(f, FractionQuestionCatalogue.Draft(f));
                var roles = FindXQuestionCatalogue.Available(profile, op, tier).ToArray();
                Check(roles.Length > 0, "Find X theme roles available");
                var x = FindXQuestionCatalogue.Create(profile, op, tier, language, new(7));
                Check(x.IsValid && x.KnowledgeGroup == group, "Find X theme contract");
                var xScene = FindXQuestionCatalogue.Find(x.SceneId)!.SourceScene;
                var xSubject = FoodThemeCatalogue.Subjects.Single(s => s.Group == group && s.Unit.Id == xScene.UnitId);
                CheckArtwork(x, FindXQuestionCatalogue.Draft(x), xSubject.ArtworkId);
                CheckFoodVenue(x, FindXQuestionCatalogue.Draft(x));
                count++;
            }
        }
        Console.WriteLine($"PASS animal images: {animalCount} bilingual species/operation fixtures with Find X; new dish images: {dishCount} bilingual operation/family/AI fixtures; food/drink/fruit images: {count} bilingual group/operation/tier fixtures across arithmetic, fractions, Find X and AI relation checks.");
    }
}
