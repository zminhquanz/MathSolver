using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Printing;
using System.Text.Json;

internal static class KoreanDishWorksheetTests
{
    private static readonly (string Id, string Name, bool Bakery)[] Dishes =
    [
        ("tteokbokki", "Tteokbokki", false),
        ("korean-corn-dog", "Korean Corn Dog", false),
        ("hotteok", "Hotteok", true),
        ("bungeoppang", "Bungeoppang", true),
        ("gyeranppang", "Gyeranppang", true),
        ("sundae-gukbap", "Sundae Gukbap", false),
        ("andong-jjimdak", "Andong Jjimdak", false),
        ("kalguksu", "Kalguksu", false),
        ("squid-sundae", "Squid Sundae", false)
    ];

    public static void Run(string directory)
    {
        var traces = new List<object>();
        int cases = 0, pdfs = 0;
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Food);
        var tier = CurriculumTier.ThreeStars;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            bool vi = language == AppLanguage.Vietnamese;
            var items = new List<QuizPrintItem>();
            foreach (var dish in Dishes)
            foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
            {
                string sourceScene = $"theme-food-{dish.Id}-total";
                // Deliberately cover bakeries for sweet baked goods and eateries for cooked meals.
                string desiredVenue = FoodThemePrompt.Venue(dish.Bakery,
                    dish.Bakery ? 0 : 1, language);
                BasicQuestionContract Create(int seed)
                {
                    if (family == BankQuestionFamily.Fraction)
                        return FractionQuestionCatalogue.Create(profile, ArithmeticOperation.Add, tier,
                            language, new(seed), "fraction-" + sourceScene);
                    if (family == BankQuestionFamily.FindX)
                    {
                        var scene = FindXQuestionCatalogue.All.Single(s => s.SourceScene.Id == sourceScene);
                        return FindXQuestionCatalogue.Create(profile, scene.EquationOperation, tier,
                            language, new(seed), scene.Role, scene.Id);
                    }
                    return AppliedQuestionCatalogue.Create(profile, ArithmeticOperation.Add, tier,
                        language, new(seed), sourceScene);
                }
                string ownerVenue = vi ? desiredVenue + " của " : "'s " + desiredVenue;
                var contract = Enumerable.Range(0, 150).Select(Create).First(c =>
                    c.Subject.Contains(ownerVenue, StringComparison.OrdinalIgnoreCase));
                if (FoodThemeCatalogue.IsBakeryContract(contract) != dish.Bakery)
                    throw new Exception("Korean dish incorrect venue: " + dish.Id);
                var draft = BasicQuestionTemplates.Example(contract);
                var question = contract.ToPracticeQuestion(draft.ToWordProblem(contract), mode, new(11));
                var printed = QuizWorksheetBuilder.Format(question, language);
                if (question.ExactAnswer != contract.ExactAnswer
                    || !printed.Prompt.Contains(dish.Name, StringComparison.OrdinalIgnoreCase)
                    || !printed.Prompt.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase)
                    || printed.Prompt.Contains('{'))
                    throw new Exception("Korean dish PDF changed identity, venue, or answer: "
                        + dish.Id + "/" + family + "/" + language + ": " + printed.Prompt);
                items.Add(printed);
                traces.Add(new { dish.Id, dish.Name, dish.Bakery, family, language, mode, printed.Prompt, printed.Answer });
                cases++;
            }
            var worksheet = new QuizWorksheet("KOREAN-FOODS", vi ? "Món ăn Hàn Quốc" : "Korean foods",
                vi ? "Món ăn Hàn Quốc" : "Korean foods", language, tier, mode, items);
            foreach (bool answers in new[] { false, true })
            {
                byte[] pdf = QuizPdfWriter.Write(worksheet, answers);
                PdfAssertions.HasEmbeddedText(pdf);
                string actual = PdfTestReader.ExtractText(pdf);
                foreach (var dish in Dishes)
                    if (!actual.Contains(dish.Name, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Embedded Korean dish name missing from PDF: " + dish.Name);
                File.WriteAllBytes(Path.Combine(directory,
                    $"korean-foods-{language}-{mode}-{(answers ? "answers" : "worksheet")}.pdf"), pdf);
                pdfs++;
            }
        }
        File.WriteAllText(Path.Combine(directory, "korean-food-pdf-cases.json"),
            JsonSerializer.Serialize(traces, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS Korean food PDF: {cases} cases across 9 dishes, 3 families, VI/EN and modes; {pdfs} text-verified worksheets and answer keys.");
    }
}
