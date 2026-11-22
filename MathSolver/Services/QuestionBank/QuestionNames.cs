namespace MathSolver.Services.QuestionBank;

/// <summary>Given-name pools, separate from the role assigned to a fictional actor.</summary>
public static class QuestionNames
{
    // First 100 entries in each VNTH01 frequency list, not exclusive gender rules.
    // Nguyen Duc Anh, hoten.org, CC BY 4.0. See README.md in this directory.
    public static IReadOnlyList<string> VietnameseMale { get; } = QuizContentCatalog.LoadList<string>("QuestionNamesVietnameseMale");
    public static IReadOnlyList<string> VietnameseFemale { get; } = QuizContentCatalog.LoadList<string>("QuestionNamesVietnameseFemale");
    private static readonly IReadOnlyList<string> EnglishMale = QuizContentCatalog.LoadList<string>("QuestionNamesEnglishMale");
    private static readonly IReadOnlyList<string> EnglishFemale = QuizContentCatalog.LoadList<string>("QuestionNamesEnglishFemale");

    private static readonly IReadOnlyList<string> VietnameseFemaleRoles = QuizContentCatalog.LoadList<string>("QuestionNamesVietnameseFemaleRoles");
    private static readonly IReadOnlyList<string> VietnameseMaleRoles = QuizContentCatalog.LoadList<string>("QuestionNamesVietnameseMaleRoles");
    private static readonly IReadOnlyList<string> EnglishFemaleRoles = QuizContentCatalog.LoadList<string>("QuestionNamesEnglishFemaleRoles");
    private static readonly IReadOnlyList<string> EnglishMaleRoles = QuizContentCatalog.LoadList<string>("QuestionNamesEnglishMaleRoles");

    internal static string GivenName(string actor) => actor.Replace("'s shop", "").Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];

    public static string Create(AppLanguage language, Random random, bool largeContext = false)
    {
        bool female = random.Next(2) == 0, vi = language == AppLanguage.Vietnamese;
        var names = vi ? (female ? VietnameseFemale : VietnameseMale) : (female ? EnglishFemale : EnglishMale);
        string name = names[random.Next(names.Count)];
        var roles = vi
            ? female ? VietnameseFemaleRoles : VietnameseMaleRoles
            : female ? EnglishFemaleRoles : EnglishMaleRoles;
        string actor = roles[random.Next(roles.Count)] + name;
        // Large stock quantities belong to a business, not a child's possessions.
        return largeContext ? vi ? "cửa hàng của " + actor : actor + "'s shop" : actor;
    }
}
