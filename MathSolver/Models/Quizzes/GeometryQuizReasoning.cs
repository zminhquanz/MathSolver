using System.Numerics;
using MathSolver.Services;

namespace MathSolver.Models;

/// <summary>A supplied quantity, independent of the solved dimensions.</summary>
public sealed record GeometryGivenFact(string Id, BigInteger Value, string Unit, string Clause);

/// <summary>An intermediate quantity calculated by C#, never supplied to the model as a given.</summary>
public sealed record GeometryInferenceStep(string Key, string Label, string Expression, BigInteger Value, string Unit);

public sealed record GeometryQuizReasoning(CurriculumTier Tier, string ScenarioId,
    AppLanguage Language, IReadOnlyList<GeometryGivenFact> Givens,
    IReadOnlyList<string> Relations, IReadOnlyList<GeometryInferenceStep> Steps, IReadOnlySet<string> HiddenDimensions,
    string MathematicalText, string ProblemText, string CombinedExpression);
