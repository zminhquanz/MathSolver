using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal sealed class ReasoningStoryCycle
{
    private readonly Dictionary<(BankQuestionFamily, int, CurriculumTier, AppLanguage), Queue<string>> _recent = [];

    public BasicQuestionContract Next(BankQuestionFamily family, int variant, CurriculumTier tier, AppLanguage language)
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
        history.Enqueue(best!.SceneId + "/" + best.Unit);
        if (history.Count > 4) history.Dequeue();
        return best;
    }
}
