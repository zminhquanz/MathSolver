using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class JapaneseDishWorksheetTests
{
    private static readonly string[] DishIds =
    [
        "ramen", "sashimi", "tempura", "tonkatsu", "soba", "somen", "yakisoba",
        "gyudon", "oyakodon", "katsudon", "omurice", "onigiri", "nikujaga",
        "sukiyaki", "shabu-shabu", "yakiniku", "oden", "takoyaki", "okonomiyaki",
        "yakitori", "karaage", "gyoza", "korokke", "mochi", "dorayaki", "taiyaki",
        "dango", "anmitsu", "kakigori"
    ];

    public static void Run(string directory)
    {
        if (DishIds.Length != 29) throw new Exception("The Japanese dish inventory must have 29 entries");
        const ArithmeticOperation operation = ArithmeticOperation.Add;
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Food);
        const CurriculumTier tier = CurriculumTier.ThreeStars;
        int questions = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            var items = new List<QuizPrintItem>();
            foreach (string id in DishIds)
            {
                var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == id);
                if (subject.Group != QuestionKnowledgeGroup.Food ||
                    !FoodThemeCatalogue.NewRestaurantDishes.Contains(subject) ||
                    FoodThemeCatalogue.IsBakeryFood(id) || FoodThemeCatalogue.DessertShopKind(id) is not null)
                    throw new Exception("Japanese dish lost restaurant routing: " + id);

                foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
                {
                    string sceneId = $"theme-food-{id}-total";
                    BasicQuestionContract contract = family switch
                    {
                        BankQuestionFamily.Fraction => FractionQuestionCatalogue.Create(profile, operation, tier, language,
                            new Random(37), "fraction-" + sceneId),
                        BankQuestionFamily.FindX => CreateFindX(sceneId, language, tier, profile),
                        _ => AppliedQuestionCatalogue.Create(profile, operation, tier, language, new Random(37), sceneId)
                    };
                    var draft = BasicQuestionTemplates.Example(contract);
                    if (!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), contract).IsValid)
                        throw new Exception("Japanese source failed validation: " + id + "/" + family);
                    var question = contract.ToPracticeQuestion(draft.ToWordProblem(contract), ArithmeticQuizMode.Essay, new Random(11));
                    var printed = QuizWorksheetBuilder.Format(question, language);
                    if (question.ExactAnswer != contract.ExactAnswer || printed.Prompt.Contains('{') ||
                        !printed.Prompt.Contains(contract.Unit, StringComparison.OrdinalIgnoreCase) ||
                        !printed.Prompt.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Japanese dish/answer missing from PDF input: " + id + "/" + family);
                    items.Add(printed);
                    questions++;
                }
            }

            var sheet = new QuizWorksheet("JAPANESE-FOOD", "Món ăn Nhật Bản / Japanese dishes",
                "29 món Nhật · Phép tính, phân số, Tìm X", language, tier, ArithmeticQuizMode.Essay, items);
            foreach (bool answers in new[] { false, true })
            {
                byte[] bytes = QuizPdfWriter.Write(sheet, answers);
                PdfAssertions.HasEmbeddedText(bytes);
                string pdfText = PdfTestReader.ExtractText(bytes);
                if (!pdfText.Contains("Japanese", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Japanese worksheet text missing from PDF");
                File.WriteAllBytes(Path.Combine(directory, $"japanese-food-{language}-{(answers ? "answers" : "worksheet")}.pdf"), bytes);
            }
        }
        Console.WriteLine($"PASS Japanese food PDF: {questions} C#/fraction/FindX VI+EN questions, 4 worksheets/keys with embedded text.");
    }

    private static BasicQuestionContract CreateFindX(string sceneId, AppLanguage language, CurriculumTier tier,
        QuestionLearningProfile profile)
    {
        var scene = FindXQuestionCatalogue.All.First(s => s.SourceScene.Id == sceneId);
        return FindXQuestionCatalogue.Create(profile, scene.EquationOperation, tier, language,
            new Random(37), scene.Role, scene.Id);
    }
}
