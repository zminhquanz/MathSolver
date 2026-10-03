using MathSolver.Services;

namespace MathSolver.Models;

public sealed record ElementaryGivenValue(string Role, string Value, string Unit = "");
public sealed record ElementaryInferenceStep(string Label, string Expression, ReducedFraction Value,
    string Unit, string DisplayValue);

/// <summary>Supplied quantities and calculated steps stay separate for both generation sources.</summary>
public sealed record ElementaryQuizReasoning(CurriculumTier Tier, string ScenarioId,
    IReadOnlyList<ElementaryGivenValue> Givens, IReadOnlyList<ElementaryInferenceStep> Steps,
    IReadOnlyList<string> IntermediateUnits, QuizDiagram? SupportingDiagram = null, string? Explanation = null);
