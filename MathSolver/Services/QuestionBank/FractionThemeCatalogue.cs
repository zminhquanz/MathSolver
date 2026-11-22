using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// Measurable quantities used in fraction word problems. Subjects, units and artwork
/// are shared by deterministic questions, reviewed AI prose and worksheet output.
/// </summary>
internal sealed record FractionThemeSubject(
    string Id, QuestionKnowledgeGroup Group, string ArtworkId,
    string VietnameseItem, string EnglishItem, string VietnameseUnit, string EnglishUnit)
{
    private const string Prefix = "fraction-material-";

    internal string SceneId(string suffix) => $"{Prefix}{Id}-{suffix}";

    internal string Unit(string culture, string field, ArithmeticOperation operation)
    {
        if (field == "ResultUnit" && operation == ArithmeticOperation.Divide)
            return culture == "vi-VN" ? "suất định mức" : "standard portions";
        if (field == "UnitB" && operation == ArithmeticOperation.Multiply)
            return "";
        return culture == "vi-VN" ? VietnameseUnit : EnglishUnit;
    }
}

internal static class FractionThemeCatalogue
{
    // Original grade-4/5 contexts involving paper, ribbon, food, water, fabric and
    // measured supplies. Countable pets, medicines and toys are never split into fractions.
    internal static IReadOnlyList<FractionThemeSubject> Subjects { get; } = QuizContentCatalog.LoadList<FractionThemeSubject>("FractionThemeSubjects");
    private static readonly string[] SceneSuffixes = ["total", "remaining", "part", "portions"];

    internal static FractionThemeSubject? FindScene(string sceneId)
        => Subjects.FirstOrDefault(subject =>
            SceneSuffixes.Any(suffix => subject.SceneId(suffix) == sceneId));

    internal static IEnumerable<FractionQuestionCatalogue.Scene> Scenes()
    {
        foreach (var subject in Subjects)
        {
            yield return new(subject.SceneId("total"), subject.Group, ArithmeticOperation.Add);
            yield return new(subject.SceneId("remaining"), subject.Group, ArithmeticOperation.Subtract);
            yield return new(subject.SceneId("part"), subject.Group, ArithmeticOperation.Multiply);
            yield return new(subject.SceneId("portions"), subject.Group, ArithmeticOperation.Divide);
        }
    }
}
