using System.Globalization;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static partial class ExpandedArithmeticTests
{
    private static void Verify(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        TestAssert.That(c.IsValid && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Expanded contract/draft rejected.");
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = c.ToPracticeQuestion(draft.ToWordProblem(c), mode, new(21));
            var details = q.WordProblem!.PracticeDetails!;
            TestAssert.That(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid, "Expanded question mathematics rejected.");
            var a = q.Expression.LeftOperand; var b = q.Expression.RightOperand;
            var expected = c.Operation switch { ArithmeticOperation.Add => a + b, ArithmeticOperation.Subtract => a - b,
                ArithmeticOperation.Multiply => a * b, _ => a / b };
            TestAssert.That(q.CorrectAnswer == expected && expected >= 0 && expected <= details.Capacity, "Expanded exact answer/capacity changed.");
            foreach (var p in details.Purchases)
                TestAssert.That(p.UnitPrice >= p.MinimumPrice && p.UnitPrice <= p.MaximumPrice && p.Total == p.UnitPrice * p.Count,
                    "Unit price confused with a purchase total.");
            TestAssert.That(!q.WordProblem.ProblemText.Contains('{'), "Unresolved word problem slots.");
            TestAssert.That(details.ContextId != "classroom" || details.Capacity == 40, "Classroom capacity expanded.");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var answer = ElementaryWordProblemSolutionFormatter.Format(q, c.Language,
                    CultureInfo.GetCultureInfo(QuizContentCatalog.Culture(c.Language)));
                TestAssert.That(grader.ValidateSubmission(q, answer).IsCorrect, "Replayed expanded solution failed grading: " + answer);
                TestAssert.That(!grader.ValidateSubmission(q, "-1 kg").IsCorrect, "Incorrect answer/unit accepted.");
            }
        }
    }

    public static async Task RunAsync()
    {
        int profiles = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
        foreach (var group in new[] { QuestionKnowledgeGroup.Objects, QuestionKnowledgeGroup.Money,
            QuestionKnowledgeGroup.Packaging, QuestionKnowledgeGroup.Production, QuestionKnowledgeGroup.Data })
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var id in ExpandedArithmeticCatalogue.Contexts(group, tier, language))
        {
            var c = ExpandedArithmeticCatalogue.Create(new(group), operation, tier, language, new(17), id);
            var fresh = c.FreshFacts(new(83));
            var drafts = ExpandedArithmeticCatalogue.Drafts(c);
            TestAssert.That(drafts.Count >= 2, "Missing reviewed alternative: " + c.Practice!.TemplateId);
            foreach (var draft in drafts)
            {
                Verify(c, draft); Verify(fresh, draft);
                TestAssert.That(QuestionProseIdentity.Hash(c, draft) == QuestionProseIdentity.Hash(fresh, draft), "Fresh numbers changed prose identity.");
            }
            string raw = QuestionBankStore.SerializeDraft(drafts[0]);
            foreach (var bad in new[] { raw.Replace("{unit}", "kg", StringComparison.Ordinal), raw.Replace("{actor}", "unexpected actor", StringComparison.Ordinal),
                raw.Replace("{a}", "999999999", StringComparison.Ordinal).Replace("{price}", "999999999", StringComparison.Ordinal),
                raw.Replace("\"step_0\"", "\"step_99\"", StringComparison.Ordinal) })
                if (bad != raw) TestAssert.That(!BasicQuestionValidator.Validate(bad, c).IsValid, "Changed unit/role/value/step was accepted.");
            TestAssert.That(!(c with { Unit = "kg" }).IsValid && !(c with { Left = c.Left + 1 }).IsValid
                && !(c with { Practice = c.Practice! with { StepCount = 99 } }).IsValid, "Forged expanded contract was accepted.");
            var excluded = new HashSet<string> { QuestionProseIdentity.Hash(c, drafts[0]) };
            TestAssert.That(ExpandedArithmeticCatalogue.NovelDrafts(c, excluded).All(d => QuestionProseIdentity.Hash(c, d) != excluded.Single()),
                "Duplicate wording remains in native choices.");
            string preview = StreamingQuestionPreview.Render(raw, fresh);
            TestAssert.That(preview == drafts[0].ToWordProblem(fresh).ProblemText, "Streaming preview disagrees with final replay.");
            profiles++;
        }
        using var workspace = TestWorkspace.Temporary("expanded-arithmetic");
        var store = new QuestionBankStore(workspace.FilePath("bank.db3"));
        var money = ExpandedArithmeticCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Add,
            CurriculumTier.FiveStars, AppLanguage.Vietnamese, new(65), "school-laptop");
        var d = ExpandedArithmeticCatalogue.Draft(money);
        var saved = new ValidatedBankQuestion(money, d, QuestionBankStore.SerializeDraft(d), "offline", DateTime.UtcNow);
        TestAssert.That(await store.InsertAsync(saved), "Expanded SQLite insert failed.");
        TestAssert.That(!await store.InsertAsync(saved with { Contract = money.FreshFacts(new(46)) }), "Same prose with fresh prices was inserted twice.");
        var read = await store.TakeForProfileAsync(money.Operation, money.Tier, money.Language, new(money.KnowledgeGroup));
        TestAssert.That(read?.Contract.Version == ExpandedArithmeticCatalogue.Version, "Expanded SQLite row was not selected.");
        Verify(read!.Contract.FreshFacts(new(94)), read.Draft);
        using var backup = new MemoryStream();
        TestAssert.That(QuestionBankWorkbook.WriteBackup(backup, [saved]) == 1, "Expanded backup write failed.");
        backup.Position = 0;
        var restored = QuestionBankWorkbook.Read(backup).Single();
        TestAssert.That(restored.Question?.Contract.Practice == money.Practice && restored.ErrorCode is null,
            "Excel backup lost expanded context/price seed: " + restored.ErrorCode);
        Verify(restored.Question!.Contract.FreshFacts(new(27)), restored.Question.Draft);
        var workerStore = new QuestionBankStore(workspace.FilePath("worker.db3"));
        var worker = new AiQuestionGenerationService(new ReviewedRuntime(), workerStore);
        worker.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese, 2, true,
            new(QuestionKnowledgeGroup.Money)));
        await worker.Completion;
        TestAssert.That(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved
            && i.Contract.Version == ExpandedArithmeticCatalogue.Version), "AI worker did not route/validate/save expanded arithmetic: " + worker.Snapshot.Error);
        TestAssert.That(await workerStore.TakeExpandedAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars,
            AppLanguage.Vietnamese, new(QuestionKnowledgeGroup.Money)) is not null, "Bounded-money reader did not find the new bank rows.");
        Console.WriteLine($"PASS {profiles} expanded arithmetic profiles: reviewed alternatives, bounded prices/capacities, fresh facts, all grading modes, invalid roles/units, duplicate identity, streaming and SQLite.");
    }

    private sealed class ReviewedRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "reviewed-worker-fixture";
        public Task<string> GenerateAsync(BasicQuestionContract c, string prompt, CancellationToken cancellation,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null) =>
            GenerateNovelAsync(c, prompt, new HashSet<string>(), cancellation, onText, onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c, string prompt, IReadOnlySet<string> excluded,
            CancellationToken cancellation, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            string raw = QuestionBankStore.SerializeDraft(ExpandedArithmeticCatalogue.NovelDrafts(c, excluded).First());
            onText?.Invoke(raw);
            return Task.FromResult(raw);
        }
    }
}
