using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Localization;
using MathSolver.Services.Core;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckQuizLocalization()
    {
        var vietnamese = ReadQuizPack("QuizLocalization.vi-VN.json");
        var english = ReadQuizPack("QuizLocalization.en-US.json");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string resource in new[]
        {
            "QuizLocalization.MathPuzzlePage.xaml",
            "QuizLocalization.MathPuzzlePage.xaml.cs",
            "QuizLocalization.MathPuzzlePage.Diagrams.cs"
        })
        {
            foreach (Match match in Regex.Matches(ReadQuizResource(resource), @"Quiz\.[A-Za-z0-9_.]+"))
            {
                string key = match.Value;
                // These two prefixes are completed from the selected C# skill.
                if (!key.EndsWith('.') && key != "Quiz.Problem") keys.Add(key);
            }
        }
        keys.UnionWith(new QuizProblemTypeCatalog().Options.Select(option => option.LocalizationKey));
        keys.UnionWith(Enum.GetValues<ElementaryQuizType>().Select(type => "Quiz.Elementary." + type));

        foreach (string key in keys)
        {
            Require(vietnamese.ContainsKey(key) && english.ContainsKey(key),
                $"{key}: the quiz control is missing from a bundled language pack.");
        }

        var vietnameseLetters = new Regex(@"[ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯư\u1EA0-\u1EF9]");
        foreach (string key in vietnamese.Keys.Where(key => key.StartsWith("Quiz.", StringComparison.Ordinal)))
        {
            Require(english.TryGetValue(key, out string? englishText) && !string.IsNullOrWhiteSpace(englishText),
                $"{key}: English quiz text is missing.");
            foreach (var (pack, cultures) in new[]
            {
                (vietnamese, new[] { "vi-VN", "vi" }),
                (english, new[] { "en-US", "en-GB" })
            })
            foreach (string culture in cultures)
            {
                if (QuizLocalizationOverrides.TryGetValue(key, culture, out string fallback))
                    Require(fallback == pack[key], $"{key}/{culture}: the runtime fallback overrides the pack with different text.");
            }
            Require(!vietnameseLetters.IsMatch(englishText!), $"{key}: Vietnamese text appears in the English pack.");
            string[] Tokens(string value) => Regex.Matches(value, @"(?<!\{)\{[^{}]+\}(?!\})")
                .Select(match => match.Value).Distinct().Order().ToArray();
            Require(Tokens(vietnamese[key]).SequenceEqual(Tokens(englishText!)),
                $"{key}: a translation changed its formatting placeholders.");
        }
        Require(QuizLocalizationOverrides.TryGetValue("Quiz.CurriculumHintSkill", "vi-VN", out string viHint) &&
                vietnameseLetters.IsMatch(viHint) && !viHint.Contains("Each skill", StringComparison.Ordinal),
            "The Vietnamese difficulty hint must not show the English sentence.");
        CheckGeneratedQuizLanguage(vietnameseLetters);
        Console.WriteLine($"  Checked {keys.Count} quiz UI keys, {vietnamese.Keys.Count(key => key.StartsWith("Quiz."))} bilingual strings, runtime fallbacks and placeholders.");
    }

    private static void CheckGeneratedQuizLanguage(Regex vietnameseLetters)
    {
        var englishPhrases = new Regex(@"\b(?:step|answer|solution|there are|how many|how much|what is|find the|calculate|compare|which)\b", RegexOptions.IgnoreCase);
        var displayedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "ProblemText", "SolutionText", "SubjectName", "AnswerUnit", "Caption", "Label", "Text",
            "Title", "Unit", "PresentedText", "ChoiceTexts", "Labels", "Explanation"
        };
        IEnumerable<string> ReadDisplayedText(JsonElement element, string field = "")
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                foreach (string text in ReadDisplayedText(property.Value, property.Name)) yield return text;
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                foreach (string text in ReadDisplayedText(item, field)) yield return text;
            }
            else if (element.ValueKind == JsonValueKind.String && displayedFields.Contains(field))
                yield return element.GetString()!;
        }
        int count = 0;
        // Inspect stored presentation fields without evaluating unrelated computed properties.
        var serialization = new JsonSerializerOptions { IgnoreReadOnlyProperties = true };
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        AppLanguage previous = AppLanguageManager.CurrentLanguage;
        try
        {
            foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
            {
                AppLanguageManager.CurrentLanguage = language;
                void Check(ArithmeticQuizQuestion question, string label)
                {
                    IEnumerable<string> text = ReadDisplayedText(JsonSerializer.SerializeToElement(question, serialization));
                    foreach (bool solved in new[] { false, true })
                    {
                        var diagram = QuizDiagramBuilder.Build(question, language, solved);
                        if (diagram is not null) text = text.Concat(ReadDisplayedText(JsonSerializer.SerializeToElement(diagram, serialization)));
                    }
                    var invalid = grader.Validate(question, null, null, null);
                    text = text.Append(EssayFeedbackFormatter.Format(question, invalid, null, null, null, language,
                        CultureInfo.GetCultureInfo(language == AppLanguage.Vietnamese ? "vi-VN" : "en-US")));
                    foreach (string value in text)
                    {
                        bool mixed = language == AppLanguage.Vietnamese
                            ? englishPhrases.IsMatch(value) : vietnameseLetters.IsMatch(value);
                        Require(!mixed, $"{label}/{language}: mixed-language quiz content: {value}");
                    }
                    count++;
                }
                foreach (var (kind, subtype) in AllSubtypes())
                for (int seed = 0; seed < 2; seed++)
                    Check(Generate(kind, subtype, ArithmeticQuizMode.Essay, language, seed), $"{kind}/{subtype}/{seed}");
                foreach (QuizProblemKind kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
                foreach (ElementaryQuizType type in ElementaryQuizGenerator.Types(kind))
                foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
                for (int seed = 0; seed < 2; seed++)
                    Check(new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay, kind, type, language, tier),
                        $"{kind}/{type}/{tier}/{seed}");
            }
        }
        finally { AppLanguageManager.CurrentLanguage = previous; }
        Console.WriteLine($"  Checked {count} bilingual generated problems, solutions, units, diagrams and grading messages.");
    }

    private static Dictionary<string, string> ReadQuizPack(string name)
    {
        using var document = JsonDocument.Parse(ReadQuizResource(name));
        return document.RootElement.GetProperty("strings").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
    }

    private static string ReadQuizResource(string name)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing localization test resource: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
