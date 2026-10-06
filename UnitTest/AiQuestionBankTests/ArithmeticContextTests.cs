using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;

internal static class ArithmeticContextTests
{
    private static readonly ArithmeticOperation[] Operations = [ArithmeticOperation.Subtract, ArithmeticOperation.Multiply, ArithmeticOperation.Divide];
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static BasicDraftValidation Validate(BasicQuestionDraft draft, BasicQuestionContract c)
        => BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c);

    public static async Task RunAsync(string directory)
    {
        int count = 0;
        var bankEntries = new List<ValidatedBankQuestion>();
        foreach (var operation in Operations)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var (scene, structure) in ArithmeticQuestionCatalogue.Available(operation, tier))
        foreach (string id in ArithmeticQuestionCatalogue.Scale(scene.Id, operation, tier)!.UnitIds)
        {
            var c = ArithmeticQuestionCatalogue.Create(operation, tier, language, new Random(2026 + count), scene.Id, structure, id);
            var draft = BasicQuestionTemplates.Example(c);
            var validation = Validate(draft, c);
            string tag = $"{operation}/{tier}/{language}/{scene.Id}/{structure}/{id}";
            Check(c.IsValid && validation.IsValid && validation.Contract == c, $"Valid context rejected {tag}: {validation.ErrorCode}");
            Check(draft.GivenA.EndsWith(',') && !char.IsUpper(draft.GivenB[0]), "Broken joined clauses: " + tag);
            string prompt = BasicQuestionPrompt.Build(c), grammar = GgufQuestionRuntime.BuildGrammar(c);
            Check(prompt.Length < 2300 && !prompt.Contains("Unit catalogue:")
                && !System.Text.RegularExpressions.Regex.IsMatch(prompt, @"(?<!\p{L})" + System.Text.RegularExpressions.Regex.Escape(c.Subject) + @"(?!\p{L})"),
                "Prompt expanded the catalogue or concrete facts: " + tag);
            Check(grammar.All(ch => ch < 128) && !grammar.Contains("\"books\" | \"notebooks\""), "Grammar lost exact C# units/Unicode safety: " + tag);
            foreach (int seed in new[] { 1, 36, 812 })
            {
                var fresh = c.FreshFacts(new Random(seed));
                Check(fresh.IsValid && fresh.SceneId == c.SceneId && fresh.Structure == structure
                    && fresh.Unit == c.Unit && fresh.Left >= QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier)
                    && fresh.Left <= QuizCurriculumLayer.GetMaximumOperandValue(tier), "Fresh data escaped the scale/context: " + tag);
                Check(operation != ArithmeticOperation.Divide || fresh.Left % fresh.Right == 0, "Non-exact division: " + tag);
                Check(operation != ArithmeticOperation.Subtract || fresh.Left >= fresh.Right, "Negative stock: " + tag);
                var word = draft.ToWordProblem(fresh);
                Check(!word.ProblemText.Contains('{') && !word.SolutionLead.Contains('{') && word.AnswerUnit == fresh.AnswerUnit, "Unrendered/group target: " + tag);
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(fresh.ToPracticeQuestion(word, mode)).IsValid,
                        "Practice answer mode invalid: " + tag);
                var essay = fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                string equation = $"{fresh.Left} {BasicArithmeticEngine.GetSymbol(operation)} {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                var graded = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(essay, word.SolutionLead, equation,
                    $"{fresh.Answer} {word.AnswerUnit}");
                Check(graded.IsCorrect, "Valid grouped answer was graded incorrectly: " + tag + " / " + equation + " / " + word.SolutionLead + " / " + JsonSerializer.Serialize(graded));
            }
            Check(!Validate(draft with { GivenB = draft.GivenB.Replace("{b}", "{a}") }, c).IsValid, "Swapped numeric role accepted: " + tag);
            Check(!Validate(draft with { Question = structure == BasicQuestionStructure.CountGroups ? draft.Question.Replace("{group}", "{unit}")
                : draft.Question.Replace("{unit}", "{group}").Replace("{group_one}", "{unit}") }, c).IsValid,
                "Swapped target dimension accepted: " + tag);
            Check(!Validate(draft with { GivenA = draft.GivenA + " łącznie" }, c).IsValid, "Foreign prose accepted: " + tag);
            Check(!Validate(draft with { GivenB = draft.GivenB + (language == AppLanguage.Vietnamese ? " không có." : " doesn't have.") }, c).IsValid,
                "Negated relation accepted: " + tag);
            Check(!Validate(draft with { GivenA = draft.GivenA + (language == AppLanguage.Vietnamese ? " quả táo" : " apples") }, c).IsValid,
                "Extra literal object accepted: " + tag);
            Check(!(c with { TopicId = "wrong" }).IsValid && !(c with { Left = QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier) - 1 }).IsValid,
                "Forged context or smaller primary accepted: " + tag);
            if (operation != ArithmeticOperation.Subtract && structure is not (BasicQuestionStructure.CountGroups or BasicQuestionStructure.CompareFactor))
                Check(!(c with { Right = 1 }).IsValid && !(c with { Right = 100 }).IsValid, "Trivial/unbounded factor accepted: " + tag);
            if (language == AppLanguage.Vietnamese && id == ArithmeticQuestionCatalogue.Scale(scene.Id, operation, tier)!.UnitIds[0])
                bankEntries.Add(new(c, draft, QuestionBankStore.SerializeDraft(draft), "context-test", DateTime.UtcNow));
            count++;
        }
        Check(ArithmeticQuestionCatalogue.Available(ArithmeticOperation.Subtract, CurriculumTier.FiveStars).All(p => p.Scene.Id != "family-gifts"),
            "Individual small context used for five digits.");
        var compare = ArithmeticQuestionCatalogue.Create(ArithmeticOperation.Divide, CurriculumTier.ThreeStars,
            AppLanguage.Vietnamese, new Random(836), "school-supplies", BasicQuestionStructure.TimesFewer, "books");
        var compareDraft = BasicQuestionTemplates.Example(compare);
        Check(!Validate(compareDraft with {
            GivenB = "{other} có gấp {unit} lần số lượng của chính nó, hỏi số lượng của đối gấp {b} lần {unit} của {name} là bao nhiêu." }, compare).IsValid,
            "Real GGUF placeholder explanation passed as a comparison fact.");
        foreach (string scene in new[] { "food-supplies", "fish-farm", "construction-stock" })
        {
            var c = ArithmeticQuestionCatalogue.Create(ArithmeticOperation.Subtract, CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(37), scene);
            Check(!c.Subject.StartsWith("giỏ ") && !c.Subject.StartsWith("bể ") && !c.Subject.StartsWith("góc "), "Inanimate keeper used as an active subject.");
        }
        foreach (var operation in Operations)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var cycle = new ArithmeticQuestionCycle(new Random(114));
            int relationCount = BasicQuestionTemplates.AllowedContextual(operation, tier).Length;
            var picks = Enumerable.Range(0, ArithmeticQuestionCatalogue.Available(operation, tier).Count())
                .Select(_ => cycle.Next(operation, tier, AppLanguage.Vietnamese)).ToArray();
            Check(picks.All(c => c.IsValid) && picks.Take(relationCount).Select(c => c.Structure).Distinct().Count() == relationCount,
                "Scene catalogue dominated relation rotation.");
            Check(picks.Select(c => c.SceneId).Distinct().Count() == ArithmeticQuestionCatalogue.Available(operation, tier).Select(p => p.Scene.Id).Distinct().Count(),
                "A setting was starved in generation.");
        }
        await StorageAsync(directory, bankEntries);
        Console.WriteLine($"PASS {count} bilingual subtraction/multiplication/division context/scale/unit/role cases, fresh operands, grouping targets, grading and invalid-prose rejection");
    }

    private static async Task StorageAsync(string directory, List<ValidatedBankQuestion> entries)
    {
        string path = Path.Combine(directory, "arithmetic-contexts.db3");
        var store = new QuestionBankStore(path);
        var uniqueEntries = new List<ValidatedBankQuestion>();
        var identities = new HashSet<string>();
        foreach (var entry in entries)
        {
            bool unique = identities.Add(QuestionProseIdentity.Hash(entry.Contract, entry.Draft));
            Check(await store.InsertAsync(entry) == unique, "Context wording deduplication failed.");
            if (unique) { uniqueEntries.Add(entry); continue; }
            // Retain coverage of pre-existing banks containing the same wording
            // at multiple star levels; new insertion correctly rejects it.
            var c = entry.Contract;
            using var db = new SQLite.SQLiteConnection(path);
            db.Insert(new QuestionBankStore.Row {
                Hash = "historical-context-" + db.ExecuteScalar<int>("SELECT COUNT(*) FROM BasicQuestionBank"),
                Operation = (int)c.Operation, Stars = (int)c.Tier, Language = (int)c.Language, Version = c.Version,
                ContractJson = JsonSerializer.Serialize(c), DraftJson = QuestionBankStore.SerializeDraft(entry.Draft),
                RawJson = entry.RawJson, ModelName = entry.ModelName, CreatedUtc = entry.CreatedUtc,
                Structure = (int)c.Structure, TopicId = c.TopicId, SceneId = c.SceneId, Grade = c.Grade,
                KnowledgeGroup = (int)c.KnowledgeGroup, ProblemType = (int)c.Family, ProblemVariant = (int)c.UnknownRole });
        }
        Check(!await store.InsertAsync(entries[0] with { Contract = entries[0].Contract.FreshFacts(new Random(735)) }), "Preview numbers defeated template dedup.");
        foreach (var operation in Operations)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var choices = entries.Where(q => q.Contract.Operation == operation && q.Contract.Tier == tier).ToArray();
            var picked = new List<BasicQuestionContract>();
            for (int i = 0; i < choices.Length; i++)
            {
                var item = await store.TakeAsync(operation, tier, AppLanguage.Vietnamese);
                Check(item is not null && item.Contract.IsValid, "Missing contextual SQLite practice question.");
                picked.Add(item!.Contract);
            }
            Check(picked.Select(c => c.SceneId).Distinct().Count() == choices.Select(q => q.Contract.SceneId).Distinct().Count(),
                "SQLite context rotation starved a scene.");
        }
        using var workbook = new MemoryStream();
        var exported = await store.ExportExcelAsync(workbook);
        Check(exported.Exported == entries.Count && exported.Skipped == 0, "Excel skipped valid v4 entries.");
        workbook.Position = 0;
        var imported = new QuestionBankStore(Path.Combine(directory, "arithmetic-import.db3"));
        var report = await imported.ImportExcelAsync(workbook);
        Check(report.Inserted == uniqueEntries.Count && report.Duplicates == entries.Count - uniqueEntries.Count
            && report.Rejected == 0, "Excel lost arithmetic metadata/group units or retained duplicate wording.");
        await imported.QueryAsync("UPDATE BasicQuestionBank SET TopicId='wrong' WHERE SceneId='library'");
        using var tamperedExport = new MemoryStream();
        var forged = await imported.ExportExcelAsync(tamperedExport);
        Check(forged.Skipped == uniqueEntries.Count(q => q.Contract.SceneId == "library"), "Indexed metadata tampering reached export.");
        foreach (var operation in Operations)
        {
            var legacy = BasicQuestionContract.CreateTemplate(operation, CurriculumTier.OneStar, AppLanguage.English, new Random(17));
            Check(await store.InsertAsync(new(legacy, BasicQuestionTemplates.Example(legacy), "legacy", "legacy", DateTime.UtcNow)), "Old template failed insertion.");
            Check((await store.TakeAsync(operation, CurriculumTier.OneStar, AppLanguage.English))?.Contract.Version == 2, "Old template lost selection compatibility.");
        }
        Console.WriteLine("PASS v4 SQLite selection/rotation/dedup, Excel interchange, forged metadata and v2 compatibility");
    }

    public static async Task RunModelAsync(string path)
    {
        var runtime = new GgufQuestionRuntime();
        var failures = new List<string>();
        await runtime.LoadAsync(path);
        try
        {
            Console.WriteLine($"MODEL {runtime.ModelName}; threads {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
            var scenes = new[] { "food-supplies", "poultry", "school-supplies", "construction-stock", "library" };
            foreach (var operation in Operations)
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            {
                var language = tier == CurriculumTier.FourStars ? AppLanguage.English : AppLanguage.Vietnamese;
                var roles = BasicQuestionTemplates.Allowed(operation, tier);
                var structure = roles[((int)tier - 1) % roles.Length];
                if (operation == ArithmeticOperation.Multiply && tier == CurriculumTier.FiveStars) structure = BasicQuestionStructure.TimesAsMany;
                var c = ArithmeticQuestionCatalogue.Create(operation, tier, language, new Random(836), scenes[(int)tier - 1], structure);
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string tag = $"{operation}/{tier}/{language}/{c.SceneId}/{structure}";
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    Console.WriteLine($"GENERATING {tag} attempt {attempt}, prompt {prompt.Length} chars");
                    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, text => streamed.Append(text), value => metrics = value);
                    var valid = BasicQuestionValidator.Validate(raw, c);
                    Console.WriteLine(raw);
                    Console.WriteLine($"RESULT {valid.ErrorCode ?? "PASS"}, {metrics?.TokensPerSecond:F2} token/s, context {runtime.LastContextTokens}");
                    Check(streamed.ToString() == raw && metrics?.TokensPerSecond > 0, "Broken stream/token metrics.");
                    correction = valid.ErrorCode;
                    if (!valid.IsValid) continue;
                    for (int seed = 0; seed < 4; seed++)
                    {
                        var fresh = c.FreshFacts(new Random(seed));
                        var word = valid.Draft!.ToWordProblem(fresh);
                        var essay = fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                        string equation = $"{fresh.Left} {BasicArithmeticEngine.GetSymbol(operation)} {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                        Check(fresh.IsValid && !word.ProblemText.Contains('{') && new EssayAnswerValidator(new BasicArithmeticEngine())
                            .Validate(essay, word.SolutionLead, equation, $"{fresh.Answer} {word.AnswerUnit}").IsCorrect, "Generated context failed fresh practice grading.");
                    }
                    Console.WriteLine("RENDERED " + valid.Draft!.ToWordProblem(c).ProblemText);
                    passed = true; break;
                }
                if (!passed) failures.Add($"{operation}/{tier}/{structure}");
            }
        }
        finally { await runtime.EjectAsync(); }
        Check(failures.Count == 0, "Failed three model attempts: " + string.Join(", ", failures));
        Console.WriteLine("PASS real GGUF subtraction/multiplication/division at all five tiers; no app database writes.");
    }
}
