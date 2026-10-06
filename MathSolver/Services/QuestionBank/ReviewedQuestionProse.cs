using MathSolver.Models;
using MathSolver.Services;
using System.Text;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

/// <summary>Whole-clause alternatives preserve roles and dimensions. Novelty excludes
/// complete stories, rather than forcing every individual clause to be different.</summary>
internal sealed record ReviewedQuestionProse(string[] GivenA, string[] GivenB, string[] Questions,
    string[] Leads, string UnitId)
{
    public static ReviewedQuestionProse? For(BasicQuestionContract c)
    {
        if (c.Version == FractionQuestionCatalogue.Version) return FractionQuestionCatalogue.Prose(c);
        if (c.Version == FindXQuestionCatalogue.Version) c = FindXQuestionCatalogue.AsApplied(c);
        if (c.Version != AppliedQuestionCatalogue.Version) return null;
        var d = AppliedQuestionCatalogue.Draft(c); var e = AppliedQuestionCatalogue.Draft(c, 1);
        bool vi = c.Language == AppLanguage.Vietnamese;
        static string[] Unique(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).ToArray();
        string[] Given(string first, string second)
        {
            // These replacements bind the complete actor/verb phrase. They never
            // rewrite comparison direction, conversions, price or grouping roles.
            (string From, string To)[] phrases = vi
                ? [("{name} có ", "{name} đang có "), ("{other} có ", "{other} đang có "),
                    ("còn lại {", "còn {"), ("thu hoạch được", "thu được sau khi thu hoạch"),
                    (" chứa ", " đựng "), (" chở ", " vận chuyển "), ("chia đều", "phân chia đều"),
                    ("xếp đều", "sắp xếp đều"), ("{name} mua vở hết {a} {unit}", "Số tiền {name} chi để mua vở là {a} {unit}"),
                    ("{name} mua bút hết {b} {unit}", "tiền mua bút của {name} là {b} {unit}"),
                    ("dùng {b} {unit} mua đồ dùng học tập", "chi {b} {unit} cho đồ dùng học tập")]
                : [("{name} has ", "{name} currently has "), ("{other} has ", "{other} currently has "),
                    ("previously spent", "had already spent"), (" contains ", " holds "), (" carries ", " transports "),
                    (" buys ", " purchases "), ("divided equally", "split equally"),
                    ("spends {a} {unit} on", "pays {a} {unit} for"), ("spends {b} {unit} on", "pays {b} {unit} for")];
            return Unique(new[] { first, second }.SelectMany(text => new[] { text }
                .Concat(phrases.Select(phrase => text.Replace(phrase.From, phrase.To))))).Take(8).ToArray();
        }
        var result = new ReviewedQuestionProse(Given(d.GivenA, e.GivenA), Given(d.GivenB, e.GivenB),
            Unique([d.Question, e.Question]), Unique([d.SolutionLead!, e.SolutionLead!]), d.UnitId!);
        if (c.SceneId != "saving-total") return result;
        // The old savings grammar only varied 'tất cả/tổng cộng', so the model
        // could never produce another factual sentence even with a retry prompt.
        return result with {
            GivenA = vi ? [d.GivenA, "Số tiền {name} đã tiết kiệm được là {a} {unit},",
                "{name} có khoản tiết kiệm {a} {unit},", "Khoản tiền tiết kiệm của {name} hiện là {a} {unit},"]
                : [d.GivenA, "The amount {name} has saved is {a} {unit},",
                    "{name}'s savings amount to {a} {unit},", "{name} has {a} {unit} in savings,"],
            GivenB = vi ? [d.GivenB, "sau đó {name} để dành thêm {b} {unit}.",
                "{name} bổ sung {b} {unit} vào khoản tiết kiệm.", "khoản tiết kiệm của {name} tăng thêm {b} {unit}."]
                : [d.GivenB, "then {name} puts aside another {b} {unit}.",
                    "{name} adds {b} {unit} to these savings.", "these savings increase by {b} {unit}."],
            Questions = vi ? [d.Question, e.Question, "Khoản tiết kiệm của {name} sau đó là bao nhiêu {unit}?",
                "Sau khi tiết kiệm thêm, {name} có bao nhiêu {unit} trong khoản tiết kiệm?"]
                : [d.Question, e.Question, "What is the total amount of {name}'s savings in {unit}?",
                    "How many {unit} are in {name}'s savings after this addition?"]
        };
    }

    public IEnumerable<BasicQuestionDraft> Stories()
    {
        foreach (string a in GivenA)
        foreach (string b in GivenB)
        foreach (string q in Questions)
            yield return new(a, b, q, Leads[0], UnitId);
    }

    public BasicQuestionDraft? NovelExample(BasicQuestionContract c, IReadOnlySet<string> excluded)
    {
        var stories = Stories().Select(d => (Draft: d, Used: excluded.Contains(QuestionProseIdentity.Hash(c, d)))).ToArray();
        var used = stories.Where(s => s.Used).Select(s => s.Draft).ToArray();
        // Prefer less-used factual clauses instead of cycling through questions
        // over the same two givens. Sampling still enforces whole-story exclusions.
        return stories.Where(s => !s.Used).OrderBy(s => used.Count(d => d.GivenA == s.Draft.GivenA)
                + used.Count(d => d.GivenB == s.Draft.GivenB))
            .ThenBy(s => used.Count(d => d.Question == s.Draft.Question)).Select(s => s.Draft).FirstOrDefault();
    }

    public string Grammar(BasicQuestionContract c, IReadOnlySet<string> excluded)
    {
        var stories = Stories().Where(d => !excluded.Contains(QuestionProseIdentity.Hash(c, d))).ToArray();
        if (stories.Length == 0) throw new ProseAlternativesExhaustedException();
        static string L(string value) => JsonSerializer.Serialize(value);
        static string Field(string value) => "\"\\\"\" " + L(value) + " \"\\\"\"";
        // A branch is a complete (given_a, given_b, question) tuple. Removing a
        // used question alone would wrongly discard unused combinations of givens.
        var grammar = new StringBuilder("root ::= \"{\" ws \"\\\"given_a\\\"\" ws \":\" ws (");
        grammar.Append(string.Join(" | ", Enumerable.Range(0, stories.Length).Select(i => "story" + i))).Append(") tail\n");
        for (int i = 0; i < stories.Length; i++)
        {
            var d = stories[i];
            grammar.Append("story").Append(i).Append(" ::= ").Append(Field(d.GivenA))
                .Append(" ws \",\" ws \"\\\"given_b\\\"\" ws \":\" ws ").Append(Field(d.GivenB))
                .Append(" ws \",\" ws \"\\\"question\\\"\" ws \":\" ws ").Append(Field(d.Question)).Append('\n');
        }
        grammar.Append("tail ::= ws \",\" ws \"\\\"solution_lead\\\"\" ws \":\" ws lead ws \",\" ws \"\\\"unit_id\\\"\" ws \":\" ws ")
            .Append(Field(UnitId)).Append(" ws \"}\" ws\nlead ::= ")
            .Append(string.Join(" | ", Leads.Select(Field))).Append("\nws ::= [ \\t\\n\\r]*\n");
        return grammar.ToString();
    }
}

internal sealed class ProseAlternativesExhaustedException : InvalidOperationException
{
    public ProseAlternativesExhaustedException() : base("ProseAlternativesExhausted") { }
}
