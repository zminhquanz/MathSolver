using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using SQLite;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class NarrativeDepthTests
{
    internal static readonly BankQuestionFamily[] Families = [BankQuestionFamily.TwoNumbers,
        BankQuestionFamily.MultiStep, BankQuestionFamily.Average, BankQuestionFamily.Percentage,
        BankQuestionFamily.Proportion, BankQuestionFamily.Motion];

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    internal static async Task RunAsync()
    {
        QuizContentTests.Run();
        int cases = 0;
        foreach (var family in Families)
        {
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            {
                var variants = ReasoningStoryCatalogue.Variants(family, tier);
                for (int seed = 0; seed < 24; seed++)
                {
                    var c = ReasoningStoryCatalogue.Create(family, variants[seed % variants.Length], tier, language, new(seed + 300));
                    Check(c.Story!.ContextVersion == NarrativeContextExpansion.Version && c.IsValid, "Invalid expanded profile");
                    Verify(c);
                    cases++;
                }
                var legacy = ReasoningStoryCatalogue.Create(family, variants[0], tier, language, new(31), contextVersion: 0);
                var node = JsonNode.Parse(JsonSerializer.Serialize(legacy))!;
                node["Story"]!.AsObject().Remove("ContextVersion");
                var restored = JsonSerializer.Deserialize<BasicQuestionContract>(node.ToJsonString())!;
                Check(restored.Story!.ContextVersion == 0 && restored.IsValid, "Legacy catalogue selection changed");
                Check(restored.ExactAnswers.SequenceEqual(legacy.ExactAnswers), "Legacy numerical answers changed");
                Verify(restored);
                cases++;
            }
        }
        var settings = new Dictionary<BankQuestionFamily, string[]> {
            [BankQuestionFamily.TwoNumbers] = ["kitchen", "livestock", "agriculture", "distribution", "environment", "decoration", "water"],
            [BankQuestionFamily.MultiStep] = ["kitchen", "livestock", "water", "environment", "agriculture"],
            [BankQuestionFamily.Average] = ["reading-pages", "bakery-batches", "nursery-seedlings", "swimming-training", "volunteer-gifts", "commute-duration"],
            [BankQuestionFamily.Percentage] = ["school-attendance", "nursery-quality", "library-returns", "delivery-progress", "household-budget", "sports-registration", "sewing-progress", "recycling-sorting"] };
        foreach (var (family, ids) in settings)
        foreach (string id in ids)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            int variant = family == BankQuestionFamily.Average ? (int)AverageQuizType.Direct
                : ReasoningStoryCatalogue.Variants(family, tier)[0];
            BasicQuestionContract? chosen = null;
            for (int seed = 0; seed < 1000 && chosen is null; seed++)
            {
                var candidate = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(seed));
                if (candidate.SceneId == id) chosen = candidate;
            }
            Check(chosen is not null, "Unavailable context: " + family + "/" + id);
            Verify(chosen!); cases++;
        }
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (int variant in ReasoningStoryCatalogue.Variants(BankQuestionFamily.Proportion, tier))
        foreach (string id in ProportionQuizGenerator.NarrativeIds((ProportionQuizType)variant, tier, language)
            .Where(id => int.Parse(id.Split('.').Last()) >= 48))
        {
            Verify(ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion, variant, tier, language, new(73), id));
            cases++;
        }

        // Save new contexts, replay with fresh values and preserve catalogue version through Excel.
        string directory = Path.Combine(Path.GetTempPath(), "MathSolver-depth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
            foreach (var family in Families)
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            {
                var c = NativeContract(family, language);
                var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
                Check(await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow)), "Context insert failed");
                var saved = await store.TakeReasoningAsync(family, c.BankVariant, c.Tier, language);
                Check(saved?.Contract.Story?.ContextVersion == NarrativeContextExpansion.Version, "SQLite lost catalogue version");
                var fresh = saved!.Contract.FreshFacts(new(192));
                Check(fresh.SceneId == c.SceneId, "Fresh values changed the situation");
                ProseExpansionTests.VerifyPractice(fresh, saved.Draft);
            }
            using var excel = new MemoryStream();
            Check((await store.ExportExcelAsync(excel)).Exported == 12, "Excel export lost contexts");
            excel.Position = 0;
            var imported = new QuestionBankStore(Path.Combine(directory, "import.db3"));
            Check((await imported.ImportExcelAsync(excel)).Inserted == 12, "Excel import lost contexts");
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine($"Narrative depth: PASS {cases} expanded/legacy cases, all six families, 1–5 stars, Vietnamese/English, grammar/roles, grading, SQLite/Excel and fresh values.");
    }

    private static void Verify(BasicQuestionContract c)
    {
        string label = $"{c.Family}/{c.BankVariant}/{c.Tier}/{c.Language}/{c.SceneId}/{c.Story!.Seed}";
        try
        {
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Reviewed roles rejected");
            ProseExpansionTests.VerifyPractice(c, draft);
            var fresh = c.FreshFacts(new(911));
            Check(fresh.Story!.ContextVersion == c.Story!.ContextVersion, "Fresh values lost catalogue version");
            ProseExpansionTests.VerifyPractice(fresh, draft);
            _ = ReasoningStoryValidator.Grammar(c);
        }
        catch (Exception error) { throw new InvalidOperationException(label, error); }
    }

    internal static BasicQuestionContract NativeContract(BankQuestionFamily family, AppLanguage language)
    {
        var tier = CurriculumTier.FiveStars;
        int variant = ReasoningStoryCatalogue.Variants(family, tier)[0];
        if (family == BankQuestionFamily.Average) variant = (int)AverageQuizType.Direct;
        if (family == BankQuestionFamily.Proportion) variant = (int)ProportionQuizType.Direct;
        for (int seed = 0; seed < 1000; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(seed),
                family == BankQuestionFamily.Proportion ? "ProportionQuizGenerator.Narrative.052" : "");
            bool chosen = family switch
            {
                BankQuestionFamily.TwoNumbers or BankQuestionFamily.MultiStep => c.SceneId == "kitchen",
                BankQuestionFamily.Average => c.SceneId == "nursery-seedlings",
                BankQuestionFamily.Percentage => c.SceneId == "delivery-progress",
                BankQuestionFamily.Proportion => true,
                _ => ReasoningStoryCatalogue.Lesson(c).QuestionModel.MotionProblem!.ProblemText.Contains(language == AppLanguage.Vietnamese ? "giao hàng" : "delivery")
            };
            if (chosen) return c;
        }
        throw new InvalidOperationException("No expanded native case for " + family);
    }

    internal static void CheckDatabase(string path, bool legacy)
    {
        using var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
        int cases = 0;
        foreach (var row in db.Table<QuestionBankStore.Row>().ToArray())
        {
            var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson)!;
            Check(c.IsValid, "Stored contract invalid: " + row.SceneId);
            var validation = BasicQuestionValidator.Validate(row.DraftJson, c);
            Check(validation.IsValid, "Stored prose invalid: " + row.SceneId);
            if (ReasoningStoryCatalogue.Supports(c.Family))
            {
                if (legacy) Check(c.Story!.ContextVersion == 0, "Evidence must predate expanded catalogue");
                ProseExpansionTests.VerifyPractice(c, validation.Draft!);
                ProseExpansionTests.VerifyPractice(c.FreshFacts(new(631)), validation.Draft!);
            }
            cases++;
        }
        Check(cases > 0, "Empty legacy evidence");
        Console.WriteLine($"{(legacy ? "Legacy" : "Expanded")} real-model SQLite evidence: PASS {cases} saved records, validation and fresh-value grading.");
    }
}
