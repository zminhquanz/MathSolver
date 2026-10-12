using MathSolver.Services;

namespace MathSolver.Models;

/// <summary>Stable context and difficulty; the JSON context owns the area units and bounds.</summary>
public sealed record AreaMeasurementProfile(string ContextId, CurriculumTier Tier, AppLanguage Language);

/// <summary>Reviewed clauses use named roles rather than numbers. Not connected to model generation yet.</summary>
public sealed record AreaMeasurementDraft(string Initial, string? Added, string? Removed,
    string Question, IReadOnlyList<string> SolutionLeads);

public sealed record AreaMeasurementContextSnapshot(string QuantityCategory, string Subject,
    string LargeUnitId, string SmallUnitId, int MaximumQuantity);

public sealed record AreaMeasurementQuestionContract(AreaMeasurementProfile Profile, int DataSeed,
    int Version = 1, AreaMeasurementDraft? Draft = null, AreaMeasurementContextSnapshot? Context = null);
