using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class QuizContentTests
{
    internal static QuizContentPack Read(string path)
    {
        using var stream = File.OpenRead(path);
        return QuizContentCatalog.ReadPack(stream);
    }

    internal static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Reject(Action action, string label)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Invalid pack accepted: " + label);
        }
        QuizContentPack Load(string culture)
        {
            using var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent." + culture + ".json")!;
            return QuizContentCatalog.ReadPack(stream);
        }
        var vi = Load("vi-VN"); var en = Load("en-US");
        using (var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.catalogues.json")!)
        using (var document = JsonDocument.Parse(stream))
        {
            var lists = document.RootElement.GetProperty("Lists");
            foreach (var (name, expectedCount) in new (string Name, int Count)[]
            {
                ("BreadQuestionTypes", 3),
                ("FoodThemeSubjects", 207),
                ("FoodThemeRestaurantDishIds", 103),
                ("FoodThemeExistingUnits", 9),
                ("FoodThemeCoffeeDrinkIds", 5),
                ("FoodThemeMilkTeaDrinkIds", 9),
                ("FoodThemeDessertShopKinds", 16),
                ("FoodThemeBakeryIds", 32),
                ("FruitGardenPlantUnits", 32),
                ("PetAnimalThemeSubjects", 18),
                ("ToyThemeSubjects", 12),
                ("SchoolThemeSubjects", 26),
                ("FractionThemeSubjects", 34),
                ("QuestionUnits", 47),
                ("AdditionExtraUnits", 37),
                ("AdditionScenes", 29),
                ("FoodPairedDishIds", 6),
                ("FoodKoreanDishDescriptions", 9),
                ("FoodVietnameseDishDescriptions", 13),
                ("FoodJapaneseDishDescriptions", 29),
                ("FoodBakeryVenues", 2),
                ("FoodEateryVenues", 3),
                ("QuestionNamesVietnameseMale", 100),
                ("QuestionNamesVietnameseFemale", 100),
                ("QuestionNamesEnglishMale", 20),
                ("QuestionNamesEnglishFemale", 20),
                ("QuestionNamesVietnameseMaleRoles", 7),
                ("QuestionNamesVietnameseFemaleRoles", 7),
                ("QuestionNamesEnglishMaleRoles", 4),
                ("QuestionNamesEnglishFemaleRoles", 4),
                ("ArithmeticSceneIds", 22),
                ("OpenProseVietnameseOpenings", 6),
                ("OpenProseEnglishOpenings", 5),
                ("AppliedExpandedUnits", 22),
                ("AppliedCountedActivityGroups", 9),
                ("AppliedSeparatedUnitIds", 8),
                ("BreadBakeryLegacyUnitIds", 2),
                ("FoodThemeLegacyItemNames", 8),
                ("FoodThemeLegacySubjectUnitIds", 3),
                ("FoodDessertShopTranslations", 3),
                ("FoodDrinkShopActorPhrases", 3),
                ("FoodVenuePlurals", 4)
            })
            {
                Check(lists.TryGetProperty(name, out var data), "Missing JSON catalogue " + name);
                Check(data.GetArrayLength() == expectedCount, "Changed JSON catalogue count " + name);
            }
        }
        Check(FoodThemeCatalogue.Subjects.Count == 210, "Food subject registry changed during JSON migration.");
        Check(FoodThemeCatalogue.Subjects.Skip(BreadQuestionCatalogue.Types.Count)
            .SequenceEqual(QuizContentCatalog.LoadList<FoodThemeCatalogue.Subject>("FoodThemeSubjects")),
            "Food subject metadata must come from the cached JSON catalogue.");
        Check(ReferenceEquals(PetAnimalThemeCatalogue.Subjects,
                QuizContentCatalog.LoadList<PetAnimalSubject>("PetAnimalThemeSubjects"))
            && ReferenceEquals(ToyThemeCatalogue.Subjects,
                QuizContentCatalog.LoadList<ToyThemeSubject>("ToyThemeSubjects"))
            && ReferenceEquals(SchoolThemeCatalogue.Subjects,
                QuizContentCatalog.LoadList<SchoolThemeSubject>("SchoolThemeSubjects"))
            && ReferenceEquals(FractionThemeCatalogue.Subjects,
                QuizContentCatalog.LoadList<FractionThemeSubject>("FractionThemeSubjects")),
            "Topic catalogues must use cached typed JSON lists.");
        var fruits = FoodThemeCatalogue.Subjects.Where(s => s.Group == QuestionKnowledgeGroup.Fruit).ToArray();
        var fruitPlants = QuizContentCatalog.LoadList<FruitGardenPlantUnit>("FruitGardenPlantUnits");
        Check(fruits.Length == fruitPlants.Count
            && fruits.All(fruit => fruitPlants.Count(plant => plant.UnitId == fruit.Unit.Id) == 1)
            && fruitPlants.All(plant => !string.IsNullOrWhiteSpace(plant.Vietnamese)
                && !string.IsNullOrWhiteSpace(plant.English)
                && !string.IsNullOrWhiteSpace(plant.EnglishSingular)
                && plant.English != plant.EnglishSingular),
            "Fruit planting unit metadata must cover each fruit in JSON exactly once.");
        Check(fruitPlants.Single(plant => plant.UnitId == "bananas").English == "banana plants"
            && fruitPlants.Single(plant => plant.UnitId == "grapes").English == "grape vines"
            && fruitPlants.Single(plant => plant.UnitId == "durians").English == "durian trees",
            "Fruit planting unit names changed during migration.");
        Check(QuestionUnits.All.Count == 77, "Question unit registry changed during JSON migration.");
        var expectedUnits = QuizContentCatalog.LoadList<QuestionUnit>("QuestionUnits")
            .Concat(ToyThemeCatalogue.Subjects.Select(subject => subject.Unit))
            .Concat(PetAnimalThemeCatalogue.Subjects.Select(subject => subject.Unit));
        Check(QuestionUnits.All.SequenceEqual(expectedUnits)
            && QuestionUnits.All.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).Count() == QuestionUnits.All.Count,
            "Question unit registry must combine the JSON-backed lists without duplicate IDs.");
        Check(ReferenceEquals(QuestionNames.VietnameseMale, QuizContentCatalog.LoadList<string>("QuestionNamesVietnameseMale"))
            && ReferenceEquals(QuestionNames.VietnameseFemale, QuizContentCatalog.LoadList<string>("QuestionNamesVietnameseFemale")),
            "Given-name pools should share the cached JSON lists.");
        Check(QuestionNames.VietnameseMale.Distinct(StringComparer.Ordinal).Count() == 100
            && QuestionNames.VietnameseFemale.Distinct(StringComparer.Ordinal).Count() == 100
            && QuestionNames.VietnameseMale[0] == "Huy" && QuestionNames.VietnameseFemale[^1] == "Chinh",
            "Vietnamese given-name data changed during migration.");
        var arithmeticSceneIds = QuizContentCatalog.LoadList<string>("ArithmeticSceneIds");
        Check(arithmeticSceneIds.Distinct(StringComparer.Ordinal).Count() == arithmeticSceneIds.Count
            && ArithmeticQuestionCatalogue.Scenes.Select(scene => scene.Id).SequenceEqual(arithmeticSceneIds),
            "Arithmetic scene selection must preserve the JSON list order.");
        var appliedExpandedUnits = QuizContentCatalog.LoadList<AppliedExpandedUnit>("AppliedExpandedUnits");
        Check(appliedExpandedUnits.Select(unit => unit.Id).Distinct(StringComparer.Ordinal).Count() == appliedExpandedUnits.Count
            && appliedExpandedUnits.Single(unit => unit.Id == "rolls").Vi == "ổ bánh mì",
            "Applied unit translations changed during JSON migration.");
        var activityGroups = QuizContentCatalog.LoadList<AppliedCountedActivityGroup>("AppliedCountedActivityGroups");
        Check(activityGroups.Select(group => group.Id).Distinct(StringComparer.Ordinal).Count() == activityGroups.Count
            && activityGroups.Single(group => group.Id == "bakery").En == "bakery",
            "Activity group translations changed during JSON migration.");
        Check(ReferenceEquals(AdditionQuestionCatalogue.Scenes, QuizContentCatalog.LoadList<AdditionScene>("AdditionScenes")),
            "Addition scenes must load directly from the cached JSON catalogue.");
        Check(AdditionQuestionCatalogue.Scenes.Select(scene => scene.Id).Distinct(StringComparer.Ordinal).Count() == 29,
            "Addition scene IDs must stay unique.");
        Check(AdditionQuestionCatalogue.Find("bakery") is { } bakery
            && bakery.UnitIds.SequenceEqual(BreadQuestionCatalogue.BakeryUnitIds)
            && bakery.EnglishBaseVerb == "prepare" && bakery.EnglishPastVerb == "prepared",
            "Bakery scene and saved-question compatibility changed during JSON migration.");
        Check(FoodThemeCatalogue.Subjects.Any(s => s.Unit.Id == "com-tam" && s.ArtworkId == "com_tam"),
            "Food topic artwork mapping was lost.");
        Check(FractionThemeCatalogue.Subjects.Any(s => s.Id == "school-drawing-paper" && s.VietnameseUnit == "m²"),
            "Fraction unit metadata was lost.");
        Check(PetAnimalThemeCatalogue.Subjects.Single(s => s.Unit.Id == "arowanas").MaxPerGroup == 1,
            "Pet welfare constraint was lost.");
        Check(FoodThemeCatalogue.IsCheDessert("che-dau-do")
            && FoodThemeCatalogue.DessertShopKind("che-dau-do") == "ch\u00e8",
            "Vietnamese sweet-soup venue was corrupted during JSON migration.");
        Check(FoodThemeCatalogue.DessertShopKind("sua-chua-ha-long") == "s\u1eefa chua",
            "Vietnamese yogurt venue was corrupted during JSON migration.");
        foreach (string activityPrefix in new[]
        {
            "ElementaryQuizGenerator.DecimalStories.Activity.",
            "ElementaryQuizGenerator.MeasurementStories.Activity.",
            "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.Activity.",
            "ElementaryQuizGenerator.TimeDifficulty.CreateTimeDifficulty.Activity."
        })
        {
            string requiredId = en.Texts.First(text => text.Id.StartsWith(activityPrefix, StringComparison.Ordinal)).Id;
            var missingActivityText = new QuizContentPack
            {
                Version = en.Version,
                Culture = en.Culture,
                Texts = en.Texts.Where(text => text.Id != requiredId).ToList(),
                Lists = en.Lists
            };
            Reject(() => QuizContentValidation.CheckLists(missingActivityText), "missing activity text " + requiredId);
        }
        // Food units appear in question prompts, answer units, AI-generated
        // exercises and the PDF/Excel interchange paths. Keep the shared source
        // text intact so every consumer receives proper Vietnamese spelling.
        string[] vietnameseFoodUnits =
        [
            "suất cơm tấm", "phần trứng ốp-la", "phần thịt kho trứng",
            "chiếc bánh xèo", "tô bún thịt nướng chả giò", "tô hủ tiếu Nam Vang",
            "tô mì Quảng", "tô bún bò Huế", "đĩa bánh cuốn",
            "đĩa kim chi", "phần sushi"
        ];
        var fractionFoodUnits = vi.Texts.Where(text =>
            text.Id.StartsWith("Fraction.fraction-theme-food-", StringComparison.Ordinal)
            && (text.Id.EndsWith(".UnitA", StringComparison.Ordinal)
                || text.Id.EndsWith(".UnitB", StringComparison.Ordinal)
                || text.Id.EndsWith(".ResultUnit", StringComparison.Ordinal))).ToArray();
        Check(fractionFoodUnits.Length > 0, "Missing fraction-food units from the Vietnamese quiz pack.");
        foreach (string expected in vietnameseFoodUnits)
            Check(fractionFoodUnits.Any(text => text.Text == expected), "Missing correctly accented food unit: " + expected);
        Check(fractionFoodUnits.All(text => !text.Text.Contains('?')),
            "Fraction-food unit text contains replacement question marks instead of Vietnamese diacritics.");
        foreach (var pack in new[] { vi, en })
        {
            QuizContentValidation.CheckTranslation(pack, pack);
            foreach (var entry in pack.Texts)
            {
                var values = entry.Variables.Select(v => (v, "<{data}>:" + v)).ToArray();
                var rendered = QuizContentCatalog.Text(pack.Culture, entry.Id, values);
                Check(!entry.Variables.Any(v => rendered.Contains("{" + v + "}", StringComparison.Ordinal)), "Unrendered value: " + entry.Id);
            }
        }
        var first = en.Texts.First(t => t.Variables.Length == 0);
        Check(QuizContentCatalog.Text("fr-FR", first.Id) == first.Text, "Missing culture fallback failed.");
        Reject(() => QuizContentCatalog.Text("en-US", "unknown.text.id"), "missing key");
        var variable = en.Texts.First(t => t.Variables.Length > 0);
        Reject(() => QuizContentCatalog.Text("en-US", variable.Id), "missing values");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "en-US", Texts = [first, first] }), "duplicate key");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "en-US", Texts = [new("typo", "{wrong_slot}", [])] }), "unknown slot");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "vi-VN",
            Texts = [new("unit-text", "{a} {unit}", ["a", "unit"], new() { ["kg"] = "{a}" })] }),
            "unit wording lost a fact or dimension");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "vi-VN",
            Texts = [new("unit-text", "{a} {unit}", ["a", "unit"], new() { ["kg"] = "{unit} {a}" })] }),
            "unit wording swapped ordered roles");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 8, Culture = "en-US" }), "version");
        var changed = new QuizContentPack { Version = 1, Culture = "fr-FR", Texts = en.Texts.Skip(1).ToList(), Lists = en.Lists };
        Reject(() => QuizContentValidation.CheckTranslation(changed, en), "incomplete translation");
        QuizContentPack ChangeRow(string list, string field, int value)
        {
            var rows = JsonNode.Parse(en.Lists[list].GetRawText())!;
            rows[0]![field] = value;
            return new() { Version = 1, Culture = "fr-FR", Texts = en.Texts,
                Lists = new(en.Lists) { [list] = JsonSerializer.SerializeToElement(rows) } };
        }
        Reject(() => QuizContentCatalog.Validate(ChangeRow("MotionQuizGenerator.UnitProfiles", "TimeDivisor", 0)), "invalid time conversion");
        Reject(() => QuizContentValidation.CheckTranslation(ChangeRow("AverageQuizGenerator.DirectContexts", "Capacity", 1), en), "changed mathematical metadata");
        Reject(() => QuizContentCatalog.Validate(ChangeRow("PercentageQuizGenerator.PercentageStories.Extensions", "Capacity", 0)), "invalid expanded scenario capacity");
        var duplicateContexts = JsonNode.Parse(en.Lists["AverageQuizGenerator.DirectContexts.Extensions"].GetRawText())!;
        duplicateContexts[0]!["Id"] = "library";
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "en-US", Texts = en.Texts,
            Lists = new(en.Lists) { ["AverageQuizGenerator.DirectContexts.Extensions"] = JsonSerializer.SerializeToElement(duplicateContexts) } }),
            "expanded catalogue collided with a historical context");
        QuizContentPack ChangeFractionContexts(Action<JsonArray> change)
        {
            var rows = JsonNode.Parse(en.Lists["FractionQuantityContexts"].GetRawText())!.AsArray();
            change(rows);
            return new() { Version = 1, Culture = "fr-FR", Texts = en.Texts,
                Lists = new(en.Lists) { ["FractionQuantityContexts"] = JsonSerializer.SerializeToElement(rows) } };
        }
        Reject(() => QuizContentCatalog.Validate(ChangeFractionContexts(rows => rows[0]!["ContextId"] = null)), "missing fraction context ID");
        Reject(() => QuizContentCatalog.Validate(ChangeFractionContexts(rows => rows[1]!["ContextId"] = rows[0]!["ContextId"]!.GetValue<string>())), "duplicate fraction context ID");
        Reject(() => QuizContentCatalog.Validate(ChangeFractionContexts(rows => rows[0]!["Quantity"] = "Unspecified")), "missing quantity policy");
        Reject(() => QuizContentCatalog.Validate(ChangeFractionContexts(rows => rows[0]!["PartProblemAlternatives"] = null)), "missing reviewed fraction facts");
        Reject(() => QuizContentCatalog.Validate(ChangeFractionContexts(rows => rows[0]!["WholeProblemAlternatives"]![0] = "Hỏi lượng còn lại?")), "changed fraction target/placeholders");
        Reject(() => QuizContentValidation.CheckTranslation(ChangeFractionContexts(rows => rows[5]!["Quantity"] = "Count"), en), "translation changed a measured quantity to count");
        QuizContentPack FractionMetadata(QuizContentPack pack) => new() { Version = 1, Culture = pack.Culture,
            Texts = pack.Texts.Where(text => text.Id.StartsWith("ElementaryQuizGenerator.FractionDifficulty.", StringComparison.Ordinal)).ToList(),
            Lists = new() { ["FractionQuantityContexts"] = pack.Lists["FractionQuantityContexts"],
                [ReviewedNarrativePhrasings.FractionQuantityListName] = pack.Lists[ReviewedNarrativePhrasings.FractionQuantityListName] } };
        QuizContentValidation.CheckTranslation(FractionMetadata(en), FractionMetadata(vi));
        QuizContentPack ChangeCharts(Action<JsonArray> change)
        {
            var rows = JsonNode.Parse(en.Lists["DataChartContexts"].GetRawText())!.AsArray();
            change(rows);
            return new() { Version = 1, Culture = "fr-FR", Lists = new() { ["DataChartContexts"] = JsonSerializer.SerializeToElement(rows) } };
        }
        Reject(() => QuizContentCatalog.Validate(ChangeCharts(rows => rows[0]!["ContextId"] = null)), "missing chart context ID");
        Reject(() => QuizContentCatalog.Validate(ChangeCharts(rows => rows[1]!["ContextId"] = rows[0]!["ContextId"]!.GetValue<string>())), "duplicate chart context ID");
        Reject(() => QuizContentCatalog.Validate(ChangeCharts(rows => rows[0]!["CategoryIds"] = null)), "missing chart category IDs");
        Reject(() => QuizContentCatalog.Validate(ChangeCharts(rows => rows[0]!["CategoryIds"]![1] = rows[0]!["CategoryIds"]![0]!.GetValue<string>())), "duplicate chart category ID");
        QuizContentPack ChartMetadata(QuizContentPack pack) => new() { Version = 1, Culture = pack.Culture,
            Lists = new() { ["DataChartContexts"] = pack.Lists["DataChartContexts"] } };
        QuizContentValidation.CheckTranslation(ChartMetadata(en), ChartMetadata(vi));
        Reject(() => QuizContentValidation.CheckTranslation(ChangeCharts(rows =>
        {
            string firstId = rows[0]!["CategoryIds"]![0]!.GetValue<string>();
            rows[0]!["CategoryIds"]![0] = rows[0]!["CategoryIds"]![1]!.GetValue<string>();
            rows[0]!["CategoryIds"]![1] = firstId;
        }), ChartMetadata(en)), "translation shuffled stable chart roles");
        var charts = QuizContentCatalog.LoadList<DataChartStoryContext>("DataChartContexts", "vi-VN");
        Check(ReferenceEquals(charts, QuizContentCatalog.LoadList<DataChartStoryContext>("DataChartContexts", "vi-VN")), "Lists are not cached.");
        var applied = QuizContentCatalog.LoadList<AppliedQuestionScene>("AppliedScenes");
        var fractions = QuizContentCatalog.LoadList<FractionQuestionCatalogue.Scene>("FractionScenes");
        // Catalogues may grow as new topics are introduced; retain the historical coverage floor.
        Check(applied.Count >= 849 && applied.Select(s => s.Id).Distinct().Count() == applied.Count, "Lost or duplicate applied scenes.");
        Check(fractions.Count >= 256 && fractions.Select(s => s.Id).Distinct().Count() == fractions.Count, "Lost or duplicate fraction scenes.");
        Console.WriteLine($"Quiz JSON content passed: {vi.Texts.Count} keys per language, {applied.Count + fractions.Count} unique scenes, typed/cached lists, fallback, slot rendering and malformed-pack rejection.");
    }
}
