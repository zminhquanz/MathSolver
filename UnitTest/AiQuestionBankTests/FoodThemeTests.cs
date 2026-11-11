using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class FoodThemeTests
{
    private static readonly string[] TakeawayBakeryCakeIds =
        ["banh-chung", "banh-gio", "banh-day", "banh-bao-thit-trung-cut", "banh-tet"];

    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (id,op) in new[]{("pho",ArithmeticOperation.Subtract),("lemon-tea-glasses",ArithmeticOperation.Multiply),("strawberries",ArithmeticOperation.Add)}
            .Concat(TakeawayBakeryCakeIds.Select(id => (id,ArithmeticOperation.Add))))
        {
            var s=FoodThemeCatalogue.Subjects.Single(s=>s.Unit.Id==id);
            yield return ($"{s.Group}/{id}/{op}/{language}",FoodThemeInterchangeTests.Contract(s,op,language,BankQuestionFamily.Arithmetic));
        }
    }
    public static void WriteManifest() => File.WriteAllText("artifacts/food-theme-subjects.json", System.Text.Json.JsonSerializer.Serialize(
        FoodThemeCatalogue.Subjects.Select(s => new { Group=s.Group.ToString(), s.Unit, s.ArtworkId, s.FractionVietnamese, s.FractionEnglish, s.FractionArtworkId }),
        new System.Text.Json.JsonSerializerOptions { WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void CheckAllFoodVenueKinds()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var (unit, bakery) in BakeryVenueCases())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (int seed in Enumerable.Range(0, 80))
            {
                var contract = AppliedQuestionCatalogue.Create(new QuestionLearningProfile(QuestionKnowledgeGroup.Food),
                    ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new Random(seed),
                    $"theme-food-{unit}-total");
                CheckFoodVenue(contract, AppliedQuestionCatalogue.Draft(contract));
                foreach (int i in Enumerable.Range(0, FoodThemePrompt.VenueCount(bakery)))
                {
                    string venue = FoodThemePrompt.Venue(bakery, i, language);
                    string ownerPhrase = language == AppLanguage.Vietnamese ? venue + " của " : "'s " + venue;
                    if (contract.Subject.Contains(ownerPhrase, StringComparison.OrdinalIgnoreCase))
                        seen.Add(venue);
                }
            }
            Check(seen.Count == FoodThemePrompt.VenueCount(bakery),
                $"Seeded C# scenes did not exercise every food venue: {unit}/{language}: {string.Join(", ", seen)}");
        }
    }
    private static void CheckAiCombinedVenueValidation()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var (unit, bakery) in BakeryVenueCases())
        {
            var contract = AppliedQuestionCatalogue.Create(new QuestionLearningProfile(QuestionKnowledgeGroup.Food),
                ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new Random(23),
                $"theme-food-{unit}-total");
            var draft = AppliedQuestionCatalogue.Draft(contract);
            Check(FoodThemePrompt.CombinedQuestionNamesVenue(draft.Question, contract),
                "C# reference question has mismatched food venues");

            var venues = Enumerable.Range(0, FoodThemePrompt.VenueCount(bakery))
                .Select(i => FoodThemePrompt.Venue(bakery, i, language)).ToArray();
            string current = venues.Single(v => contract.Subject.Contains(
                language == AppLanguage.Vietnamese ? v + " của " : "'s " + v,
                StringComparison.OrdinalIgnoreCase));
            string different = venues.First(v => v != current);
            string replacement = language == AppLanguage.Vietnamese ? "cả hai " + different
                : "the two " + (different switch {
                    "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
                    "restaurant" => "restaurants", _ => "diners"
                });
            string invalid = draft.Question.Replace(
                language == AppLanguage.Vietnamese ? "cả hai cơ sở" : "the two businesses",
                replacement, StringComparison.OrdinalIgnoreCase);
            Check(invalid != draft.Question, "Reference question no longer has a replaceable venue phrase");
            Check(!FoodThemePrompt.CombinedQuestionNamesVenue(invalid, contract),
                "Mismatched AI venue must not pass the semantic venue check");
            Check(!AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft with { Question = invalid }), contract).IsValid,
                "Mismatched AI venue must be rejected before saving to the question bank");
            var creative = contract with { OpenProse = true };
            if (OpenQuestionProse.Supports(creative))
                Check(!OpenQuestionProse.Validate(QuestionBankStore.SerializeDraft(draft with { Question = invalid }), creative).IsValid,
                    "Creative AI prose must reject a mismatched food venue");
        }
    }
    private static IEnumerable<(string Unit, bool Bakery)> BakeryVenueCases()
    {
        yield return ("cakes", true);
        yield return ("pho", false);
        foreach (var id in TakeawayBakeryCakeIds)
            yield return (id, true);
    }
    internal static void CheckFoodVenue(BasicQuestionContract contract, BasicQuestionDraft draft)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Food) return;
        bool vi = contract.Language == AppLanguage.Vietnamese;
        bool bakery = FoodThemeCatalogue.IsBakeryContract(contract);
        var venues = Enumerable.Range(0, FoodThemePrompt.VenueCount(bakery))
            .Select(i => FoodThemePrompt.Venue(bakery, i, contract.Language)).ToArray();
        string ActorVenue(string v) => vi ? v + " của " : "'s " + v;
        var matching = venues.Where(v => contract.Subject.Contains(ActorVenue(v), StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(matching.Length == 1,
            $"Prepared food requires one valid venue: {contract.SceneId}/{contract.Language}: {contract.Subject}");
        string venue = matching[0];
        if (!string.IsNullOrWhiteSpace(contract.OtherSubject))
            Check(contract.OtherSubject.Contains(ActorVenue(venue), StringComparison.OrdinalIgnoreCase),
                $"Food owners must have identical venue kinds: {contract.SceneId}/{contract.Language}: {contract.Subject}, {contract.OtherSubject}");
        if (bakery)
        {
            Check(!contract.Subject.Contains(vi ? "quán ăn" : "'s eatery", StringComparison.OrdinalIgnoreCase),
                "Bakery question received restaurant wording: " + contract.SceneId);
            Check(contract.Unit.Contains(vi ? "bánh kem" : "cream cake", StringComparison.OrdinalIgnoreCase)
                || !contract.SceneId.Contains("food-cakes-", StringComparison.Ordinal),
                "Cake question must count cream cakes: " + contract.SceneId);
        }
        string story = draft.ToWordProblem(contract).ProblemText;
        Check(story.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase),
            $"Rendered prepared-food story omits owner venue: {contract.SceneId}/{contract.Language}: {story}");
        if (TakeawayBakeryCakeIds.Any(id => contract.SceneId.Contains("food-" + id + "-", StringComparison.Ordinal)))
        {
            foreach (string restaurantTerm in vi ? new[] { "quán ăn", "nhà hàng", "phục vụ", "bàn ăn" }
                : new[] { "eatery", "restaurant", "diner", "serve at tables", "dining table" })
                Check(!story.Contains(restaurantTerm, StringComparison.OrdinalIgnoreCase),
                    $"Takeaway bakery cake uses restaurant wording: {contract.SceneId}/{contract.Language}: {story}");
            Check(story.Contains(vi ? "bán" : "sell", StringComparison.OrdinalIgnoreCase)
                || story.Contains(vi ? "chuẩn bị" : "prepare", StringComparison.OrdinalIgnoreCase),
                $"Takeaway bakery cake lacks selling/preparing context: {contract.SceneId}/{contract.Language}: {story}");
        }
        if (contract.Structure == BasicQuestionStructure.Combine)
        {
            Check(story.Contains(contract.OtherSubject, StringComparison.OrdinalIgnoreCase),
                "Combined food story omits second matching venue: " + story);
            string pair = vi ? "cả hai " + venue : "the two " + (venue switch
            {
                "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
                "restaurant" => "restaurants", _ => "diners"
            });
            Check(story.Contains(pair, StringComparison.OrdinalIgnoreCase),
                "Combined food question must name both matching businesses: " + story);
        }
        string prompt = BasicQuestionPrompt.Build(contract);
        string instruction = bakery
            ? (vi ? "Bánh được làm hoặc bán tại" : "Cakes, pastries and bakery items are made or sold")
            : (vi ? "Tất cả món ăn đã chế biến được phục vụ" : "All prepared dishes are served");
        Check(prompt.Split(instruction, StringSplitOptions.None).Length == 2,
            $"Prepared-food AI guidance missing or duplicated: {contract.SceneId}/{contract.Language}");
        if (contract.Structure == BasicQuestionStructure.Combine)
            Check(prompt.Contains(contract.Subject) && prompt.Contains(contract.OtherSubject)
                && prompt.Contains(vi ? "Hai cửa hàng trong cùng một đề" : "Both owners run the same kind of venue"),
                "Combined food AI prompt must keep the venue relation");
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
            ("banh-chung", "banh_chung"), ("banh-day", "banh_day"),
            ("banh-tet", "banh_tet"), ("banh-beo", "banh_beo"),
            ("banh-khot", "banh_khot"), ("banh-bot-loc", "banh_bot_loc"),
            ("banh-gio", "banh_gio"),
            ("banh-bao-thit-trung-cut", "banh_bao_thit_trung_cut"),
            ("ha-cao", "ha_cao"),
            ("kimchi", "kimchi"), ("sushi", "sushi"),
            ("banh-cam", "banh_cam"), ("banh-com", "banh_com"),
            ("banh-trung-thu", "banh_trung_thu"), ("banh-pia", "banh_pia"),
            ("croissants", "croissant"), ("banh-da-lon", "banh_da_lon"),
            ("banh-bo", "banh_bo"), ("muffins", "muffin"),
            ("cupcakes", "cupcake"), ("cookies", "cookie"),
            ("donuts", "donut"), ("waffles", "waffle"),
            ("apple-pies", "apple_pie"), ("flans", "flan")
        };
        int verified = 0;
        foreach (var (id, artwork) in dishes)
        {
            var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == id);
            Check(subject.Group == QuestionKnowledgeGroup.Food && subject.ArtworkId == artwork,
                "Incorrect food group or image mapping: " + id);
            if (id is "banh-chung" or "banh-gio" or "banh-day" or "banh-bao-thit-trung-cut" or "banh-tet")
                Check(FoodThemeCatalogue.IsBakeryFood(id),
                    "Traditional takeaway cake must belong to a bakery/cake shop: " + id);
            else if (id is "banh-beo" or "banh-khot" or "banh-bot-loc" or "ha-cao")
                Check(!FoodThemeCatalogue.IsBakeryFood(id),
                    "Traditional prepared dish must belong to a restaurant/eatery/diner: " + id);
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
        CheckAllFoodVenueKinds();
        CheckAiCombinedVenueValidation();
        Check(!FoodThemeCatalogue.IsBakeryFood("hamburgers"),
            "Hamburgers must use an eatery or restaurant, not a bakery");
        Check(!FoodThemeCatalogue.IsBakeryFood("banh-xeo") && !FoodThemeCatalogue.IsBakeryFood("banh-cuon"),
            "Banh xeo and banh cuon must use an eatery/restaurant rather than a bakery");
        Check(FoodThemeCatalogue.IsBakeryFood("cakes") && FoodThemeCatalogue.IsBakeryFood("flans"),
            "Desserts must use a named bakery or cake shop");
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
