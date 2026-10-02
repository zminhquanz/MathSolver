using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckElementaryContracts()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        var aiValidator = new LlmWordProblemValidator();
        int count = 0;
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        foreach (var type in ElementaryQuizGenerator.Types(kind))
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 4; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(
                mode, kind, type, language, tier);
            var contract = question.ElementaryProblem!;
            Require(MathSolver.Services.Localization.QuizLocalizationOverrides.TryGetValue(
                "Quiz.Elementary." + type, language == AppLanguage.Vietnamese ? "vi" : "en", out var label)
                && !string.IsNullOrWhiteSpace(label), "Missing bilingual subtype label.");
            Require(contract.ChoiceTexts!.Count == 4 && contract.ChoiceTexts.Distinct().Count() == 4,
                $"{type}/{seed}: expected four distinct choices.");
            Require(contract.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                $"{type}/{seed}: ambiguous or missing correct choice.");
            Require(ElementaryEssayValidator.CheckAnswers(question, contract.PresentedText) == question.PresentedEquationIsCorrect,
                $"{type}/{seed}: true/false answer mismatch.");
            var parts = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
            var result = validator.Validate(question, parts.Solution, parts.Equation, parts.Answer);
            Require(result.IsCorrect, $"{language}/{tier}/{type}/{seed}: example rejected: " +
                string.Join(" | ", result.Details) + "\n" + contract.SolutionText);
            var draft = new LlmWordProblemDraft { ProblemText = contract.ProblemText,
                SolutionLead = contract.Answers[0].Label, AnswerUnit = contract.Answers[0].Unit,
                SubjectName = contract.Answers[0].Label };
            var ai = aiValidator.ValidateElementary(draft, contract);
            Require(ai.IsValid, $"{type}: AI draft should preserve the authoritative contract.");
            string prompt = LlmQuizPromptBuilder.BuildElementaryUserPrompt(contract);
            Require(LlmWordProblemParser.TryParse(prompt[prompt.IndexOf('{')..], out var parsedDraft,
                out _, out _, allowEmptyAnswerUnit: true) && aiValidator.ValidateElementary(parsedDraft!, contract).IsValid,
                $"{type}: the production JSON parser must accept the new prompt schema.");
            var aiQuestion = question with { WordProblem = ai.WordProblem };
            Require(validator.Validate(aiQuestion, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                $"{type}: AI and Algorithm grading disagree.");
            Require(!aiValidator.ValidateElementary(new LlmWordProblemDraft { ProblemText = contract.ProblemText + " 999", AnswerUnit = contract.Answers[0].Unit }, contract).IsValid,
                "AI must not append unrelated facts.");
            Require(!aiValidator.ValidateElementary(new LlmWordProblemDraft { ProblemText = contract.ProblemText, AnswerUnit = "invalidunit" }, contract).IsValid,
                "AI must not replace the expected unit.");
            count++;
        }
        Require(count > 3000, "The new curriculum matrix is unexpectedly small.");
        Console.WriteLine($"  Checked {count} elementary contracts, examples, choices and AI responses.");

        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var type in new[] { ElementaryQuizType.FractionOfNumber, ElementaryQuizType.WholeFromFraction })
        {
            var contexts = FractionQuantityStoryContextCatalog.GetProfile(language);
            var seenUnits = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                var question = new ElementaryQuizGenerator(new Random(seed)).Generate(
                    ArithmeticQuizMode.Essay, QuizProblemKind.FractionSkills, type, language, CurriculumTier.FiveStars);
                var contract = question.ElementaryProblem!;
                var answer = contract.Answers[0];
                seenUnits.Add(answer.Unit);
                var context = contexts.Single(story => story.Unit == answer.Unit);
                bool findingPart = type == ElementaryQuizType.FractionOfNumber;
                int given = int.Parse(contract.Facts[0]), numerator = int.Parse(contract.Facts[1]), denominator = int.Parse(contract.Facts[2]);
                int expectedValue = findingPart ? given * numerator / denominator : given * denominator / numerator;
                Require(answer.Value == new ReducedFraction(expectedValue, 1) && answer.Label ==
                    (findingPart ? context.PartLabel : context.WholeLabel), "Fraction story labels or answer differ from the given facts.");
                var parts = EssayCombinedInputParser.Parse(contract.SolutionText, true, true);
                Require(validator.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    $"{type}/{language}/{answer.Unit}: themed example was rejected.");
                string prompt = LlmQuizPromptBuilder.BuildElementaryUserPrompt(contract);
                Require(LlmWordProblemParser.TryParse(prompt[prompt.IndexOf('{')..], out var draft,
                    out _, out _, allowEmptyAnswerUnit: true), "Themed AI prompt could not be parsed.");
                var ai = aiValidator.ValidateElementary(draft!, contract);
                Require(ai.IsValid && validator.Validate(question with { WordProblem = ai.WordProblem },
                    parts.Solution, parts.Equation, parts.Answer).IsCorrect, "AI themed grading differs from Algorithm.");
                if (answer.Unit is "lít" or "litres")
                {
                    foreach (var sourceQuestion in new[] { question, question with { WordProblem = ai.WordProblem } })
                        Require(validator.Validate(sourceQuestion, parts.Solution,
                            parts.Equation.Replace(" " + answer.Unit, " l", StringComparison.Ordinal),
                            answer.Value + "l").IsCorrect, "The litre symbol must remain accepted in calculations and answers.");
                    Require(!ElementaryEssayValidator.CheckAnswers(question, answer.Value + "ml"),
                        "Millilitres must not be treated as litres without conversion.");
                }
                var diagram = QuizDiagramBuilder.Build(question, language)!;
                Require(diagram.Caption.EndsWith(" " + answer.Unit, StringComparison.Ordinal) &&
                    diagram.Rows[0].Label.EndsWith(" " + answer.Unit, StringComparison.Ordinal) &&
                    diagram.Explanation is null, "Themed diagram lost the unit or revealed the solution.");
            }
            Require(contexts.All(context => seenUnits.Contains(context.Unit)), "Fraction stories did not cover all themes.");
        }
        Console.WriteLine("  Checked 400 themed fraction-part/whole stories, AI contracts, grading and diagram units.");
        CheckDataChartStories(validator, aiValidator);
    }

    private static void CheckDataChartStories(EssayAnswerValidator validator, LlmWordProblemValidator aiValidator)
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var type in ElementaryQuizGenerator.Types(QuizProblemKind.Data))
        {
            var contexts = DataChartStoryContextCatalog.GetProfile(language);
            var seenThemes = new HashSet<string>();
            var pieDistributions = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                var mode = Enum.GetValues<ArithmeticQuizMode>()[seed % 3];
                var question = new ElementaryQuizGenerator(new Random(seed)).Generate(
                    mode, QuizProblemKind.Data, type, language, (CurriculumTier)(seed % 5 + 1));
                var contract = question.ElementaryProblem!;
                var visual = contract.Visual!;
                Require(visual.Labels.Count == 3 && visual.Labels.Distinct().Count() == 3 && visual.Values.Count == 3,
                    "A themed chart must have three distinct labeled values.");
                var context = contexts.Single(story => story.Labels.Order().SequenceEqual(visual.Labels.Order()));
                seenThemes.Add(context.Description);
                Require(contract.ProblemText.Contains(context.Description, StringComparison.Ordinal),
                    "The chart question lost its story context.");
                Require(contract.Facts.SequenceEqual(visual.Values.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))),
                    "The chart and grading facts disagree.");
                string expectedUnit = type == ElementaryQuizType.ReadPieChart ? "%" : context.Unit;
                Require(visual.Unit == expectedUnit && contract.Answers[0].Unit == expectedUnit,
                    "The question, chart and answer must use the same unit.");
                int[] queriedRows = Enumerable.Range(0, visual.Labels.Count)
                    .Where(index => contract.ProblemText.Contains($"“{visual.Labels[index]}”", StringComparison.Ordinal)).ToArray();
                decimal expectedValue = type switch
                {
                    ElementaryQuizType.ChartTotal => visual.Values.Sum(),
                    ElementaryQuizType.ChartDifference when queriedRows.Length == 2 =>
                        Math.Abs(visual.Values[queriedRows[0]] - visual.Values[queriedRows[1]]),
                    _ when queriedRows.Length == 1 => visual.Values[queriedRows[0]],
                    _ => throw new InvalidOperationException("Chart question refers to the wrong number of categories.")
                };
                Require(contract.Answers[0].Value == new ReducedFraction((int)expectedValue, 1),
                    "The answer differs from the categories actually requested in the chart question.");
                Require(contract.ChoiceTexts!.Count(choice => ElementaryEssayValidator.CheckAnswers(question, choice)) == 1,
                    "A themed chart must retain exactly one correct choice.");
                var parts = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
                Require(validator.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                    $"{type}/{language}/{context.Description}: themed chart example was rejected.");
                string prompt = LlmQuizPromptBuilder.BuildElementaryUserPrompt(contract);
                Require(LlmWordProblemParser.TryParse(prompt[prompt.IndexOf('{')..], out var draft,
                    out _, out _, allowEmptyAnswerUnit: true), "The themed chart AI prompt could not be parsed.");
                var ai = aiValidator.ValidateElementary(draft!, contract);
                Require(ai.IsValid && validator.Validate(question with { WordProblem = ai.WordProblem },
                    parts.Solution, parts.Equation, parts.Answer).IsCorrect, "Chart grading differs between Algorithm and AI.");
                if (type == ElementaryQuizType.ReadPieChart)
                {
                    Require(visual.Values.All(value => value > 0) && visual.Values.Sum() == 100,
                        "Pie chart percentages must be positive and sum to 100.");
                    pieDistributions.Add(string.Join(",", visual.Values));
                }
            }
            Require(contexts.All(context => seenThemes.Contains(context.Description)), "A chart subtype did not sample every theme.");
            if (type == ElementaryQuizType.ReadPieChart)
                Require(pieDistributions.Count > 10, "Pie chart percentages are still repetitive.");
        }
        Console.WriteLine("  Checked 1000 bilingual themed charts, independent answers, percentage totals and AI grading.");
    }

    internal static void CheckElementaryFlexibleWork()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        ArithmeticQuizQuestion Make(QuizProblemKind kind, ElementaryQuizType type,
            string[] facts, string[] constants, params ElementaryAnswer[] answers) =>
            new(new(0, ArithmeticOperation.Add, 0), ArithmeticQuizMode.Essay, 0, null, null, [],
                ElementaryProblem: new(kind, type, AppLanguage.Vietnamese, "Đề toán", "", facts, constants,
                    answers, kind == QuizProblemKind.TwoNumbers));
        var two = Make(QuizProblemKind.TwoNumbers, ElementaryQuizType.SumDifference, ["32", "6"], ["0", "1", "2"],
            new ElementaryAnswer("Số bé", new(13, 1), "", "(32-6)/2"), new("Số lớn", new(19, 1), "", "32-(32-6)/2"));
        var aiTwo = two with { WordProblem = new("Một bài toán tìm hai số.", "Tìm hai số:", "", "số") };
        foreach (var question in new[] { two, aiTwo })
        {
            foreach (string work in new[] { "(32-6)/2=13\n32-13=19", "(32+6)/2=19\n32-19=13",
                "32-6=26\n26/2=13\n13+6=19", "(32-6)/2=13", "(32-6)/2=13; (32+6)/2=19" })
            foreach (string answer in new[] { "13;19", "19;13", "Số lớn:19; Số bé:13" })
                Require(validator.Validate(question, "Hai số cần tìm là:", work, answer).IsCorrect,
                    "Valid two-number work or answer ordering was rejected: " + work + " / " + answer);
            var wrong = validator.Validate(question, "Hai số cần tìm:", "32-6=25\n(32-6)/2=13", "13;19");
            Require(!wrong.IsCorrect && wrong.Steps[0].ComputedValue == "26" && wrong.Steps[0].WrittenValue == "25",
                "A correct final pair must not hide an earlier error.");
            Require(!validator.Validate(question, "Hai số:", "13+0=13", "13;19").IsCorrect,
                "Copying answers without deriving either number must fail.");
            Require(!validator.Validate(question, "Hai số:", "(32-6)/2=13", "13;13").IsCorrect,
                "A duplicate number must not replace the second answer.");
            Require(!validator.Validate(question, "Hai số:", "(32-6)/2=13", "13").IsCorrect,
                "A two-answer question must not accept a missing answer.");
        }
        var sumRatio = Make(QuizProblemKind.TwoNumbers, ElementaryQuizType.SumRatio, ["35", "2", "5"], ["0", "1"],
            new ElementaryAnswer("Số bé", new(10, 1), "", "35/(2+5)*2"), new("Số lớn", new(25, 1), "", "35/(2+5)*5"));
        Require(validator.Validate(sumRatio, "Hai số:", "2+5=7\n35/7=5\n5*2=10\n35-10=25", "10;25").IsCorrect,
            "Sum-ratio work must permit reuse of a part value that is also a given number.");
        var differenceRatio = Make(QuizProblemKind.TwoNumbers, ElementaryQuizType.DifferenceRatio, ["15", "2", "5"], ["0", "1"],
            new ElementaryAnswer("Số bé", new(10, 1), "", "15/(5-2)*2"), new("Số lớn", new(25, 1), "", "15/(5-2)*5"));
        Require(validator.Validate(differenceRatio, "Hai số:", "5-2=3\n15/3=5\n5*5=25\n25-15=10", "25;10").IsCorrect,
            "Difference-ratio work must permit finding the larger number first.");

        var remainder = Make(QuizProblemKind.Remainder, ElementaryQuizType.QuotientRemainder, ["17", "5"], ["0", "1", "2", "3"],
            new ElementaryAnswer("Thương", new(3, 1), "", "(17-2)/5"), new("Số dư", new(2, 1), "", "17-3*5"));
        Require(validator.Validate(remainder, null, "17÷5=3 dư 2", "3;2").IsCorrect, "School remainder notation should be accepted.");
        Require(!validator.Validate(remainder, null, "17÷5=2 dư 7", "3;2").IsCorrect, "A remainder >= divisor must fail.");
        Require(!validator.Validate(remainder, null, "17÷5=4 dư 2", "3;2").IsCorrect, "An incorrect quotient must fail.");
        var vehicles = Make(QuizProblemKind.Remainder, ElementaryQuizType.MinimumGroups, ["17", "5"], ["0", "1", "2", "3"],
            new ElementaryAnswer("Số xe", new(4, 1), "xe", "(17-2)/5+1"));
        Require(validator.Validate(vehicles, null, "17÷5=3 dư 2\n3+1=4 xe", "4xe").IsCorrect,
            "Remainder work should establish the number of extra vehicles.");
        Require(!validator.Validate(vehicles, null, "17÷5=3 dư 2", "4xe").IsCorrect, "The extra vehicle must be justified.");

        var decimalQuestion = Make(QuizProblemKind.Decimal, ElementaryQuizType.DecimalAdd, ["1.25", "0.5"], ["0", "1"],
            new ElementaryAnswer("Kết quả", new(7, 4), "", "1.25+0.5"));
        Require(validator.Validate(decimalQuestion, null, "1,250+0,500=1,750", "1,750").IsCorrect,
            "Decimal context must accept comma notation and trailing zeros, including three decimal places.");
        Require(!validator.Validate(decimalQuestion, null, "1.25+0.5=1.8", "1.75").IsCorrect,
            "Decimal calculations must remain exact, without rounding tolerance.");
        var mixed = Make(QuizProblemKind.FractionSkills, ElementaryQuizType.MixedNumber, ["7", "3"], ["0", "1"],
            new ElementaryAnswer("Hỗn số", new(7, 3), "", "7/3", RequireMixedNumber: true));
        Require(validator.Validate(mixed, null, "7/3=7/3", "2 1/3").IsCorrect, "A proper mixed answer must be accepted.");
        Require(!validator.Validate(mixed, null, "7/3=7/3", "7/3").IsCorrect, "An improper fraction does not meet a mixed-number task.");
        var reduce = Make(QuizProblemKind.FractionSkills, ElementaryQuizType.ReduceFraction, ["6", "8"], ["0", "1", "2"],
            new ElementaryAnswer("Phân số", new(3, 4), "", "6/8", RequireReduced: true));
        Require(validator.Validate(reduce, null, "6/8=3/4", "3/4").IsCorrect, "Lowest terms should pass.");
        Require(!validator.Validate(reduce, null, "6/8=3/4", "6/8").IsCorrect, "An unreduced final answer must fail a reduction task.");
        Require(!validator.Validate(reduce, null, "6/8=3/4", "0.75").IsCorrect, "A decimal does not meet a request for a reduced fraction.");
        Require(validator.Validate(mixed, null, "7/3=2 1/3", "2 1/3").IsCorrect, "Mixed numbers should work in the calculation result too.");
        Require(validator.Validate(mixed, null, "7÷3=2 dư 1", "2 1/3").IsCorrect, "A mixed number may be derived by school integer division.");
        var common = Make(QuizProblemKind.FractionSkills, ElementaryQuizType.CommonDenominator, ["1", "2", "4"], ["0"],
            new ElementaryAnswer("Phân số thứ nhất", new(1, 2), "", "1/2", RequiredDenominator: 4),
            new("Phân số thứ hai", new(1, 4), "", "1/4", RequiredDenominator: 4));
        Require(validator.Validate(common, null, "1/2=2/4\n1/4=1/4", "2/4;1/4").IsCorrect, "The requested common denominator should pass.");
        Require(!validator.Validate(common, null, "1/2=2/4\n1/4=1/4", "1/2;1/4").IsCorrect, "Equivalent values with the wrong denominator should fail the task form.");
        var clock = Make(QuizProblemKind.Time, ElementaryQuizType.ReadClock, ["3", "15"], ["0", "1"],
            new ElementaryAnswer("Giờ", new(3, 1), "", "3"), new("Phút", new(15, 1), "", "15"));
        Require(validator.Validate(clock, null, null, "3:15").IsCorrect && validator.Validate(clock, null, null, "3 giờ 15 phút").IsCorrect,
            "Clock reading should also accept natural time notation.");
        Require(!validator.Validate(clock, null, null, "3:16").IsCorrect, "Clock reading must retain the minute value.");
        var mass = Make(QuizProblemKind.Measurement, ElementaryQuizType.MassConversion, ["2"], ["1000"],
            new ElementaryAnswer("Khối lượng", new(2000, 1), "g", "2*1000"));
        mass = mass with { ElementaryProblem = mass.ElementaryProblem! with { RequiresSolution = true } };
        Require(validator.Validate(mass, "Số gam là:", "2*1000=2000g", "2000gam").IsCorrect, "Solution and answer unit aliases should be shared with legacy grading.");
        Require(!validator.Validate(mass, "Số gam là:", "2*1000=2000kg", "2000g").IsCorrect, "Different units must not become equivalent just by matching a substring.");

        var random = new Random(761);
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        {
            var samples = Enumerable.Range(0, 150).Select(_ => new ElementaryQuizGenerator(random).Generate(
                ArithmeticQuizMode.Essay, kind, null, AppLanguage.English, CurriculumTier.FiveStars).ElementaryProblem!.Type).ToHashSet();
            Require(ElementaryQuizGenerator.Types(kind).All(samples.Contains), $"{kind}: Mixed never selected a subtype.");
        }
    }
}
