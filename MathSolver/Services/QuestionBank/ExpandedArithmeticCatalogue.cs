using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public sealed record ExpandedArithmeticSeed(int Seed, string ContextId, string TemplateId,
    int StepCount, PracticeArithmeticStyle? Style);

/// <summary>Version 10 binds reviewed prose to the same price/context-bounded C# facts used by practice and PDF.</summary>
public static class ExpandedArithmeticCatalogue
{
    public const int Version = 10;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static bool Supports(QuestionKnowledgeGroup group, CurriculumTier tier) =>
        tier is CurriculumTier.FourStars or CurriculumTier.FiveStars && PracticeArithmeticStoryGenerator.Supports(group);

    public static string[] Contexts(QuestionKnowledgeGroup group, CurriculumTier tier, AppLanguage language) => !Supports(group, tier) ? []
        : group == QuestionKnowledgeGroup.Money
            ? QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", QuizContentCatalog.Culture(language))
                .Where(i => i.MinimumStars <= (int)tier).Select(i => i.Id).ToArray()
            : QuizContentCatalog.LoadList<PracticeCountContext>("PracticeArithmeticContexts", QuizContentCatalog.Culture(language))
                .Where(c => c.Group == group).Select(c => c.Id).ToArray();

    public static BasicQuestionContract Create(QuestionLearningProfile profile, ArithmeticOperation operation,
        CurriculumTier tier, AppLanguage language, Random? random = null, string? contextId = null,
        PracticeArithmeticStyle? style = null)
    {
        if (!profile.IsValid || !Supports(profile.Group, tier) || !Enum.IsDefined(operation) || !Enum.IsDefined(language))
            throw new ArgumentException("InvalidExpandedArithmeticProfile");
        random ??= Random.Shared;
        var ids = Contexts(profile.Group, tier, language);
        contextId ??= ids[random.Next(ids.Length)];
        if (!ids.Contains(contextId)) throw new ArgumentException("InvalidExpandedArithmeticContext");
        var c = new BasicQuestionContract(Version, operation, tier, language, 1, 1, "", "", "",
            TopicId: profile.Group.ToString(), SceneId: contextId, KnowledgeGroup: profile.Group)
        { Practice = new(random.Next(), contextId, "", 0, style) };
        var q = Replay(c, ArithmeticQuizMode.Essay);
        var facts = q.WordProblem!.PracticeDetails!;
        return c with { Left = (int)q.Expression.LeftOperand, Right = (int)q.Expression.RightOperand,
            Subject = q.WordProblem.SubjectName, Unit = q.WordProblem.AnswerUnit,
            GroupUnit = facts.Slots!.GetValueOrDefault("group", ""),
            Practice = c.Practice! with { TemplateId = facts.TemplateId, StepCount = facts.Steps.Count, Style = q.ArithmeticStyle } };
    }

    internal static ArithmeticQuizQuestion Replay(BasicQuestionContract c, ArithmeticQuizMode mode) =>
        new PracticeArithmeticStoryGenerator(new Random(c.Practice!.Seed)).Generate(c.Operation, c.Tier, c.Language,
            c.KnowledgeGroup, mode, c.Practice.ContextId, c.Practice.Style);

    public static bool IsValid(BasicQuestionContract c)
    {
        if (c.Version != Version || c.Practice is not { Seed: >= 0, StepCount: >= 1 and <= 3 } seed
            || c.Story is not null || !Enum.IsDefined(c.Operation) || !Enum.IsDefined(c.Language)
            || !Supports(c.KnowledgeGroup, c.Tier) || c.UnknownRole != FindXUnknownRole.None
            || c.Grade != 0 || c.LeftDenominator != 1 || c.RightDenominator != 1
            || c.OtherSubject != "" || c.PartA != "" || c.PartB != "" || c.Structure != BasicQuestionStructure.Increase
            || seed.Style is { } style && !Enum.IsDefined(style)
            || c.TopicId != c.KnowledgeGroup.ToString() || c.SceneId != seed.ContextId
            || !Contexts(c.KnowledgeGroup, c.Tier, c.Language).Contains(seed.ContextId)) return false;
        try
        {
            var q = Replay(c, ArithmeticQuizMode.Essay); var d = q.WordProblem!.PracticeDetails!;
            return c.Left == q.Expression.LeftOperand && c.Right == q.Expression.RightOperand
                && c.Subject == q.WordProblem.SubjectName && c.Unit == q.WordProblem.AnswerUnit
                && c.GroupUnit == d.Slots!.GetValueOrDefault("group", "") && seed.TemplateId == d.TemplateId
                && seed.StepCount == d.Steps.Count && seed.Style == q.ArithmeticStyle;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException) { return false; }
    }

    public static BasicQuestionContract Fresh(BasicQuestionContract c, Random? random = null)
    {
        if (!IsValid(c)) throw new ArgumentException("InvalidContract");
        random ??= Random.Shared;
        for (int attempt = 0; attempt < 128; attempt++)
        {
            var fresh = Create(new(c.KnowledgeGroup), c.Operation, c.Tier, c.Language, random, c.SceneId, c.Practice!.Style);
            if (fresh.Practice!.TemplateId == c.Practice.TemplateId && fresh.Practice.StepCount == c.Practice.StepCount
                && (fresh.Left != c.Left || fresh.Right != c.Right || !Replay(fresh, ArithmeticQuizMode.Essay).WordProblem!.PracticeDetails!.Slots!
                    .OrderBy(v => v.Key).SequenceEqual(Replay(c, ArithmeticQuizMode.Essay).WordProblem!.PracticeDetails!.Slots!.OrderBy(v => v.Key)))) return fresh;
        }
        throw new InvalidOperationException("ExpandedArithmeticFactsUnavailable");
    }

    private static PracticeArithmeticTemplate Template(BasicQuestionContract c) => QuizContentCatalog.LoadList<PracticeArithmeticTemplate>(
        "PracticeArithmeticTemplates", QuizContentCatalog.Culture(c.Language)).Single(t => t.Id == c.Practice!.TemplateId);

    public static IReadOnlyList<BasicQuestionDraft> Drafts(BasicQuestionContract c)
    {
        var template = Template(c); var q = Replay(c, ArithmeticQuizMode.Essay);
        var leads = q.WordProblem!.PracticeDetails!.Steps.Select((s, i) => new NarrativeClause("step_" + i,
            s.Lead.Replace(c.Unit, "{unit}", StringComparison.Ordinal))).ToArray();
        return new[] { template.Problem }.Concat(template.AlternativeProblems ?? []).Distinct().Select(problem =>
        {
            int split = problem.LastIndexOf(". ", StringComparison.Ordinal) + 2;
            if (split < 2 || !problem.EndsWith('?')) throw new InvalidDataException("Expanded template needs a separate question.");
            return new BasicQuestionDraft("", "", problem[split..], leads[^1].Text, "expanded-practice")
            { Facts = [new("situation", problem[..split].Trim())], SolutionLeads = leads };
        }).ToArray();
    }

    public static BasicQuestionDraft Draft(BasicQuestionContract c) => Drafts(c)[0];
    internal static object DiagnosticData(BasicQuestionContract c) => new {
        Contract = c, Facts = Replay(c, ArithmeticQuizMode.Essay).WordProblem!.PracticeDetails
    };
    public static IReadOnlyList<BasicQuestionDraft> NovelDrafts(BasicQuestionContract c, IReadOnlySet<string> excluded) =>
        Drafts(c).Where(d => !excluded.Contains(QuestionProseIdentity.Hash(c, d))).ToArray();

    public static string Render(string text, BasicQuestionContract c)
    {
        var slots = Replay(c, ArithmeticQuizMode.Essay).WordProblem!.PracticeDetails!.Slots!;
        return Regex.Replace(text, @"\{([a-z_]+)\}", m => slots.TryGetValue(m.Groups[1].Value, out var value)
            ? value : throw new InvalidDataException("Unknown expanded arithmetic variable: " + m.Value), RegexOptions.CultureInvariant, RegexTimeout);
    }

    public static MathWordProblem ToWordProblem(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        var word = Replay(c, ArithmeticQuizMode.Essay).WordProblem!;
        var leads = draft.SolutionLeads!.ToDictionary(s => s.Role, s => Render(s.Text, c));
        var details = word.PracticeDetails! with { Steps = word.PracticeDetails!.Steps.Select((s, i) =>
            s with { Lead = leads["step_" + i] }).ToArray() };
        return word with { ProblemText = Render(draft.ProblemText, c), SolutionLead = details.Steps[^1].Lead, PracticeDetails = details };
    }

    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        if (!IsValid(c)) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) return new(null, "InvalidJson");
        try
        {
            using var doc = JsonDocument.Parse(raw, new() { MaxDepth = 8 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(
                new[] { "facts", "question", "solution_leads" }.Order())) return new(null, "InvalidFields");
            static NarrativeClause[] Clauses(JsonElement value, string key)
            {
                if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > 3) throw new JsonException();
                return value.EnumerateArray().Select(item =>
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { key, "text" }.Order()))
                        throw new JsonException();
                    return new NarrativeClause(item.GetProperty(key).GetString()!, item.GetProperty("text").GetString()!);
                }).ToArray();
            }
            var facts = Clauses(root.GetProperty("facts"), "role"); var leads = Clauses(root.GetProperty("solution_leads"), "step");
            string? question = root.GetProperty("question").GetString();
            var approved = Drafts(c).FirstOrDefault(d => d.Question == question && d.Facts!.SequenceEqual(facts) && d.SolutionLeads!.SequenceEqual(leads));
            return approved is null ? new(null, "ChangedRelationOrTarget", ErrorDetails: "Use an approved situation, target and solution-step sequence; preserve every placeholder and unit.") : new(approved, null);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return new(null, "InvalidJson"); }
    }

    public static string ProseHash(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        var slots = Replay(c, ArithmeticQuizMode.Essay).WordProblem!.PracticeDetails!.Slots!;
        string prose = Regex.Replace(draft.ProblemText, @"\{([a-z_]+)\}", m => m.Groups[1].Value switch
        {
            "actor" => " actor_a ",
            // An item is a fixed context noun, like the existing unit vocabulary, not a randomized person's name.
            // Canonicalize singular/plural to the same item so a count change cannot bypass deduplication.
            "item" or "items" => slots.GetValueOrDefault("item", ""),
            "unit" or "group" => slots.GetValueOrDefault(m.Groups[1].Value, ""),
            _ => " quantity "
        }, RegexOptions.CultureInvariant, RegexTimeout);
        prose = string.Join(' ', Regex.Matches(prose.Normalize(NormalizationForm.FormC).ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
            .Select(m => m.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c.Language + "\n" + prose)));
    }

    public static string Prompt(BasicQuestionContract c, IReadOnlySet<string>? excluded = null, string? correction = null)
    {
        var candidates = NovelDrafts(c, excluded ?? new HashSet<string>()).Take(4).ToArray();
        if (candidates.Length == 0) throw new InvalidOperationException("DuplicateProseRetriesExhausted");
        string instruction = c.Language == AppLanguage.Vietnamese
            ? "Chọn một cách diễn đạt đã duyệt dưới đây và trả đúng JSON đó. Đây là mẫu lời văn, không phải bài cần giải. C# sinh số theo mức sao và quy mô bối cảnh, giữ đơn giá từng món riêng với tổng chi phí. Giữ nguyên mọi biến, đơn vị, số bước, thứ tự sự việc và mục tiêu hỏi. Không điền số, không sửa giá, không thêm nhân vật hay dữ kiện."
            : "Choose one reviewed wording below and return that JSON. This is a prose template, not a problem to solve. C# samples star/context-bounded facts and keeps unit prices distinct from purchase totals. Preserve every placeholder, unit, step, chronology and target. Do not fill numbers, change prices, or add actors or facts.";
        return instruction + "\nContext: " + c.SceneId + "; operation=" + c.Operation + "; stars=" + (int)c.Tier
            + "\n" + string.Join("\n", candidates.Select(QuestionBankStore.SerializeDraft))
            + (correction is null ? "" : "\nRejected: " + correction + ". Choose a complete valid JSON.");
    }

    public static string Grammar(BasicQuestionContract c, IReadOnlySet<string>? excluded = null)
    {
        var candidates = NovelDrafts(c, excluded ?? new HashSet<string>()).Take(8).ToArray();
        if (candidates.Length == 0) throw new InvalidOperationException("DuplicateProseRetriesExhausted");
        // Exact approved branches enforce mathematical roles while the model chooses the wording.
        // Escape the outer GBNF literal to ASCII (the same convention as the existing narrative grammar).
        // Raw accented characters in a native grammar terminal can be marshalled with the wrong encoding.
        return "root ::= " + string.Join(" | ", candidates.Select(d => JsonSerializer.Serialize(QuestionBankStore.SerializeDraft(d)))) + "\n";
    }
}
