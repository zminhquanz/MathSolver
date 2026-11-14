using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class PreparedDishVenueWorksheetTests
{
    public static void Run(string directory)
    {
        var traces = new List<object>();
        int cases = 0, pdfs = 0;
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Food);
        var tier = CurriculumTier.ThreeStars;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            bool vi = language == AppLanguage.Vietnamese;
            var items = new List<QuizPrintItem>();
            foreach (string dish in new[] { "bot-chien", "spaghetti", "pizzas", "mixed-salad", "hot-dogs", "udon" })
            foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
            foreach (var venue in new[] { (Vi: "quán ăn", En: "eatery", Plural: "eateries"),
                (Vi: "nhà hàng", En: "restaurant", Plural: "restaurants"), (Vi: "tiệm ăn", En: "diner", Plural: "diners") })
            {
                string id = $"theme-food-{dish}-total";
                BasicQuestionContract Create(int seed)
                {
                    if (family == BankQuestionFamily.Fraction)
                        return FractionQuestionCatalogue.Create(profile, ArithmeticOperation.Add, tier, language, new(seed), "fraction-" + id);
                    if (family == BankQuestionFamily.FindX)
                    {
                        var scene = FindXQuestionCatalogue.All.Single(s => s.SourceScene.Id == id);
                        return FindXQuestionCatalogue.Create(profile, scene.EquationOperation, tier, language, new(seed), scene.Role, scene.Id);
                    }
                    return AppliedQuestionCatalogue.Create(profile, ArithmeticOperation.Add, tier, language, new(seed), id);
                }
                string owner = vi ? venue.Vi + " của " : "'s " + venue.En;
                string pair = vi ? "cả hai " + venue.Vi : "the two " + venue.Plural;
                var c = Enumerable.Range(0, 100).Select(Create).First(c => c.Subject.Contains(owner, StringComparison.OrdinalIgnoreCase));
                var d = BasicQuestionTemplates.Example(c);
                var q = c.ToPracticeQuestion(d.ToWordProblem(c), mode, new(11));
                var printed = QuizWorksheetBuilder.Format(q, language);
                string label = $"{dish}/{family}/{language}/{venue.Vi}/{mode}";
                if (!printed.Prompt.Contains(pair, StringComparison.OrdinalIgnoreCase)
                    || !printed.Prompt.Contains(c.Subject, StringComparison.OrdinalIgnoreCase)
                    || !printed.Prompt.Contains(c.OtherSubject, StringComparison.OrdinalIgnoreCase)
                    || printed.Prompt.Contains(vi ? "cơ sở" : "businesses", StringComparison.OrdinalIgnoreCase)
                    || printed.Prompt.Contains('{')) throw new Exception("PDF venue pair changed: " + label + ": " + printed.Prompt);
                if (q.ExactAnswer != c.ExactAnswer) throw new Exception("PDF venue change altered answer: " + label);
                items.Add(printed);
                traces.Add(new { dish, family, language, mode, venue = venue.Vi, printed.Prompt, printed.Answer });
                cases++;
            }
            var sheet = new QuizWorksheet("PREPARED-DISH-VENUES", "Prepared dishes", "Prepared dishes", language, tier, mode, items);
            foreach (bool answers in new[] { false, true })
            {
                byte[] bytes = QuizPdfWriter.Write(sheet, answers);
                PdfAssertions.HasEmbeddedText(bytes);
                string text = PdfTestReader.ExtractText(bytes);
                File.WriteAllText(Path.Combine(directory, $"prepared-dish-venues-{language}-{mode}-{answers}.txt"), text);
                foreach (string pair in vi ? new[] { "cả hai quán ăn", "cả hai nhà hàng", "cả hai tiệm ăn" }
                    : new[] { "the two eateries", "the two restaurants", "the two diners" })
                    if (!answers && !text.Contains(pair, StringComparison.OrdinalIgnoreCase)) throw new Exception("Exported PDF lost pair: " + pair);
                File.WriteAllBytes(Path.Combine(directory, $"prepared-dish-venues-{language}-{mode}-{(answers ? "answers" : "worksheet")}.pdf"), bytes);
                pdfs++;
            }
        }
        File.WriteAllText(Path.Combine(directory, "prepared-dish-venue-pdf-cases.json"), JsonSerializer.Serialize(traces,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"PASS prepared dish venue PDF: {cases} six-dish/three-venue/three-family/VI-EN/mode fixtures, {pdfs} embedded-text worksheets/keys.");
    }
}
