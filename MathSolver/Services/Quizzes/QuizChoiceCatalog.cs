using System.Globalization;
using System.Text;

namespace MathSolver.Services;

public sealed record QuizChoiceDefinition(string Key, string IllustrationId, string DescriptionKey);
public sealed record QuizChoiceOption(int Index, string Key, string Title, string Description,
    string IllustrationId, bool IsSelected);

/// <summary>Stable presentation IDs are independent of translated labels and filtered list positions.</summary>
public static class QuizChoiceCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, QuizChoiceDefinition>> Catalogue = new(() =>
        QuizContentCatalog.LoadList<QuizChoiceDefinition>("QuizChoices")
            .ToDictionary(row => row.Key, StringComparer.Ordinal));

    public static IReadOnlyDictionary<string, QuizChoiceDefinition> Definitions => Catalogue.Value;

    public static QuizChoiceOption Create(int index, string key, string title, AppLanguage language, bool selected)
    {
        if (!Definitions.TryGetValue(key, out var definition))
            throw new InvalidDataException("Missing illustrated quiz choice: " + key);
        return new(index, key, title, QuizContentCatalog.Text(language, definition.DescriptionKey),
            definition.IllustrationId, selected);
    }

    public static IReadOnlyList<QuizChoiceOption> Filter(IEnumerable<QuizChoiceOption> choices, string query)
    {
        string[] terms = SearchText(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return choices.Where(choice => terms.All(term =>
            SearchText(choice.Title + " " + choice.Description).Contains(term, StringComparison.Ordinal)))
            .ToArray();
    }

    private static string SearchText(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(c is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(c));
        return result.ToString();
    }
}
