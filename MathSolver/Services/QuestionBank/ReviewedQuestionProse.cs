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
        if (c.Version == FindXQuestionCatalogue.Version) c = FindXQuestionCatalogue.AsApplied(c);
        return c.Version is AppliedQuestionCatalogue.Version or FractionQuestionCatalogue.Version
            ? QuizStoryTemplates.For(c).Prose : null;
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
