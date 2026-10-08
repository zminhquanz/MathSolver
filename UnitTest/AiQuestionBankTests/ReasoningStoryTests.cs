using MathSolver.Services;
using MathSolver.Models;
using MathSolver.Services.QuestionBank;

internal static class ReasoningStoryTests
{
    public static async Task CheckPromptBudgetAsync(string path)
    {
        // Tokenize the exact Gemma chat framing, without allocating a KV cache or
        // running inference. Include retry instructions in the worst-case budget.
        using var weights = await LLama.LLamaWeights.LoadFromFileAsync(new LLama.Common.ModelParams(path)
            { GpuLayerCount = 0, UseMemorymap = true });
        int maximum = 0, count = 0;
        foreach (var family in new[] { BankQuestionFamily.TwoNumbers, BankQuestionFamily.Average, BankQuestionFamily.Percentage, BankQuestionFamily.MultiStep })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var variant in ReasoningStoryCatalogue.Variants(family, tier))
        for (int seed = 0; seed < 4; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(seed));
            string prose = ReasoningStoryCatalogue.Draft(c).ProblemText;
            foreach (string prompt in new[] { BasicQuestionPrompt.Build(c, "ChangedRelationOrTarget"),
                BasicQuestionPrompt.Build(c, "DuplicateProse", [prose, "According to the report, " + prose]) })
            {
                int tokens = weights.Tokenize($"<|turn>user\n{prompt}<turn|>\n<|turn>model\n", true, true, System.Text.Encoding.UTF8).Length;
                GgufQuestionRuntime.GetContextTokens(tokens);
                maximum = Math.Max(maximum, tokens);
            }
            count++;
        }
        Console.WriteLine($"Gemma prompt budget: {count} cases, maximum {maximum} input tokens; fits the 2048-token context with 700 reserved output tokens.");
    }

    public static async Task RunModelAsync(string path)
    {
        string directory = Path.Combine("artifacts", "verification", "reasoning-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        var runtime = new GgufQuestionRuntime();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        await runtime.LoadAsync(path, timeout.Token);
        try
        {
            foreach (var (family, variant, tier) in new[] {
                (BankQuestionFamily.TwoNumbers, 0, CurriculumTier.ThreeStars),
                (BankQuestionFamily.Average, 4, CurriculumTier.FourStars),
                (BankQuestionFamily.Percentage, 2, CurriculumTier.FiveStars) })
            {
                var c = ReasoningStoryCatalogue.Create(family, variant, tier, AppLanguage.Vietnamese, new(19));
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token);
                    var validated = BasicQuestionValidator.Validate(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, $"{family}-{attempt}.txt"), prompt + "\n\n" + raw + "\n" + validated.ErrorCode);
                    Console.WriteLine($"{family} {attempt}: {validated.ErrorCode ?? "Valid"}");
                    if (validated.IsValid) { passed = true; break; }
                    correction = validated.ErrorCode;
                }
                if (!passed) throw new Exception("Real model failed: " + family + "; evidence: " + directory);
            }
        }
        finally { await runtime.EjectAsync(); }
        Console.WriteLine("Real GGUF reasoning prose passed; evidence: " + directory);
    }

    public static async Task RunAsync()
    {
        int count = 0;
        foreach (var family in new[] { BankQuestionFamily.TwoNumbers, BankQuestionFamily.Average, BankQuestionFamily.Percentage, BankQuestionFamily.MultiStep })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var variant in ReasoningStoryCatalogue.Variants(family, tier))
        {
            for (int seed = 0; seed < 4; seed++)
            {
                var c = ReasoningStoryCatalogue.Create(family, variant, tier, language, new(seed));
                if (!c.IsValid) throw new Exception($"Invalid {family}/{variant}/{tier}/{language}");
                var draft = ReasoningStoryCatalogue.Draft(c);
                using (var diagnostic = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(ReasoningStoryCatalogue.DiagnosticData(c))))
                    if (diagnostic.RootElement.GetProperty("Answer").GetString() != c.AnswerText
                        || diagnostic.RootElement.GetProperty("Quantities").GetArrayLength() == 0)
                        throw new Exception("Developer diagnostics lost the actual C# facts");
                string json = QuestionBankStore.SerializeDraft(draft);
                var validated = BasicQuestionValidator.Validate(json, c);
                if (!validated.IsValid)
                {
                    var lesson = ReasoningStoryCatalogue.Lesson(c);
                    foreach (var f in draft.Facts!) Console.WriteLine($"Fact {f.Role}: {SemanticProseRules.Matches(f.Text, [f.Text], language)}");
                    foreach (var f in draft.SolutionLeads!) Console.WriteLine($"Step {f.Role}: {SemanticProseRules.Matches(f.Text, [f.Text], language)}");
                    Console.WriteLine($"Question: {SemanticProseRules.Matches(draft.Question, [draft.Question], language)}");
                }
                if (!validated.IsValid) throw new Exception($"Rejected {family}/{variant}/{tier}/{language}: {validated.ErrorCode}\n{json}");
                var fresh = c.FreshFacts(new(seed + 99));
                if (fresh.Story!.Schema != c.Story!.Schema) throw new Exception("Changed schema");
                var q = ReasoningStoryCatalogue.ToPractice(fresh, validated.Draft!, ArithmeticQuizMode.Essay);
                if (q.Mode != ArithmeticQuizMode.Essay) throw new Exception("Changed mode");
                try
                {
                    fresh.ToPracticeQuestion(validated.Draft!.ToWordProblem(fresh), ArithmeticQuizMode.Essay);
                    throw new Exception("Structured story fell through the two-operand adapter");
                }
                catch (InvalidOperationException error) when (error.Message == "UseStructuredReasoningDraft") { }
                var submitted = EssayCombinedInputParser.Parse(ReasoningStoryCatalogue.Solution(fresh, validated.Draft), true,
                    preserveAllCalculations: true);
                var grade = new EssayAnswerValidator(new MathSolver.Services.Core.BasicArithmeticEngine())
                    .Validate(q, submitted.Solution, submitted.Equation, submitted.Answer);
                if (!grade.IsCorrect) throw new Exception($"C# work cannot be graded after prose projection: {family}/{variant}/{tier}/{language}\n"
                    + ReasoningStoryCatalogue.Solution(fresh, validated.Draft) + "\n" + grade);
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var practice = ReasoningStoryCatalogue.ToPractice(fresh, validated.Draft!, mode);
                    if (practice.CorrectAnswer != q.CorrectAnswer || practice.Mode != mode)
                        throw new Exception("Language template changed mathematics or answer mode");
                    if (mode == ArithmeticQuizMode.MultipleChoice && practice.ElementaryProblem is null
                        && (practice.Choices.Count != 4 || !practice.Choices.Contains(practice.CorrectAnswer)))
                        throw new Exception("Broken choices");
                }
                var changedRole = draft with { Facts = draft.Facts!.Select((f, i) => i == 0 ? f with { Role = "unknown_role" } : f).ToArray() };
                var answerLeak = draft with { Question = draft.Question + " 123" };
                var foreign = draft with { Question = draft.Question + " łącznie" };
                var extra = draft with { Question = draft.Question + (language == AppLanguage.Vietnamese ? " Không tính phần bị mất." : " Exclude the missing part.") };
                foreach (var bad in new[] { changedRole, answerLeak, foreign, extra })
                    if (BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid) throw new Exception("Unsafe prose passed");
                if (draft.Facts!.Count > 1)
                {
                    var swapped = draft with { Facts = draft.Facts.Select((f, i) => f with
                        { Text = i == 0 ? draft.Facts[1].Text : i == 1 ? draft.Facts[0].Text : f.Text }).ToArray() };
                    if (draft.Facts[0].Text != draft.Facts[1].Text
                        && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(swapped), c).IsValid)
                        throw new Exception("Swapped quantity roles passed");
                }
                var wrongUnit = draft with { Question = draft.Question.Replace("{v0}", "{v999}") };
                if (wrongUnit.Question != draft.Question
                    && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(wrongUnit), c).IsValid)
                    throw new Exception("Unknown owner or unit binding passed");
                var wrongCase = draft with { Facts = draft.Facts.Select(f => f with { Text = f.Text.Replace("{f", "{F") }).ToArray() };
                if (wrongCase.Facts.Any(f => f.Text.Contains("{F"))
                    && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(wrongCase), c).IsValid)
                    throw new Exception("Unrenderable case-sensitive variable passed");
                var novel = draft with { Question = (language == AppLanguage.Vietnamese ? "Theo báo cáo, " : "According to the report, ") + draft.Question };
                if (!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(novel), c).IsValid)
                    throw new Exception("Novel equivalent wording was restricted to the original sentence");
                int textStart = json.IndexOf("\"text\":", StringComparison.Ordinal) + 8;
                string prefix = json[..Math.Min(json.Length, textStart + 20)];
                if (StreamingQuestionPreview.Render(prefix, c).Length == 0) throw new Exception("Incomplete fact prose is not streamed");
                count++;
            }
        }
        Console.WriteLine($"Reasoning stories: {count} curriculum/template/fresh-fact cases passed.");
        string directory = Path.Combine(Path.GetTempPath(), "reasoning-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new QuestionBankStore(Path.Combine(directory, "stories.db3"));
            foreach (var family in new[] { BankQuestionFamily.TwoNumbers, BankQuestionFamily.Average, BankQuestionFamily.Percentage, BankQuestionFamily.MultiStep })
            {
                int variant = ReasoningStoryCatalogue.Variants(family, CurriculumTier.ThreeStars)[0];
                var c = ReasoningStoryCatalogue.Create(family, variant, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, new(19));
                var d = ReasoningStoryCatalogue.Draft(c);
                var saved = new ValidatedBankQuestion(c, d, QuestionBankStore.SerializeDraft(d), "test", DateTime.UtcNow);
                if (!await store.InsertAsync(saved) || await store.InsertAsync(saved)) throw new Exception("Storage/deduplication failed");
                if (await store.TakeReasoningAsync(family, variant, c.Tier, c.Language) is not { } selected
                    || selected.Contract != c) throw new Exception("Stored family or variant was lost");
                using var excel = new MemoryStream();
                QuestionBankWorkbook.Write(excel, [saved]);
                excel.Position = 0;
                var imported = QuestionBankWorkbook.Read(excel).Single();
                if (imported.ErrorCode is not null || imported.Question?.Contract != c
                    || QuestionBankStore.SerializeDraft(imported.Question.Draft) != QuestionBankStore.SerializeDraft(d))
                    throw new Exception("Excel lost structured story data: " + imported.ErrorCode);
                using var malformed = new MemoryStream();
                malformed.Write(excel.ToArray());
                using (var archive = new System.IO.Compression.ZipArchive(malformed, System.IO.Compression.ZipArchiveMode.Update, true))
                {
                    var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
                    System.Xml.Linq.XElement sheet;
                    using (var content = entry.Open()) sheet = System.Xml.Linq.XElement.Load(content);
                    sheet.Descendants().Single(e => e.Name.LocalName == "c" && (string?)e.Attribute("r") == "AF2")
                        .Descendants().Single(e => e.Name.LocalName == "t").Value = "[null]";
                    entry.Delete();
                    using var updated = archive.CreateEntry("xl/worksheets/sheet1.xml").Open();
                    sheet.Save(updated);
                }
                malformed.Position = 0;
                if (QuestionBankWorkbook.Read(malformed).Single().ErrorCode != "InvalidExcelRow")
                    throw new Exception("Malformed narrative Excel row was not rejected safely");
                var worker = new AiQuestionGenerationService(new StoryRuntime(), store);
                worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.English, 1, true, Family: family, StoryVariant: variant));
                await worker.Completion;
                if (worker.Snapshot.State != AiJobState.Completed || worker.Snapshot.Items.Single().State != AiItemState.Saved)
                    throw new Exception("New-family background worker failed: " + worker.Snapshot.Error);
                var original = ReasoningStoryCatalogue.ToPractice(c, d, ArithmeticQuizMode.Essay);
                var provider = new BasicPracticeQuestionProvider(store, new BankRandom());
                var bankPractice = await provider.SelectReasoningAsync(original, c.Tier, c.Language);
                if (bankPractice == original || bankPractice.ElementaryProblem?.Type != original.ElementaryProblem?.Type
                    || bankPractice.AverageProblem?.Type != original.AverageProblem?.Type
                    || bankPractice.PercentageProblem?.Type != original.PercentageProblem?.Type)
                    throw new Exception("Practice did not refresh facts from the matching stored template");
                if (await store.TakeReasoningAsync(family, variant, c.Tier, AppLanguage.English) is not null)
                    throw new Exception("Language filter leaked another family/template");
            }
            using var allExcel = new MemoryStream();
            var exported = await store.ExportExcelAsync(allExcel);
            if (exported.Exported != 8) throw new Exception("Store export lost version 8 rows");
            await store.DeleteAllAsync();
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("Reasoning SQL/Excel/background generation/practice integration passed.");
    }

    private sealed class BankRandom() : Random(82)
    { public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue); }
    private sealed class StoryRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "story-test";
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
            => Task.FromResult(QuestionBankStore.SerializeDraft(ReasoningStoryCatalogue.Draft(c)));
    }
}
