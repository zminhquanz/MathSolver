using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text;
using System.Text.Json;

internal static partial class QuantityDepthTests
{

    internal static IEnumerable<BasicQuestionContract> NativeCases()
    {
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            int i = 0;
            foreach (var type in ElementaryQuizGenerator.DecimalStoryTypes)
            {
                var language = ((int)tier + i) % 2 == 0 ? AppLanguage.English : AppLanguage.Vietnamese;
                string context = new[] { "sewing-uniforms", "juice-bottling", "feed-preparation", "rope-cutting", "rice-packing", "bakery-batches" }[((int)tier + i++) % 6];
                yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.Decimal, (int)type, tier, language, new(71), context);
            }
            foreach (var type in ElementaryQuizGenerator.FractionQuantityStoryTypes)
            {
                var language = type == ElementaryQuizType.FractionOfNumber ? AppLanguage.Vietnamese : AppLanguage.English;
                string context = new[] { "reading-pages", "bread-flour", "juice-capacity", "sports-registration", "route-distance" }[(int)tier - 1];
                yield return ReasoningStoryCatalogue.Create(BankQuestionFamily.FractionQuantity, (int)type, tier, language, new(71), context);
            }
        }
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var family in new[] { BankQuestionFamily.Decimal, BankQuestionFamily.FractionQuantity })
        foreach (int variant in ReasoningStoryCatalogue.Variants(family, tier).Where(v => !ElementaryQuizGenerator.IsTimeFractionExtension((ElementaryQuizType)v)))
        {
            var contexts = family == BankQuestionFamily.Decimal
                ? ElementaryQuizGenerator.DecimalContexts(language).Select(c => c.Id)
                : FractionQuantityStoryContextCatalog.GetProfile(language).Select(c => c.ContextId);
            foreach (string context in contexts)
            {
                var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(63), context);
                Verify(c); count++;
                bool old = family == BankQuestionFamily.Decimal
                    ? ElementaryQuizGenerator.DecimalContexts(language, false).Any(c => c.Id == context)
                    : FractionQuantityStoryContextCatalog.GetProfile(language, false).Any(c => c.ContextId == context);
                if (!old) continue;
                foreach (int version in new[] { 0, 1 })
                {
                    var legacy = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(63), context, contextVersion: version);
                    Verify(legacy); count++;
                }
            }
        }
        // A number must not change the template's identity; SQLite and Excel
        // must retain the new activity and the catalogue replay version.
        using var workspace = TestWorkspace.Temporary("QuantityDepthTests");
        string directory = workspace.DirectoryPath;
        var store = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
        foreach (var c in NativeCases())
        {
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow)), "Insert failed");
            Check(!await store.InsertAsync(new(c.FreshFacts(new(92)), draft, QuestionBankStore.SerializeDraft(draft), "test", DateTime.UtcNow)), "Changed numbers bypassed duplicate detection");
        }
        using var excel = new MemoryStream();
        int exported = (await store.ExportExcelAsync(excel)).Exported;
        excel.Position = 0;
        var restored = new QuestionBankStore(Path.Combine(directory, "import.db3"));
        Check((await restored.ImportExcelAsync(excel)).Inserted == exported && exported == 30, "Excel lost quantity stories");
        foreach (var c in NativeCases())
        {
            var saved = await restored.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(saved?.Contract.Story?.ContextVersion == NarrativeContextExpansion.Version, "Lost quantity catalogue version");
            ProseExpansionTests.VerifyPractice(saved!.Contract.FreshFacts(new(31)), saved.Draft);
        }
        Console.WriteLine($"Quantity depth: PASS {count} current/legacy profiles, 1–5 stars, Vietnamese/English; conservation, capacities, semantic rejection, grading, SQLite and Excel.");
    }

    private static void Verify(BasicQuestionContract c)
    {
        string label = $"{c.Family}/{c.BankVariant}/{c.Tier}/{c.Language}/{c.SceneId}/v{c.Story!.ContextVersion}";
        try
        {
            Check(c.IsValid, "Invalid contract");
            var original = ReasoningStoryCatalogue.Draft(c);
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original), c).IsValid, "Base rejected");
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Reviewed wording rejected");
            ProseExpansionTests.VerifyPractice(c, draft);
            ProseExpansionTests.VerifyPractice(c.FreshFacts(new(19)), draft);
            _ = ReasoningStoryValidator.Grammar(c);
            var foreign = draft with { Question = draft.Question + " łącznie" };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(foreign), c).IsValid, "Foreign word accepted");
            var changed = draft with { Facts = draft.Facts!.Select(f => f with { Text = f.Text.Replace("{f0}", "{f999}") }).ToArray() };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), c).IsValid, "Wrong quantity role accepted");
            if (c.Family == BankQuestionFamily.Decimal)
            {
                var context = ElementaryQuizGenerator.DecimalContexts(c.Language).Single(x => x.Id == c.SceneId);
                var q = ReasoningStoryCatalogue.Lesson(c).QuestionModel.ElementaryProblem!;
                var g = q.Reasoning!.Givens.ToDictionary(x => x.Role, x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
                decimal amount = g["quantity"] / ((int)c.Tier == 5 ? context.ConversionFactor : 1);
                if (context.GroupMaximum > 0 && q.Type is ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
                {
                    if (q.Type == ElementaryQuizType.DecimalDivide) amount /= g.GetValueOrDefault("portion-count", g.GetValueOrDefault("second-quantity"));
                    if ((int)c.Tier >= 3) amount += q.Type == ElementaryQuizType.DecimalMultiply ? .5m : .5m + ((int)c.Tier >= 4 ? .25m : 0);
                    Check(amount <= context.GroupMaximum, "Per-group capacity exceeded");
                }
                var answer = c.ExactAnswer;
                Check(answer.Numerator > 0 && answer.Numerator <= context.MaximumQuantity * answer.Denominator, "Total capacity exceeded");
            }
            else if (c.Tier == CurriculumTier.FourStars && c.Story.ContextVersion >= 2)
            {
                var givens = ReasoningStoryCatalogue.Lesson(c).QuestionModel.ElementaryProblem!.Reasoning!.Givens
                    .ToDictionary(g => g.Role, g => decimal.Parse(g.Value, CultureInfo.InvariantCulture));
                Check(givens["removed"] <= Math.Min(givens["quantity-first"], givens["quantity-second"]), "Impossible overlapping records");
            }
        }
        catch (Exception error) { throw new InvalidOperationException(label, error); }
    }


}
