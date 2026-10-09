using MathSolver.Services;

namespace MathSolver.Models;

public enum DataChartQuestionKind { CategoryValue, Total, AbsoluteDifference, MoreThan }

/// <summary>Stable narrative/visual roles. Numeric data can change without shuffling these roles.</summary>
public sealed record DataChartProfile(string ContextId, ElementaryQuizType Type, CurriculumTier Tier,
    AppLanguage Language, IReadOnlyList<string> CategoryIds, IReadOnlyList<string> TargetCategoryIds,
    IReadOnlyList<string> HiddenCategoryIds, DataChartQuestionKind QuestionKind);

/// <summary>C# owns the data seed as well as the profile; prose and visuals are rebuilt together.</summary>
public sealed record DataChartQuestionContract(DataChartProfile Profile, int DataSeed, int Version = 1);
