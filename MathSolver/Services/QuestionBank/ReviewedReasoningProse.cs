namespace MathSolver.Services.QuestionBank;

/// <summary>Compose reviewed fact clauses without altering their mathematical roles.
/// Only facts and the question participate in novelty; solution leads never do.</summary>
internal static class ReviewedReasoningProse
{
    internal static IEnumerable<BasicQuestionDraft> NovelDrafts(BasicQuestionContract c, IReadOnlySet<string> excluded)
    {
        var lesson = ReasoningStoryCatalogue.Lesson(c);
        var choices = lesson.Facts.Select(f => lesson.FactPhrasings[f.Role]).Append(lesson.QuestionPhrasings).ToArray();
        var indices = new int[choices.Length];
        var original = ReasoningStoryCatalogue.Draft(c);
        var seen = new HashSet<string>(excluded, StringComparer.Ordinal);
        while (true)
        {
            var draft = original with {
                Facts = lesson.Facts.Select((f, i) => f with { Text = choices[i][indices[i]] }).ToArray(),
                Question = choices[^1][indices[^1]]
            };
            if ((!ReasoningStoryCatalogue.UsesReviewedPhrasings(c.Family) || HasNewFacts(c, draft))
                && seen.Add(QuestionProseIdentity.Hash(c, draft))) yield return draft;
            int index = 0;
            // Vary the factual clauses before merely changing the question.
            while (index < indices.Length && ++indices[index] == choices[index].Length) indices[index++] = 0;
            if (index == indices.Length) yield break;
        }
    }

    internal static bool HasNewFacts(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        var lesson = ReasoningStoryCatalogue.Lesson(c);
        string Signature(IEnumerable<NarrativeClause> facts)
        {
            string text = CanonicalizeOpening(c, string.Join(" ", facts.Select(f => f.Text)));
            return string.Join(" ", System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
                .Select(m => m.Value));
        }
        return Signature(draft.Facts!) != Signature(lesson.Facts);
    }

    // Keep the actual context, but treat its equivalent introductory phrases
    // as the same wording in SQLite as well as within a generation batch.
    internal static string CanonicalizeOpening(BasicQuestionContract c, string text)
    {
        if (c.Family != BankQuestionFamily.MultiStep) return text;
        var lesson = ReasoningStoryCatalogue.Lesson(c);
        string introId = "ElementaryQuizGenerator.MultiStep.CreateMultiStep." + (lesson.Context switch {
            "library" => "002", "craft" => "003", "community" => "004", "distribution" => "005",
            _ => throw new InvalidOperationException("UnknownMultiStepContext")
        });
        string intro = QuizContentCatalog.Entry(QuizContentCatalog.Culture(c.Language), introId).Text;
        var openings = new[] { intro }.Concat(ReviewedNarrativePhrasings.For(c.Language)[intro])
            .Select(text => text.Trim()).OrderByDescending(text => text.Length).ToArray();
        string? opening = openings.FirstOrDefault(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return opening is null ? text : intro.Trim() + text[opening.Length..];
    }
}
