using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckIllustratedChoices()
    {
        // Validate the keys used by the AI subtype picker, not only entries already
        // present in the catalog: missing entries otherwise escape catalog checks.
        using var pickerSource = typeof(PuzzleTests).Assembly.GetManifestResourceStream("QuizLocalization.AiQuestionBankPage.Learning.cs")
            ?? throw new Exception("Missing AI picker source for catalog validation.");
        using var reader = new StreamReader(pickerSource);
        var aiSubtypeKeys = System.Text.RegularExpressions.Regex.Matches(reader.ReadToEnd(), "\"(Quiz\\.[A-Za-z0-9_.]+)\"")
            .Select(match => match.Groups[1].Value)
            .Where(key => !key.EndsWith('.')).Distinct(StringComparer.Ordinal).ToArray();
        Require(aiSubtypeKeys.Contains("Quiz.ProportionDirect") && aiSubtypeKeys.Contains("Quiz.ProportionInverse"),
            "AI proportion subtype routes must be validated.");
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (string key in aiSubtypeKeys)
        {
            var choice = QuizChoiceCatalog.Create(0, key, "label", language, false);
            Require(!string.IsNullOrWhiteSpace(choice.Description) && choice.Description != key,
                "Missing AI subtype description: " + key);
        }
        var catalog = new QuizProblemTypeCatalog();
        foreach (var option in catalog.Options)
            if (!QuizChoiceCatalog.Definitions.ContainsKey(option.LocalizationKey))
                throw new Exception("Missing topic illustration: " + option.LocalizationKey);
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        foreach (var type in ElementaryQuizGenerator.Types(kind))
            if (!QuizChoiceCatalog.Definitions.ContainsKey("Quiz.Elementary." + type))
                throw new Exception("Missing skill illustration: " + type);

        // Choices in the same selector need distinct skill thumbnails. Shape aliases in
        // different selectors may intentionally share the same mathematical shape.
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        {
            var types = ElementaryQuizGenerator.Types(kind);
            var ids = types.Select(type => QuizChoiceCatalog.Definitions["Quiz.Elementary." + type].IllustrationId).ToArray();
            Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
                "Repeated skill illustration in " + kind);
        }
        foreach (string prefix in new[] { "Quiz.Problem", "Quiz.Expression", "Quiz.Average", "Quiz.Percentage", "Quiz.Motion", "Quiz.Proportion", "FindXBank.Role." })
        {
            var ids = QuizChoiceCatalog.Definitions.Values.Where(row => row.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(row => row.IllustrationId).ToArray();
            Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
                "Repeated illustration in selector " + prefix);
        }
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            foreach (var definition in QuizChoiceCatalog.Definitions.Values)
            {
                var choice = QuizChoiceCatalog.Create(7, definition.Key, "label", language, true);
                if (string.IsNullOrWhiteSpace(choice.Description) || string.IsNullOrWhiteSpace(choice.IllustrationId))
                    throw new Exception("Invalid choice content: " + definition.Key);
            }
            foreach (string key in new[] { "Choice.SelectProblem", "Choice.SelectSubtype", "Choice.SelectShape", "Choice.Search",
                         "Choice.Empty", "Choice.Close", "Choice.Selected" })
                if (string.IsNullOrWhiteSpace(QuizContentCatalog.Text(language, key))) throw new Exception("Missing chooser text: " + key);
        }

        QuizChoiceOption[] examples =
        [
            new(12, "measurement", "Đo lường", "Đổi đơn vị độ dài", "measurement", false),
            new(38, "fraction", "Phân số", "Các phần bằng nhau", "fraction", true),
            new(4, "clock", "Thời gian", "Đọc đồng hồ", "clock", false)
        ];
        var fractions = QuizChoiceCatalog.Filter(examples, "phan so");
        if (fractions.Count != 1 || fractions[0].Index != 38 || !fractions[0].IsSelected)
            throw new Exception("Filtering changed the chosen question route.");
        if (QuizChoiceCatalog.Filter(examples, "doi dai").Single().Index != 12)
            throw new Exception("Vietnamese search must match descriptions without diacritics.");
        if (QuizChoiceCatalog.Filter(examples, "not available").Count != 0 || QuizChoiceCatalog.Filter(examples, " ").Count != 3)
            throw new Exception("Empty search must not drop selections or invent matches.");
        Console.WriteLine($"  {QuizChoiceCatalog.Definitions.Count} illustrated choices validated in both languages; filtered routes stay stable.");
    }
}
