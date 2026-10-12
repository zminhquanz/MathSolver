using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class EquivalentProseTests
{

    internal static async Task RunAsync()
    {
        foreach (var (language, original, equivalent, bad) in new[] {
            (AppLanguage.Vietnamese, "{name} có {a} {unit},", "{a} {unit} thuộc về {name},", "{a} {unit} thuộc về {other},"),
            (AppLanguage.English, "{name} has {a} {unit},", "{a} {unit} belong to {name},", "{b} {unit} belong to {name},"),
            (AppLanguage.Vietnamese, "{name} nhận thêm {b} {unit}.", "{b} {unit} được bổ sung thêm cho {name}.", "{name} mượn thêm {b} {unit}."),
            (AppLanguage.English, "{name} receives {b} more {unit}.", "{name} is given {b} more {unit}.", "{name} borrows {b} more {unit}."),
            (AppLanguage.Vietnamese, "Mỗi {group_one} chứa {a} {unit},", "{a} {unit} được chứa trong mỗi {group_one},", "Mỗi {unit} chứa {a} {group_one},"),
            (AppLanguage.English, "Each {group_one} contains {a} {unit},", "There are {a} {unit} in each {group_one},", "There are {a} {group_one} in each {unit},"),
            (AppLanguage.Vietnamese, "Trước đó {name} đã bán {b} {unit}.", "{name} đã bán {b} {unit} trước đó.", "{name} đã bán {b} {unit} sau đó."),
            (AppLanguage.Vietnamese, "Trong {f0} ngày, một thư viện nhận được lần lượt {f1}, {f2}, {f3} {v0}.",
                "Một thư viện nhận được lần lượt {f1}, {f2}, {f3} {v0} trong {f0} ngày.",
                "Một thư viện nhận được lần lượt {f1}, {f2}, {f3} {v0} sau {f0} ngày."),
            (AppLanguage.Vietnamese, "Phần thứ nhất gồm {f3} nhóm bằng nhau, mỗi nhóm {f4} {v1}.",
                "Trong phần thứ nhất, mỗi nhóm trong {f3} nhóm bằng nhau có {f4} {v1}.",
                "Trong phần thứ nhất, mỗi nhóm trong {f4} nhóm bằng nhau có {f3} {v1}."),
            (AppLanguage.English, "The first portion has {f3} equal groups of {f4} {v1}.",
                "Each of the {f3} equal groups in the first portion contains {f4} {v1}.",
                "Each of the {f4} equal groups in the first portion contains {f3} {v1}."),
            (AppLanguage.Vietnamese, "Số phiếu chọn đọc sách chiếm bao nhiêu phần trăm số phiếu khảo sát?",
                "Tỉ lệ phần trăm của số phiếu chọn đọc sách so với số phiếu khảo sát là bao nhiêu?",
                "Tỉ lệ phần trăm của số phiếu khảo sát so với số phiếu chọn đọc sách là bao nhiêu?") })
        {
            Check(SemanticProseRules.Matches(equivalent, [original], language), "Equivalent structure rejected: " + equivalent);
            Check(!SemanticProseRules.Matches(bad, [original], language), "Changed mathematical role accepted: " + bad);
            Check(!SemanticProseRules.Matches(equivalent.Replace("{a}", "{A}"), [original], language) || !equivalent.Contains("{a}"), "Case-sensitive slot weakened");
            Check(!SemanticProseRules.Matches(equivalent + " 123", [original], language), "Literal new fact accepted");
            Check(!SemanticProseRules.Matches(equivalent + "}", [original], language), "Malformed placeholder syntax accepted");
        }
        int count = 0;
        var singular = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
            CurriculumTier.OneStar, AppLanguage.English, new(17), "object-total") with { Left = 1 };
        Check(singular.IsValid && AppliedQuestionCatalogue.Render("{a} {unit} belong to {name}", singular).Contains("1 book belongs to"),
            "Fresh singular ownership verb does not agree");
        Check(AppliedQuestionCatalogue.Render("There are {a} {unit} in each {group}", singular).StartsWith("There is 1 book"),
            "Fresh singular per-group verb does not agree");
        Check(AppliedQuestionCatalogue.Render("{a} {unit} were sold by {name}", singular).Contains("1 book was sold"),
            "Fresh singular passive sale verb does not agree");
        foreach (var template in OneStepQuestionCatalogue.All)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            var c = template.CreateFacts(language, new(32));
            var p = ReviewedQuestionProse.For(c)!;
            var original = OneStepQuestionCatalogue.Draft(c);
            foreach (string a in p.GivenA)
            {
                var d = original with { GivenA = a };
                var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c);
                Check(validation.IsValid, $"One-step structure rejected {c.SceneId}/{c.Structure}/{language}: {validation.ErrorCode}");
                var fresh = c.FreshFacts(new(23));
                Check(!d.ToWordProblem(fresh).ProblemText.Contains('{'), "Fresh facts left placeholders");
                count++;
            }
            foreach (string b in p.GivenB)
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(original with { GivenB = b }), c).IsValid,
                    $"Second factual structure rejected: {c.SceneId}/{c.Structure}/{language}");
        }
        // Applied and Find X use the same quantity/owner anchor, with a different
        // mathematical target. Validation must preserve that target separately.
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (bool findX in new[] { false, true })
        {
            var c = findX ? FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
                CurriculumTier.ThreeStars, language, new(17), FindXUnknownRole.Addend)
                : AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
                    CurriculumTier.ThreeStars, language, new(17), "object-total");
            var p = ReviewedQuestionProse.For(c)!;
            var d = findX ? FindXQuestionCatalogue.Draft(c) : AppliedQuestionCatalogue.Draft(c);
            foreach (string a in p.GivenA)
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d with { GivenA = a }), c).IsValid,
                    "Applied/Find X structure rejected");
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d with { Question = "Hỏi mỗi {group} chứa bao nhiêu {unit}?" }), c).IsValid,
                "Changed target passed");
        }
        string directory = Path.GetFullPath("artifacts/verification/prose-structures-fixtures");
        Directory.CreateDirectory(directory);
        foreach (var family in new[] { BankQuestionFamily.Average, BankQuestionFamily.Percentage, BankQuestionFamily.MultiStep })
        {
            var c = ReasoningStoryCatalogue.Create(family, ReasoningStoryCatalogue.Variants(family, CurriculumTier.FiveStars).First(),
                CurriculumTier.FiveStars, AppLanguage.Vietnamese, new(19));
            await File.WriteAllTextAsync(Path.Combine(directory, family + ".json"), QuestionBankStore.SerializeDraft(ReasoningStoryCatalogue.Draft(c)));
            var prompt = BasicQuestionPrompt.Build(c, "ChangedRelationOrTarget", errorDetails: "question, solution_leads.step_0");
            Check(prompt.Contains("Sửa các trường: question, solution_leads.step_0") && prompt.Contains("Roles:"), "Reasoning repair lacks field/quantity roles");
        }
        // Run the actual worker: wrong owner in a valid JSON field, then correction.
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            var runtime = new RepairRuntime();
            var worker = new AiQuestionGenerationService(runtime, new QuestionBankStore(Path.Combine(directory, Guid.NewGuid() + ".db3")));
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, 1, true, new(QuestionKnowledgeGroup.Objects)));
            await worker.Completion;
            var item = worker.Snapshot.Items.Single();
            Check(worker.Snapshot.State == AiJobState.Completed && item.State == AiItemState.Saved && item.Attempts.Count == 2,
                "Worker did not recover from role mismatch");
            Check(item.Attempts[0].ErrorDetails?.Contains("given_a") == true
                && runtime.Prompts[1].Contains(language == AppLanguage.Vietnamese ? "Sửa các trường: given_a" : "Repair fields: given_a")
                && runtime.Prompts[1].Contains("given_a: ") && !runtime.Prompts[1].Contains("untrusted-model-text"),
                "Field repair was dropped or echoed untrusted model prose");
        }
        Console.WriteLine($"PASS {count} one-step structures, bilingual role/unit/chronology adversaries, Find X targets, field repairs and SQLite worker recovery.");
    }

    private sealed class RepairRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "field-repair-test";
        public List<string> Prompts { get; } = [];
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Prompts.Add(prompt);
            var d = AppliedQuestionCatalogue.Draft(c);
            if (Prompts.Count == 1) d = d with { GivenA = "untrusted-model-text " + d.GivenA };
            return Task.FromResult(QuestionBankStore.SerializeDraft(d));
        }
    }
}
