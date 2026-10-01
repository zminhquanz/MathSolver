using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckInverseProportionVariety()
    {
        ProportionScenarioKind[] expectedScenarios =
        [
            ProportionScenarioKind.WorkersDays, ProportionScenarioKind.WorkersJob,
            ProportionScenarioKind.MachinesHours, ProportionScenarioKind.FoodPeopleDays,
            ProportionScenarioKind.FoodAdditionalPeople, ProportionScenarioKind.SalesStock,
            ProportionScenarioKind.WorkersRequired, ProportionScenarioKind.MachinesRequired,
            ProportionScenarioKind.TapsTime, ProportionScenarioKind.TravelSpeedTime,
            ProportionScenarioKind.TransportTrips, ProportionScenarioKind.PackagingCount
        ];
        var storyValidator = new LlmWordProblemValidator();
        var essayValidator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;

        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            AppLanguageManager.CurrentLanguage = language;
            var seenTemplates = new HashSet<string>();
            var scenarios = new Dictionary<ProportionScenarioKind, int>();
            var directions = new HashSet<(ProportionScenarioKind Scenario, bool Increased)>();
            foreach (bool aiContract in new[] { false, true })
            foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var generator = new ProportionQuizGenerator(
                    new Random(12345 + (int)language * 100 + (int)mode * 10 + (aiContract ? 1 : 0)));
                var seenInMode = new HashSet<ProportionScenarioKind>();
                for (int index = 0; index < 400; index++)
                {
                    ArithmeticQuizQuestion question = aiContract
                        ? generator.GenerateContract(mode, ProportionQuizType.Inverse, language)
                        : generator.GenerateAlgorithm(mode, ProportionQuizType.Inverse, language);
                    ProportionQuizContract contract = question.ProportionProblem!;
                    string label = $"{language}/{mode}/{aiContract}/{contract.Scenario}";
                    seenInMode.Add(contract.Scenario);
                    scenarios[contract.Scenario] = scenarios.GetValueOrDefault(contract.Scenario) + 1;
                    Require(contract.Type == ProportionQuizType.Inverse, $"{label}: wrong type.");
                    Require(contract.A > 0 && contract.B > 0 && contract.C > 0 &&
                            contract.CorrectAnswer > 0 && contract.A * contract.B % contract.C == 0,
                        $"{label}: nonpositive or fractional data.");
                    bool changesSecond = contract.Scenario is
                        ProportionScenarioKind.WorkersRequired or ProportionScenarioKind.MachinesRequired or
                        ProportionScenarioKind.SalesStock or ProportionScenarioKind.PackagingCount or
                        ProportionScenarioKind.FoodAdditionalPeople;
                    Require(contract.InverseChangesSecondQuantity == changesSecond,
                        $"{label}: incorrect numerical roles.");
                    int initialQuantity = changesSecond ? contract.B : contract.A;
                    int correspondingQuantity = changesSecond ? contract.A : contract.B;
                    BigInteger totalAnswer = contract.CorrectAnswer +
                        (contract.AsksForAdditionalPeople ? contract.A : 0);
                    Require(contract.C != initialQuantity &&
                            totalAnswer * contract.C == contract.A * contract.B &&
                            (contract.C > initialQuantity ? totalAnswer < correspondingQuantity
                                : totalAnswer > correspondingQuantity),
                        $"{label}: inverse relationship or changed quantity is wrong.");
                    directions.Add((contract.Scenario, contract.C > initialQuantity));
                    Require(question.CorrectAnswer == contract.CorrectAnswer &&
                            Evaluate(question.Expression.LeftOperand, question.Expression.Operation,
                                question.Expression.RightOperand) == contract.CorrectAnswer,
                        $"{label}: question and arithmetic answer disagree.");
                    CheckIntegerAnswerMode(question, label);
                    Require(Regex.Matches(contract.ProblemText, @"\d+").Count == 3,
                        $"{label}: story must contain exactly three numeric facts.");

                    string templateKey = Regex.Replace(contract.ProblemText, @"\d+", "N");
                    if (seenTemplates.Add(templateKey))
                    {
                        string formatted = ProportionQuizSolutionFormatter.Format(
                            contract, language, CultureInfo.InvariantCulture);
                        string[] lines = formatted.Split(Environment.NewLine);
                        Require(lines.Length == 3 &&
                                lines[2] == $"{(language == AppLanguage.Vietnamese ? "Đáp số" : "Answer")}: " +
                                    $"{contract.CorrectAnswer} {contract.AnswerUnit}",
                            $"{label}: expected one solution, calculation and answer.");
                        var reference = new LlmWordProblemDraft
                        {
                            ProblemText = contract.ProblemText,
                            SubjectName = contract.SubjectName,
                            AnswerUnit = contract.AnswerUnit,
                            SolutionLead = lines[0]
                        };
                        LlmWordProblemValidationResult validated = storyValidator.ValidateProportion(
                            reference, contract, language);
                        Require(validated.IsValid && validated.WordProblem is not null,
                            $"{label}: reference rejected: {validated.ErrorCode} — {validated.Feedback}");
                        var changed = new LlmWordProblemDraft
                        {
                            ProblemText = new Regex(@"\d+").Replace(reference.ProblemText!, "999999", 1),
                            SubjectName = reference.SubjectName,
                            AnswerUnit = reference.AnswerUnit,
                            SolutionLead = reference.SolutionLead
                        };
                        Require(storyValidator.ValidateProportion(changed, contract, language)
                                .ErrorCode == "ProportionFactsMismatch",
                            $"{label}: changed AI fact was not rejected.");

                        string calculation = $"{contract.A} * {contract.B} / {contract.C}" +
                            (contract.AsksForAdditionalPeople ? $" - {contract.A}" : "") +
                            $" = {contract.CorrectAnswer} {contract.AnswerUnit}";
                        string answer = $"{contract.CorrectAnswer} {contract.AnswerUnit}";
                        foreach (ArithmeticQuizQuestion essay in new[]
                        {
                            question with { Mode = ArithmeticQuizMode.Essay },
                            question with { Mode = ArithmeticQuizMode.Essay, WordProblem = validated.WordProblem }
                        })
                            Require(essayValidator.Validate(essay, lines[0], calculation, answer).IsCorrect,
                                $"{label}: correct algorithm/AI essay was rejected.");
                        string prompt = LlmQuizPromptBuilder.BuildProportionUserPrompt(contract, language, null);
                        Require(prompt.Contains(contract.ProblemText, StringComparison.Ordinal) &&
                                prompt.Contains(language == AppLanguage.Vietnamese
                                    ? "C thay đổi đại lượng" : "C changes the quantity", StringComparison.Ordinal),
                            $"{label}: AI prompt lost the reference or inverse roles.");
                    }
                    count++;
                }
                Require(expectedScenarios.All(seenInMode.Contains),
                    $"{language}/{mode}/{aiContract}: some inverse contexts never appeared.");
            }
            Require(seenTemplates.Count == 21,
                $"{language}: expected all 21 inverse templates, got {seenTemplates.Count}.");
            Require(scenarios[ProportionScenarioKind.WorkersDays] < scenarios.Values.Sum() / 4,
                $"{language}: duplicate worker wordings dominate the selection.");
            foreach (ProportionScenarioKind scenario in expectedScenarios)
            {
                Require(directions.Contains((scenario, false)), $"{language}/{scenario}: missing decrease.");
                if (scenario != ProportionScenarioKind.FoodAdditionalPeople)
                    Require(directions.Contains((scenario, true)), $"{language}/{scenario}: missing increase.");
            }
        }
        Console.WriteLine($"  Checked {count} inverse contracts and all 21 bilingual templates on both paths.");
    }
}
