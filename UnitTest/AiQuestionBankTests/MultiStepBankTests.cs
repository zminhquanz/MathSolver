using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class MultiStepBankTests
{

    private static IEnumerable<BasicQuestionContract> Cases()
    {
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (int variant in ReasoningStoryCatalogue.Variants(BankQuestionFamily.MultiStep, tier))
        {
            var contexts = new HashSet<string>();
            for (int seed = 0; seed < 100 && contexts.Count < 4; seed++)
            {
                var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep, variant, tier, language, new(seed));
                if (contexts.Add(c.SceneId)) yield return c;
            }
            Check(contexts.Count == 4, "Missing multi-step context.");
        }
    }

    private static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        Check(c.IsValid && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid,
            "Invalid fresh multi-step contract/prose.");
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c, draft, mode);
            Check(q.ElementaryProblem is { Kind: QuizProblemKind.MultiStep } e && e.Type == (ElementaryQuizType)c.BankVariant,
                "Changed multi-step subtype.");
            var elementary = q.ElementaryProblem!;
            Check(!Regex.IsMatch(elementary.ProblemText, @"\{[fv]\d+\}") && elementary.Answers[0].Value.Numerator == c.Answer
                && elementary.Answers[0].Value.Denominator.IsOne,
                "Unrendered slot or altered answer.");
            Check(elementary.Reasoning!.Steps.All(step => EssayCalculationEvaluator.TryEvaluate(step.Expression,
                out var value, out _) && value.Numerator == step.Value.Numerator && value.Denominator == step.Value.Denominator),
                "C# inference step is incorrect.");
            if (mode == ArithmeticQuizMode.Essay)
            {
                var submitted = EssayCombinedInputParser.Parse(ReasoningStoryCatalogue.Solution(c, draft), true, preserveAllCalculations: true);
                var grade = new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(q,
                    submitted.Solution, submitted.Equation, submitted.Answer);
                Check(grade.IsCorrect, "Fresh AI prose cannot be graded: " + grade);
            }
        }
    }

    public static async Task RunAsync()
    {
        int count = 0, phrasings = 0;
        foreach (var c in Cases())
        {
            var lesson = ReasoningStoryCatalogue.Lesson(c);
            var original = ReasoningStoryCatalogue.Draft(c);
            Check(lesson.Facts.Any(f => lesson.FactPhrasings[f.Role].Length > 1), "No factual alternatives in JSON.");
            CheckPractice(c, original);
            foreach (var fact in lesson.Facts)
            foreach (string text in lesson.FactPhrasings[fact.Role])
            {
                var alternate = original with { Facts = original.Facts!.Select(f => f.Role == fact.Role ? f with { Text = text } : f).ToArray() };
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(alternate), c).IsValid,
                    "Reviewed factual phrasing rejected: " + text);
                if (!ReviewedReasoningProse.HasNewFacts(c, alternate))
                    Check(QuestionProseIdentity.Hash(c, alternate) == QuestionProseIdentity.Hash(c, original),
                        "A context-opening-only change bypassed prose identity.");
                phrasings++;
            }
            foreach (var step in lesson.Steps)
            foreach (string text in lesson.LeadPhrasings[step.Id])
            {
                var alternate = original with { SolutionLeads = original.SolutionLeads!.Select(s => s.Role == step.Id ? s with { Text = text } : s).ToArray() };
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(alternate), c).IsValid,
                    "Reviewed step lead rejected: " + text);
                phrasings++;
            }
            // Exclude EVERY question over the old facts: novelty must alter givens.
            var excluded = lesson.QuestionPhrasings.Select(question => QuestionProseIdentity.Hash(c, original with { Question = question }))
                .ToHashSet(StringComparer.Ordinal);
            var novel = ReviewedReasoningProse.NovelDrafts(c, excluded).First();
            Check(ReviewedReasoningProse.HasNewFacts(c, novel) && excluded.Add(QuestionProseIdentity.Hash(c, novel)),
                "Only the question or solution changed.");
            for (int fresh = 0; fresh < 3; fresh++)
            {
                var refreshed = c.FreshFacts(new Random(910 + fresh));
                Check(refreshed.Story!.Schema == c.Story!.Schema, "Fresh facts changed context/roles.");
                Check(QuestionProseIdentity.Hash(refreshed, novel) == QuestionProseIdentity.Hash(c, novel), "Numbers changed prose identity.");
                CheckPractice(refreshed, novel);
            }
            foreach (var fact in original.Facts!)
            {
                // A loss/removal cannot silently become an addition, even when
                // numeric values happen to coincide.
                string changed = fact.Text.Replace("Lấy ra", "Gộp thêm").Replace("lấy ra", "gộp thêm")
                    .Replace("removed", "added").Replace("Remove", "Add");
                if (changed == fact.Text) continue;
                var bad = original with { Facts = original.Facts.Select(f => f.Role == fact.Role ? f with { Text = changed } : f).ToArray() };
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), c).IsValid, "Changed event polarity accepted.");
            }
            count++;
        }
        // Even the smallest schema has a finite, explicit exhaustion path.
        var smallest = Cases().First();
        var pool = ReviewedReasoningProse.NovelDrafts(smallest, new HashSet<string>()).ToArray();
        var all = pool.Select(d => QuestionProseIdentity.Hash(smallest, d)).ToHashSet(StringComparer.Ordinal);
        Check(all.Count == pool.Length, "Equivalent context openings consumed grammar choices.");
        Check(!ReviewedReasoningProse.NovelDrafts(smallest, all).Any(), "Reviewed palette exhaustion was ignored.");
        try { GgufQuestionRuntime.BuildNovelGrammar(smallest, all); throw new Exception("Exhausted grammar allowed duplicates."); }
        catch (InvalidOperationException e) when (e.Message == "DuplicateProseRetriesExhausted") { }
        // Content structure validation must catch a role swap before inference.
        using var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.vi-VN.json")!;
        var pack = QuizContentCatalog.ReadPack(stream);
        var rows = pack.Lists[ReviewedNarrativePhrasings.ListName].Deserialize<ReviewedNarrativePhrasings[]>()!;
        int position = Array.FindIndex(rows, row => row.Id.EndsWith(".024"));
        rows[position] = rows[position] with { Alternatives = ["có {each} nhóm, mỗi nhóm {count} {unit}."] };
        pack.Lists[ReviewedNarrativePhrasings.ListName] = JsonSerializer.SerializeToElement(rows);
        try { QuizContentCatalog.Validate(pack); throw new Exception("JSON swapped roles accepted."); }
        catch (InvalidDataException) { }
        using var workspace = TestWorkspace.Temporary("MultiStepBank");
        string database = workspace.FilePath("bank.db3");
        try
        {
            var store = new QuestionBankStore(database);
            var original = ReasoningStoryCatalogue.Draft(smallest);
            var lesson = ReasoningStoryCatalogue.Lesson(smallest);
            var openingOnly = lesson.FactPhrasings[lesson.Facts[0].Role]
                .Select(text => original with { Facts = original.Facts!.Select((f, i) => i == 0 ? f with { Text = text } : f).ToArray() })
                .First(d => d.ProblemText != original.ProblemText && !ReviewedReasoningProse.HasNewFacts(smallest, d));
            ValidatedBankQuestion Saved(BasicQuestionDraft draft) => new(smallest, draft,
                QuestionBankStore.SerializeDraft(draft), "reviewed-context", DateTime.UtcNow);
            Check(await store.InsertAsync(Saved(original)), "Could not seed SQLite prose.");
            Check(await store.ContainsProseAsync(Saved(openingOnly)) && !await store.InsertAsync(Saved(openingOnly)),
                "SQLite accepted only a changed introductory phrase.");
            // Rebuild the derived index once after changing the identity policy;
            // retain the public bank rows and their keys.
            using (var db = new SQLite.SQLiteConnection(database))
            {
                db.Execute("UPDATE QuestionProseIndex SET ProseHash='old-policy'");
                db.Execute("UPDATE QuestionProseIndexMetadata SET Value=3 WHERE Key='ValidationRevision'");
            }
            var reopened = new QuestionBankStore(database);
            Check(await reopened.ContainsProseAsync(Saved(openingOnly)), "Old derived prose index was not rebuilt.");
            using (var db = new SQLite.SQLiteConnection(database))
                Check(db.ExecuteScalar<int>("SELECT COUNT(*) FROM BasicQuestionBank") == 1, "Index rebuild changed public bank rows.");
        }
        finally { File.Delete(database); }
        Console.WriteLine($"Multi-step: {count} context/subtype/star/language cases, {phrasings} reviewed clauses, three fresh fact sets in all modes; novelty/exhaustion/unsafe prose passed.");
    }


}
