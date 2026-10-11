using MathSolver.Models;
using MathSolver.Services;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

/// <summary>Reviewed clauses supply semantic anchors, ordered variables and prompt
/// examples. Novel equivalent prose is validated without exact sentence equality.</summary>
internal sealed record ReviewedQuestionProse(string[] GivenA, string[] GivenB, string[] Questions,
    string[] Leads, string UnitId)
{
    public static ReviewedQuestionProse? For(BasicQuestionContract c)
    {
        if (c.Version == FindXQuestionCatalogue.Version) c = FindXQuestionCatalogue.AsApplied(c);
        if (c.Version is AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version)
        {
            var drafts = Enumerable.Range(0, 3).Select(i => OneStepQuestionCatalogue.Draft(c, i)).ToArray();
            return new(EquivalentProseStructures.Expand(drafts.Select(d => d.GivenA), c.Language),
                EquivalentProseStructures.Expand(drafts.Select(d => d.GivenB), c.Language),
                EquivalentProseStructures.Expand(drafts.Select(d => d.Question), c.Language),
                EquivalentProseStructures.Expand(drafts.Select(d => d.SolutionLead!), c.Language), drafts[0].UnitId!);
        }
        if (c.Version is not (AppliedQuestionCatalogue.Version or FractionQuestionCatalogue.Version)) return null;
        var prose = QuizStoryTemplates.For(c).Prose;
        return prose with {
            GivenA = EquivalentProseStructures.Expand(prose.GivenA, c.Language),
            GivenB = EquivalentProseStructures.Expand(prose.GivenB, c.Language),
            Questions = EquivalentProseStructures.Expand(prose.Questions, c.Language),
            Leads = EquivalentProseStructures.Expand(prose.Leads, c.Language)
        };
    }

    internal string MismatchedFields(BasicQuestionDraft draft, AppLanguage language) => SemanticProseRules.MismatchedFields(language,
            ("given_a", draft.GivenA, GivenA), ("given_b", draft.GivenB, GivenB),
            ("question", draft.Question, Questions), ("solution_lead", draft.SolutionLead, Leads));

    internal bool Matches(BasicQuestionDraft draft, AppLanguage language) =>
        draft.UnitId == UnitId && MismatchedFields(draft, language).Length == 0;

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
        // over the same two givens. Validation/storage still enforce prose exclusions.
        return stories.Where(s => !s.Used).OrderBy(s => used.Count(d => d.GivenA == s.Draft.GivenA)
                + used.Count(d => d.GivenB == s.Draft.GivenB))
            .ThenBy(s => used.Count(d => d.Question == s.Draft.Question)).Select(s => s.Draft).FirstOrDefault();
    }

    public string Grammar(BasicQuestionContract c, IReadOnlySet<string> excluded)
    {
        // Exclusion is enforced after validation against the database. A finite
        // approved list must not prevent novel equivalent prose from being sampled.
        static string L(string text) => JsonSerializer.Serialize(text);
        return "root ::= \"{\" ws \"\\\"given_a\\\"\" ws \":\" ws given-a ws \",\" ws \"\\\"given_b\\\"\" ws \":\" ws given-b ws \",\" ws \"\\\"question\\\"\" ws \":\" ws question ws \",\" ws \"\\\"solution_lead\\\"\" ws \":\" ws lead ws \",\" ws \"\\\"unit_id\\\"\" ws \":\" ws "
            + "\"\\\"\" " + L(UnitId) + " \"\\\"\" ws \"}\" ws\n"
            + SemanticProseRules.ClauseRule("given_a", GivenA, c.Language) + SemanticProseRules.ClauseRule("given_b", GivenB, c.Language)
            + SemanticProseRules.ClauseRule("question", Questions, c.Language) + SemanticProseRules.ClauseRule("lead", Leads, c.Language)
            + "\nws ::= [ \\t\\n\\r]*\n";
    }

}
