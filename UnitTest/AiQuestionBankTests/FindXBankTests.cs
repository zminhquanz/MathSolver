using MathSolver.Models;
using MathSolver.Services.Core;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using SQLite;

internal static class FindXBankTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        int checkedQuestions = 0;
        var arithmetic = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        var essay = new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var scenes = FindXQuestionCatalogue.Available(new(group), operation, tier).ToArray();
            Check(scenes.Length > 0, $"Missing group/operation/star: {group}/{operation}/{tier}");
            foreach (var scene in scenes)
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            foreach (int seed in new[] { 2, 27, 305 })
            {
                var c = FindXQuestionCatalogue.Create(new(group), operation, tier, language, new Random(seed), sceneId: scene.Id);
                string tag = $"{scene.Id}/{tier}/{language}/{seed}";
                Check(c.IsValid && c.Family == BankQuestionFamily.FindX && c.UnknownRole == scene.Role, "Invalid facts: " + tag);
                var eq = FindXQuestionCatalogue.Equation(c);
                var engine = new FindXEngine();
                var solved = engine.SolveInteger(eq.KnownValue, eq.ResultValue, eq.Operation, eq.UnknownIsLeftOperand);
                Check(solved.Kind == FindXCoreSolutionKind.Unique && solved.Denominator.IsOne && solved.Numerator == c.Answer,
                    "Wrong inverse/uniqueness: " + tag);
                var evaluated = engine.EvaluateIntegerLeftSide(c.Answer, 1, eq.KnownValue, eq.Operation, eq.UnknownIsLeftOperand);
                Check(evaluated.Denominator.IsOne && evaluated.Numerator == eq.ResultValue, "Back substitution failed: " + tag);
                Check(!BasicQuestionValidator.Validate("{}", c with { UnknownRole = FindXUnknownRole.None }).IsValid, "Forged unknown role passed.");
                Check(!BasicQuestionValidator.Validate("{}", c with { Right = 0 }).IsValid, "Zero divisor/factor passed.");
                var fresh = c.FreshFacts(new Random(seed + 412));
                Check(fresh.IsValid && fresh.SceneId == c.SceneId && fresh.UnknownRole == c.UnknownRole && fresh.KnowledgeGroup == group,
                    "Fresh facts changed scene/role/profile: " + tag);
                string prompt = BasicQuestionPrompt.Build(c), grammar = GgufQuestionRuntime.BuildGrammar(c);
                Check(prompt.Length < 2800 && !prompt.Contains(c.Subject) && !prompt.Contains("Unit catalogue:"), "Bloated/exposed prompt: " + tag);
                Check(grammar.StartsWith("root ::=") && grammar.All(ch => ch <= 127), "Invalid grammar: " + tag);
                foreach (int variant in new[] { 0, 1 })
                {
                    var draft = FindXQuestionCatalogue.Draft(c, variant);
                    string raw = QuestionBankStore.SerializeDraft(draft);
                    var validated = BasicQuestionValidator.Validate(raw, c);
                    Check(validated.IsValid && validated.Contract == c, "Reviewed prose rejected: " + tag + ": " + validated.ErrorCode);
                    var word = draft.ToWordProblem(c);
                    Check(!word.ProblemText.Contains('{') && !word.SolutionLead.Contains('{') && word.AnswerUnit == c.AnswerUnit,
                        "Unrendered/mismatched units: " + tag);
                    Check(StreamingQuestionPreview.Render(raw, c) == word.ProblemText, "Live preview changed dimensions: " + tag);
                    foreach (var broken in new[] {
                        draft with { GivenA = draft.GivenA.Replace("{a}", "{swap}").Replace("{b}", "{a}").Replace("{swap}", "{b}") },
                        draft with { GivenB = draft.GivenB + " łącznie" },
                        draft with { GivenA = draft.GivenA + " 42" },
                        draft with { Question = draft.Question + " {a}" },
                        draft with { SolutionLead = draft.SolutionLead + " = " + c.Answer },
                        draft with { GivenB = draft.GivenA, GivenA = draft.GivenB },
                        draft with { UnitId = "foreign-unit" },
                        draft with { GivenB = draft.GivenB + (language == AppLanguage.Vietnamese ? " Một nhóm nữa tham gia." : " Another group joins.") } })
                        Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(broken), c).IsValid, "Changed roles/answer/units passed: " + tag);
                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    {
                        var q = c.ToPracticeQuestion(word, mode, new Random(seed));
                        Check(q.FindXProblem == eq && arithmetic.Validate(q).IsValid, "Invalid answer mode: " + tag);
                        if (mode != ArithmeticQuizMode.Essay) continue;
                        string calculation = $"{c.Left} {BasicArithmeticEngine.GetSymbol(c.Expression.Operation)} {c.Right} = {c.Answer} {c.AnswerUnit}";
                        var result = essay.Validate(q, word.SolutionLead, calculation, $"{c.Answer} {c.AnswerUnit}");
                        Check(result.IsCorrect, $"Correct story work rejected: {tag}: {result}");
                        Check(!essay.Validate(q, word.SolutionLead, calculation, $"{c.Answer + 1} {c.AnswerUnit}").IsCorrect, "Wrong answer passed.");
                        Check(!essay.Validate(q, word.SolutionLead, calculation, $"{c.Answer} nonsenseunit").IsCorrect, "Wrong answer dimension passed.");
                    }
                    checkedQuestions++;
                }
            }
        }
        Check(FindXQuestionCatalogue.All.Select(s => s.Role).Distinct().Count() == 6, "Missing unknown role.");
        bool unspecifiedRoleRejected = false;
        try { new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese,
            1, true, new(QuestionKnowledgeGroup.Objects), BankQuestionFamily.FindX).Validate(); }
        catch (ArgumentOutOfRangeException) { unspecifiedRoleRejected = true; }
        Check(unspecifiedRoleRejected, "AI generation accepted an unspecified X role.");
        var cycle = new FindXQuestionCycle(new Random(503));
        var roleScenes = FindXQuestionCatalogue.Available(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Subtract,
            CurriculumTier.FiveStars, FindXUnknownRole.Minuend).ToArray();
        var seenScenes = new HashSet<string>();
        foreach (var _ in roleScenes)
            Check(seenScenes.Add(cycle.Next(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Subtract,
                CurriculumTier.FiveStars, AppLanguage.Vietnamese, FindXUnknownRole.Minuend).SceneId), "Explicit-role cycle repeated a setting.");
        string path = Path.Combine(directory, "findx.db3");
        var store = new QuestionBankStore(path);
        var records = new List<ValidatedBankQuestion>();
        var proseIdentities = new HashSet<string>();
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        {
            var c = FindXQuestionCatalogue.Create(new(group), operation, CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(30));
            var d = FindXQuestionCatalogue.Draft(c);
            var saved = new ValidatedBankQuestion(c, d, QuestionBankStore.SerializeDraft(d), "test-runtime", DateTime.UtcNow);
            records.Add(saved);
            bool unique = proseIdentities.Add(QuestionProseIdentity.Hash(c, d));
            Check(await store.InsertAsync(saved) == unique, "Find-X wording deduplication failed.");
            if (!unique) QuestionBankTestFixtures.SeedHistorical(path, saved);
            Check(!await store.InsertAsync(saved with { Contract = c.FreshFacts(new Random(150)) }), "Template dedup included random facts.");
            var selected = await store.TakeFindXAsync(operation, c.Tier, c.Language, new(group));
            Check(selected?.Contract.Family == BankQuestionFamily.FindX && selected.Contract.UnknownRole == c.UnknownRole, "Find-X SQL selection mismatch.");
            Check(await store.TakeForProfileAsync(operation, c.Tier, c.Language, new(group)) is null, "Arithmetic selected a Find-X row.");
        }
        var basic = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, CurriculumTier.OneStar,
            AppLanguage.Vietnamese, new Random(74));
        await store.InsertAsync(new(basic, AppliedQuestionCatalogue.Draft(basic), "", "C#", DateTime.UtcNow));
        Check(await store.TakeFindXAsync(basic.Operation, basic.Tier, basic.Language, new(QuestionKnowledgeGroup.Objects)) is null,
            "Find-X selected an arithmetic row.");
        using var workbook = new MemoryStream();
        var export = await store.ExportExcelAsync(workbook);
        workbook.Position = 0;
        var importStore = new QuestionBankStore(Path.Combine(directory, "findx-import.db3"));
        var imported = await importStore.ImportExcelAsync(workbook);
        Check(imported.Inserted == proseIdentities.Count + 1 && imported.Duplicates == records.Count - proseIdentities.Count
            && imported.Rejected == 0, "Find-X Excel roundtrip lost unknown roles or retained duplicate wording.");
        // A direct SQL edit cannot poison practice: storage payload and metadata must agree.
        await store.QueryAsync("UPDATE BasicQuestionBank SET ProblemVariant=99 WHERE ProblemType=1");
        Check(await store.TakeFindXAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese,
            new(QuestionKnowledgeGroup.Objects)) is null, "Forged SQL metadata passed.");

        var background = new AiQuestionGenerationService(new Runtime(), importStore);
        background.Start(new(ArithmeticOperation.Subtract, CurriculumTier.ThreeStars, AppLanguage.English, 2, true,
            new(QuestionKnowledgeGroup.Packaging), BankQuestionFamily.FindX, FindXUnknownRole.Minuend));
        await background.Completion;
        Check(background.Snapshot.State == AiJobState.Completed && background.Snapshot.Items.Count == 2
            && background.Snapshot.Items.All(i => i.State == AiItemState.Saved
                && i.Contract.UnknownRole == FindXUnknownRole.Minuend && i.Attempts.Count is 2 or 3), "Retry/background insertion failed.");
        var rejectedStore = new QuestionBankStore(Path.Combine(directory, "findx-rejected.db3"));
        var rejectedJob = new AiQuestionGenerationService(new Runtime(rejectAll: true), rejectedStore);
        rejectedJob.Start(new(ArithmeticOperation.Divide, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 2, true,
            new(QuestionKnowledgeGroup.Measurement), BankQuestionFamily.FindX, FindXUnknownRole.Divisor));
        await rejectedJob.Completion;
        Check(rejectedJob.Snapshot.State == AiJobState.Failed && rejectedJob.Snapshot.Items.Count == 1
            && rejectedJob.Snapshot.Items[0].Attempts.Count == 3 && rejectedJob.Snapshot.Items[0].State == AiItemState.Rejected,
            "Invalid Find-X generation did not stop after three attempts.");
        Check(await rejectedStore.TakeFindXAsync(ArithmeticOperation.Divide, CurriculumTier.ThreeStars,
            AppLanguage.Vietnamese, new(QuestionKnowledgeGroup.Measurement)) is null, "Rejected AI prose reached practice.");
        var generator = new FindXQuizGenerator(new FindXEngine(), new Random(410));
        var provider = new BasicPracticeQuestionProvider(importStore, new Random(7), new Random(41));
        int numeric = 0, prose = 0;
        for (int i = 0; i < 80; i++)
        {
            var original = generator.Generate(ArithmeticQuizMode.Essay, ArithmeticOperation.Add, new(CurriculumTier.FiveStars, false));
            var selected = await provider.SelectFindXAsync(original, CurriculumTier.FiveStars, AppLanguage.Vietnamese, new(QuestionKnowledgeGroup.Objects));
            Check(selected.FindXProblem is not null && arithmetic.Validate(selected).IsValid, "Practice selection failed.");
            if (selected.WordProblem is null) numeric++; else prose++;
        }
        Check(numeric > 0 && prose > 0, "Lost numeric/story mixture.");
        Console.WriteLine($"Find-X bank passed: {checkedQuestions} bilingual scene/star/role variants; 9 groups, six roles, three modes; SQLite/Excel/retries/mixed practice.");
    }

    private sealed class Runtime(bool rejectAll = false) : IQuestionTextRuntime
    {
        private int _calls;
        public bool IsLoaded => true;
        public string ModelName => "findx-test";
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            bool duplicate = prompt.Contains("DuplicateProse");
            var d = FindXQuestionCatalogue.Draft(c, duplicate ? 1 : 0);
            if (rejectAll || !duplicate && _calls++ % 2 == 0) d = d with { GivenA = d.GivenA + " łącznie" };
            string raw = QuestionBankStore.SerializeDraft(d);
            onText?.Invoke(raw);
            return Task.FromResult(raw);
        }
    }
}
