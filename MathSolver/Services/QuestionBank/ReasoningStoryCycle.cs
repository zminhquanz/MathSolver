using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal sealed class ReasoningStoryCycle
{
    private readonly Dictionary<(BankQuestionFamily, int, CurriculumTier, AppLanguage), Queue<string>> _recent = [];

    public BasicQuestionContract Next(BankQuestionFamily family, int variant, CurriculumTier tier, AppLanguage language,
        IReadOnlySet<string>? excludedProse = null)
    {
        var key = (family, variant, tier, language);
        if (!_recent.TryGetValue(key, out var history)) _recent[key] = history = new();
        BasicQuestionContract? best = null;
        int bestCount = int.MaxValue;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, language);
            string context = c.SceneId + "/" + c.Unit;
            int repetitions = history.Count(s => s == context);
            if (repetitions < bestCount) { best = c; bestCount = repetitions; }
            if (repetitions == 0) break;
        }
        if (family is BankQuestionFamily.Proportion or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time && excludedProse is not null
            && !ReviewedReasoningProse.NovelDrafts(best!, excludedProse).Any())
        {
            // One exhausted context must not stop a batch while other compatible
            // contexts still have wording. Search the finite catalog before failing.
            var narrativeIds = family switch
            {
                BankQuestionFamily.Decimal => ElementaryQuizGenerator.DecimalContexts(language).Select(c => c.Id),
                BankQuestionFamily.Measurement => ElementaryQuizGenerator.MeasurementContexts(language, (ElementaryQuizType)variant).Select(c => c.Id),
                BankQuestionFamily.Remainder => ElementaryQuizGenerator.PackingStories(language).Select(c => c.Id),
                BankQuestionFamily.Time => ElementaryQuizGenerator.TimeStoryContextIds(tier),
                _ => ProportionQuizGenerator.NarrativeIds((ProportionQuizType)variant, tier, language)
            };
            best = narrativeIds
                .Select(id => ReasoningStoryCatalogue.Create(family, variant, tier, language, narrativeId: id))
                .Where(c => ReviewedReasoningProse.NovelDrafts(c, excludedProse).Any())
                .OrderBy(c => history.Count(s => s == c.SceneId + "/" + c.Unit)).FirstOrDefault()
                ?? throw new InvalidOperationException("DuplicateProseRetriesExhausted");
        }
        history.Enqueue(best!.SceneId + "/" + best.Unit);
        if (history.Count > 4) history.Dequeue();
        return best;
    }
}
