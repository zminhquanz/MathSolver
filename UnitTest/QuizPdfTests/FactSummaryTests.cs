using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

internal static class FactSummaryTests
{
    internal static async Task Run(PracticeQuestionFactory factory, string directory)
    {
        int cases = 0;
        var groups = new HashSet<QuestionKnowledgeGroup>();
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var dataStories = new List<QuizPrintItem>();
            foreach (var scene in AppliedQuestionCatalogue.All)
            foreach (var tier in Enum.GetValues<CurriculumTier>().Where(t => (int)t >= scene.MinimumStars))
            {
                var contract = AppliedQuestionCatalogue.Create(new(scene.Group), scene.Operation,
                    tier, language, new Random(31), scene.Id);
                for (int variant = 0; variant < 2; variant++)
                {
                    var question = contract.ToPracticeQuestion(AppliedQuestionCatalogue.Draft(contract, variant).ToWordProblem(contract), ArithmeticQuizMode.Essay);
                    CheckStory(question, language);
                    if (scene.Group == QuestionKnowledgeGroup.Data && (int)tier == scene.MinimumStars && variant == 0)
                        dataStories.Add(QuizWorksheetBuilder.Format(question, language));
                    cases++;
                }
                groups.Add(scene.Group);
            }
            foreach (var scene in FindXQuestionCatalogue.All)
            foreach (var tier in Enum.GetValues<CurriculumTier>().Where(t => (int)t >= scene.MinimumStars))
            {
                var contract = FindXQuestionCatalogue.Create(new(scene.Group), scene.EquationOperation,
                    tier, language, new Random(47), sceneId: scene.Id);
                var question = contract.ToPracticeQuestion(FindXQuestionCatalogue.Draft(contract).ToWordProblem(contract), ArithmeticQuizMode.Essay);
                CheckStory(question, language);
                if (scene.Group == QuestionKnowledgeGroup.Data && (int)tier == scene.MinimumStars)
                    dataStories.Add(QuizWorksheetBuilder.Format(question, language));
                cases++;
                groups.Add(scene.Group);
            }
            foreach (var scene in FractionQuestionCatalogue.All)
            foreach (var tier in Enum.GetValues<CurriculumTier>().Where(t => (int)t >= scene.MinimumStars))
            {
                var contract = FractionQuestionCatalogue.Create(new(scene.Group), scene.Operation,
                    tier, language, new Random(53), scene.Id);
                var question = contract.ToPracticeQuestion(FractionQuestionCatalogue.Draft(contract).ToWordProblem(contract), ArithmeticQuizMode.Essay);
                CheckStory(question, language);
                cases++;
                groups.Add(scene.Group);
            }

            // Reproduce the survey story from the reported preview, using its real catalogue.
            var survey = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Data), ArithmeticOperation.Add,
                CurriculumTier.OneStar, language, new Random(17), "story-survey-total") with { Left = 1, Right = 4 };
            var story = survey.ToPracticeQuestion(AppliedQuestionCatalogue.Draft(survey).ToWordProblem(survey), ArithmeticQuizMode.Essay);
            var printed = QuizWorksheetBuilder.Format(story, language);
            Check(printed.Prompt.Contains("1 ") && printed.Prompt.Contains("4 ") && printed.Answer.StartsWith("5 "), "Survey facts or answer changed.");
            var sheet = new QuizWorksheet("FACTS", "Survey practice", "Data", language, CurriculumTier.OneStar,
                ArithmeticQuizMode.Essay, [printed, printed]);
            byte[] bytes = QuizPdfWriter.Write(sheet, false);
            string text = PdfTestReader.ExtractText(bytes);
            foreach (var row in story.WordProblem!.FactTable!.Rows)
                Check(!text.Contains(row.Label + ":"), "PDF still prints a redundant fact summary.");
            Check(text.Contains(printed.Prompt), "PDF lost the survey question.");
            File.WriteAllBytes(Path.Combine(directory, $"survey-without-summary-{language}.pdf"), bytes);

            // Include the football/badminton example, not just the generic first/second labels.
            var sports = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Data), ArithmeticOperation.Add,
                CurriculumTier.OneStar, language, new Random(19), "survey-total") with { Left = 8, Right = 1 };
            var sportsQuestion = sports.ToPracticeQuestion(AppliedQuestionCatalogue.Draft(sports).ToWordProblem(sports), ArithmeticQuizMode.Essay);
            var sportsItem = QuizWorksheetBuilder.Format(sportsQuestion, language);
            dataStories.Insert(0, sportsItem);
            bytes = QuizPdfWriter.Write(sheet with { Questions = dataStories }, false);
            text = PdfTestReader.ExtractText(bytes);
            Check(sportsItem.Prompt.Contains("8 ") && sportsItem.Prompt.Contains("1 ") && text.Contains(sportsItem.Prompt),
                "Sports survey lost its question or given values.");
            foreach (var item in dataStories)
            foreach (var row in item.Source.WordProblem!.FactTable!.Rows)
                Check(!text.Contains(row.Label + ": " + row.Value), "A data scene still repeats facts in its exported PDF.");
            File.WriteAllBytes(Path.Combine(directory, $"all-data-stories-without-summaries-{language}.pdf"), bytes);
            File.WriteAllText(Path.Combine(directory, $"all-data-stories-without-summaries-{language}.txt"), text);

            // Tables that contain required data must still appear in both preview and PDF.
            var required = new QuestionFactTable("Category", "Quantity", [new("Required table row", "17")]);
            var tableQuestion = story with { WordProblem = story.WordProblem with { FactTable = required } };
            var tableItem = QuizWorksheetBuilder.Format(tableQuestion, language);
            Check(tableItem.Table == required, "Required fact table removed from preview.");
            text = PdfTestReader.ExtractText(QuizPdfWriter.Write(sheet with { Questions = [tableItem, printed] }, false));
            Check(text.Contains("Required table row: 17"), "Required table omitted from PDF.");

            var visualItems = new List<QuizPrintItem>();
            foreach (var type in new[] { ElementaryQuizType.ReadTable, ElementaryQuizType.ReadBarChart, ElementaryQuizType.ReadPieChart,
                ElementaryQuizType.ChartTotal, ElementaryQuizType.ChartDifference, ElementaryQuizType.ReadPictograph,
                ElementaryQuizType.SortData, ElementaryQuizType.CompleteBarChart })
            {
                var question = await factory.CreateAsync(new(QuizProblemKind.Data, ElementaryType: type), ArithmeticQuizMode.Essay,
                    new(CurriculumTier.ThreeStars, false), language, new(QuestionKnowledgeGroup.Data), PracticeQuestionFormat.WordProblem, false);
                var item = QuizWorksheetBuilder.Format(question, language);
                Check(item.Visual is not null && item.Visual.Kind == question.ElementaryProblem!.Visual!.Kind,
                    "Essential table/chart removed from preview.");
                visualItems.Add(item);
            }
            bytes = QuizPdfWriter.Write(sheet with { Questions = visualItems }, false);
            File.WriteAllBytes(Path.Combine(directory, $"required-tables-and-charts-{language}.pdf"), bytes);
            PdfAssertions.HasEmbeddedText(bytes);
        }
        Check(QuestionLearningProfile.Groups().All(group => groups.Contains(group)), "A selectable knowledge group was not audited.");
        Console.WriteLine($"PASS fact summary printing: {cases} catalogue cases across all knowledge groups (including legacy groups), supported stars and both languages; every data scene exported without repeated facts, required tables and eight chart types retained.");
    }

    private static void CheckStory(ArithmeticQuizQuestion question, AppLanguage language)
    {
        var source = question.WordProblem!;
        var printed = QuizWorksheetBuilder.Format(question, language);
        Check(!printed.Prompt.Contains(';'), "A knowledge-group story still separates question data with semicolons.");
        Check(printed.Table is null && printed.Prompt == source.ProblemText, "Preview repeats facts or changes the story.");
        if (source.FactTable is not null)
            Check(source.FactTable.Rows.Count == 2, "Printing changed practice data.");
        var withoutSummary = QuizWorksheetBuilder.Format(question with { WordProblem = source with { FactTable = null } }, language);
        Check(printed.Identity == withoutSummary.Identity && printed.Answer == withoutSummary.Answer && printed.Solution == withoutSummary.Solution,
            "Hidden summaries change worksheet deduplication, answers or solutions.");
    }
}
