using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Small metadata history; no generated payloads retained.</summary>
public sealed class AppliedQuestionCycle(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly List<string> _recent = [];
    public BasicQuestionContract Next(QuestionLearningProfile profile, ArithmeticOperation operation,
        CurriculumTier tier, AppLanguage language)
    {
        var scenes = AppliedQuestionCatalogue.Available(profile, operation, tier).ToArray();
        if (scenes.Length == 0) throw new ArgumentException("InvalidLearningProfile");
        string key = $"{profile.Group}/{operation}/{tier}/{language}/";
        var selected = scenes.OrderBy(_ => _random.Next())
            .OrderBy(s => _recent.Count(x => x == key + s.Id))
            .ThenBy(s => _recent.Count > 0 && _recent[^1] == key + s.Id).First();
        _recent.Add(key + selected.Id);
        if (_recent.Count > 64) _recent.RemoveAt(0);
        return AppliedQuestionCatalogue.Create(profile, operation, tier, language, _random, selected.Id);
    }
}
