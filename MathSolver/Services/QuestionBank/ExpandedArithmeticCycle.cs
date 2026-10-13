using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Rotate pinned settings and skip exhausted wording before loading or querying the model.</summary>
internal sealed class ExpandedArithmeticCycle
{
    private readonly Dictionary<(QuestionKnowledgeGroup, ArithmeticOperation, CurriculumTier, AppLanguage, string), int> _uses = [];
    public BasicQuestionContract Next(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, IReadOnlySet<string> excluded)
    {
        var specialized = profile.Group == QuestionKnowledgeGroup.Packaging && operation == ArithmeticOperation.Divide
            ? AppliedQuestionCatalogue.Available(profile, operation, tier).Where(s => s.Id is "packing-minimum" or "packing-remainder")
                .Select(s => s.Id).ToArray() : [];
        foreach (string id in ExpandedArithmeticCatalogue.Contexts(profile.Group, tier, language).Concat(specialized).OrderBy(_ => Random.Shared.Next())
            .OrderBy(id => _uses.GetValueOrDefault((profile.Group, operation, tier, language, id))))
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                bool legacy = specialized.Contains(id);
                var c = legacy ? AppliedQuestionCatalogue.Create(profile, operation, tier, language, sceneId: id)
                    : ExpandedArithmeticCatalogue.Create(profile, operation, tier, language, contextId: id);
                if (legacy ? ReviewedQuestionProse.For(c)?.NovelExample(c, excluded) is null
                    : ExpandedArithmeticCatalogue.NovelDrafts(c, excluded).Count == 0) continue;
                var key = (profile.Group, operation, tier, language, id);
                _uses[key] = _uses.GetValueOrDefault(key) + 1;
                return c;
            }
        }
        throw new InvalidOperationException("DuplicateProseRetriesExhausted");
    }
}
