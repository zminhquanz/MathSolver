namespace MathSolver.Services.QuestionBank;

/// <summary>Authored loaf identities: never infer a bread type from AI wording or a numeric unit.</summary>
public static class BreadQuestionCatalogue
{
    public sealed record BreadType(QuestionUnit Unit, string FractionStem, string ArtworkId);
    public static IReadOnlyList<BreadType> Types { get; } = Array.AsReadOnly(new BreadType[]
    {
        new(new("sandwich-bread", "ổ bánh mì sandwich", "sandwich bread loaf", "sandwich bread loaves", "giỏ", "basket", "baskets"), "sandwich", "bread_sandwich"),
        new(new("round-bread", "ổ bánh mì tròn giòn", "crusty round bread roll", "crusty round bread rolls", "giỏ", "basket", "baskets"), "round", "bread"),
        new(new("banh-mi", "ổ bánh mì thịt Việt Nam", "Vietnamese meat-filled banh mi sandwich", "Vietnamese meat-filled banh mi sandwiches", "giỏ", "basket", "baskets"), "banh-mi", "bread_banh_mi")
    });

    // Old generic units/scenes stay available to validate and refresh already saved questions.
    internal static string[] BakeryUnitIds { get; } = ["cakes", "bread-rolls", .. Types.Select(t => t.Unit.Id)];
    internal static string[] GenerationUnits(IEnumerable<string> ids) => ids.Where(id => id != "bread-rolls").ToArray();
    internal static bool IsLegacyFraction(string id) => id is "fraction-bread-Add" or "fraction-bread-Subtract" or "fraction-bread-Multiply" or "fraction-bread-Divide";
}
