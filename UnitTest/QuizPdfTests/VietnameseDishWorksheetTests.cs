using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Printing;
using System.Text.Json;

/// <summary>End-to-end PDF assertions for all twelve Vietnamese prepared foods.</summary>
internal static class VietnameseDishWorksheetTests
{
    private static readonly (string Id, string Vietnamese, string English)[] Dishes =
    [
        ("com-suon-cay", "cơm sườn cây", "grilled pork ribs"),
        ("com-chien-hai-san", "cơm chiên hải sản", "seafood fried rice"),
        ("com-chien-duong-chau", "cơm chiên Dương Châu", "Yangzhou fried rice"),
        ("com-ga-xoi-mo", "cơm gà xối mỡ", "crispy fried chicken"),
        ("com-ga-sot-me", "cơm gà sốt me", "tamarind sauce"),
        ("com-ga-sot-tieu-xanh", "cơm gà sốt tiêu xanh", "green peppercorn sauce"),
        ("canh-chua-ca-loc", "canh chua cá lóc", "sour snakehead fish soup"),
        ("canh-kho-qua-nhoi-thit", "canh khổ qua nhồi thịt", "stuffed bitter melon"),
        ("canh-bi-do-ham-xuong", "canh bí đỏ hầm xương", "pumpkin and pork bone"),
        ("canh-cua-rau-day-mong-toi", "canh cua rau đay, mồng tơi", "jute mallow and Malabar spinach"),
        ("canh-rau-ngot-thit-bam", "canh rau ngót thịt băm", "sweet leaf soup with minced pork"),
        ("ca-loc-kho-to", "cá lóc kho tộ", "claypot braised snakehead fish")
    ];

    public static void Run(string directory)
    {
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Food);
        const CurriculumTier tier = CurriculumTier.ThreeStars;
        var traces = new List<object>();
        int cases = 0, pdfs = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            bool vietnamese = language == AppLanguage.Vietnamese;
            string ownerVenue = vietnamese ? "nhà hàng của " : "'s restaurant";
            var items = new List<QuizPrintItem>();
            foreach (var dish in Dishes)
            {
                var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == dish.Id);
                if (subject.Group != QuestionKnowledgeGroup.Food ||
                    FoodThemeCatalogue.IsBakeryFood(dish.Id))
                    throw new Exception("Vietnamese prepared food has wrong catalogue category: " + dish.Id);
                foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
                {
                    string source = $"theme-food-{dish.Id}-total";
                    BasicQuestionContract Create(int seed)
                    {
                        if (family == BankQuestionFamily.Fraction)
                            return FractionQuestionCatalogue.Create(profile, ArithmeticOperation.Add, tier,
                                language, new(seed), "fraction-" + source);
                        if (family == BankQuestionFamily.FindX)
                        {
                            var scene = FindXQuestionCatalogue.All.Single(s => s.SourceScene.Id == source);
                            return FindXQuestionCatalogue.Create(profile, scene.EquationOperation, tier,
                                language, new(seed), scene.Role, scene.Id);
                        }
                        return AppliedQuestionCatalogue.Create(profile, ArithmeticOperation.Add, tier,
                            language, new(seed), source);
                    }

                    var contract = Enumerable.Range(0, 150).Select(Create).First(c =>
                        c.Subject.Contains(ownerVenue, StringComparison.OrdinalIgnoreCase));
                    if (contract.Unit != subject.Unit.Item(language) || FoodThemeCatalogue.IsBakeryContract(contract))
                        throw new Exception("Vietnamese food unit/venue changed: " + dish.Id);

                    var draft = BasicQuestionTemplates.Example(contract);
                    var question = contract.ToPracticeQuestion(draft.ToWordProblem(contract), mode, new(11));
                    var printed = QuizWorksheetBuilder.Format(question, language);
                    string name = vietnamese ? dish.Vietnamese : dish.English;
                    if (question.ExactAnswer != contract.ExactAnswer
                        || !printed.Prompt.Contains(name, StringComparison.OrdinalIgnoreCase)
                        || !printed.Prompt.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase)
                        || printed.Prompt.Contains('{'))
                        throw new Exception("Vietnamese dish PDF lost name, venue, or answer: "
                            + dish.Id + "/" + family + "/" + language + ": " + printed.Prompt);
                    items.Add(printed);
                    traces.Add(new { dish.Id, family, language, mode, name, contract.Unit,
                        contract.SceneId, printed.Prompt, printed.Answer });
                    cases++;
                }
            }
            var worksheet = new QuizWorksheet("VIETNAMESE-FOODS",
                vietnamese ? "Món ăn Việt Nam" : "Vietnamese dishes",
                vietnamese ? "Món ăn Việt Nam" : "Vietnamese dishes",
                language, tier, mode, items);
            foreach (bool answers in new[] { false, true })
            {
                byte[] pdf = QuizPdfWriter.Write(worksheet, answers);
                PdfAssertions.HasEmbeddedText(pdf);
                string text = PdfTestReader.ExtractText(pdf);
                foreach (var dish in Dishes)
                {
                    string name = vietnamese ? dish.Vietnamese : dish.English;
                    if (!text.Contains(name, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Embedded Vietnamese dish name missing from PDF: " + name);
                }
                File.WriteAllBytes(Path.Combine(directory,
                    $"vietnamese-dishes-{language}-{mode}-{(answers ? "answers" : "worksheet")}.pdf"), pdf);
                pdfs++;
            }
        }
        File.WriteAllText(Path.Combine(directory, "vietnamese-food-pdf-cases.json"),
            JsonSerializer.Serialize(traces, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS Vietnamese food PDF: {cases} cases across 12 dishes, 3 families, VI/EN and modes; {pdfs} text-verified worksheets and answer keys.");
    }
}
