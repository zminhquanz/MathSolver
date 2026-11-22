namespace MathSolver.Services.QuestionBank;

/// <summary>Authored loaf identities: never infer a bread type from AI wording or a numeric unit.</summary>
public static class BreadQuestionCatalogue
{
    public sealed record BreadType(QuestionUnit Unit, string FractionStem, string ArtworkId);
    public static IReadOnlyList<BreadType> Types { get; } = QuizContentCatalog.LoadList<BreadType>("BreadQuestionTypes");

    // Old generic units/scenes stay available to validate and refresh already saved questions.
    internal static string[] BakeryUnitIds { get; } = [.. QuizContentCatalog.LoadList<string>("BreadBakeryLegacyUnitIds"), .. Types.Select(t => t.Unit.Id)];
    internal static string[] GenerationUnits(IEnumerable<string> ids) => ids.Where(id => id != "bread-rolls").ToArray();
    internal static bool IsLegacyFraction(string id) => id is "fraction-bread-Add" or "fraction-bread-Subtract" or "fraction-bread-Multiply" or "fraction-bread-Divide";
}
