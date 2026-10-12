using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMeasurementResultInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        ElementaryQuizType[] types = [ElementaryQuizType.MapScale, ElementaryQuizType.LengthConversion,
            ElementaryQuizType.MassConversion, ElementaryQuizType.CapacityConversion,
            ElementaryQuizType.AreaConversion, ElementaryQuizType.VolumeConversion];
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in types)
        foreach (bool expandedStories in new[] { false, true })
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed), expandActivityStories: expandedStories)
                .Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Measurement, type, language, tier);
            var contract = question.ElementaryProblem!;
            Require(EssayAnswerValidator.IsResultOnlyPractice(question), "Measurement still requires worked steps: " + type);
            string answer = contract.AnswerText;
            string number = contract.Answers[0].DisplayValue ?? contract.Answers[0].Value.ToString();
            string compact = answer.Replace(" ", "");
            var worked = EssayCombinedInputParser.Parse(contract.SolutionText, false, true);
            foreach (string input in new[] { answer, compact, compact.Replace('.', ','),
                compact.Replace("²", "2").Replace("³", "3"), compact.Replace("²", "^2").Replace("³", "^3"),
                "Đáp số: " + compact, "Answer: " + answer, contract.Answers[0].Expression + " = " + compact,
                "Kết quả = " + compact, contract.SolutionText, worked.Equation,
                worked.Solution + "\n" + worked.Equation + "\n" + compact })
            {
                Require(grader.ValidateSubmission(question, input).IsCorrect,
                    "Direct or worked measurement rejected: " + type + "\n" + input);
                count++;
            }
            if (type != ElementaryQuizType.MapScale)
            {
                foreach (string input in new[] { number, number.Replace('.', ','), contract.Answers[0].Value.ToString(),
                    "Đáp số: " + number, "Answer: " + number, "Kết quả = " + number,
                    contract.Answers[0].Expression + " = " + number,
                    worked.Solution + "\n" + worked.Equation + "\nAnswer: " + number })
                {
                    Require(grader.ValidateSubmission(question, input).IsCorrect,
                        "Unit conversion rejected a unitless final result: " + type + "\n" + input);
                    count++;
                }
                Require(grader.Validate(question, worked.Solution, worked.Equation, number).IsCorrect,
                    "Worked unit conversion rejected a unitless final answer: " + type);
            }
            string wrongUnit = contract.Answers[0].Unit == "kg" ? "cm" : "kg";
            foreach (string input in new[] { "", contract.Answers[0].Expression + " =", "Kết quả =",
                "0", contract.Answers[0].DisplayValue + wrongUnit,
                "0" + contract.Answers[0].Unit, "Answer: " + compact + "\nAnswer: 0" + contract.Answers[0].Unit,
                worked.Equation + "\nAnswer: 0" + contract.Answers[0].Unit,
                "Answer: " + number + "\nAnswer: 0" })
                Require(!grader.ValidateSubmission(question, input).IsCorrect,
                    "Missing, wrong or conflicting measurement answer passed: " + type + "\n" + input);
            if (type == ElementaryQuizType.MapScale)
                Require(!grader.ValidateSubmission(question, number).IsCorrect,
                    "Optional conversion units changed the map-scale unit requirement.");

            Require(grader.Validate(question, worked.Solution, worked.Equation, worked.Answer).IsCorrect,
                "Generated measurement steps lost independent calculation validation: " + type);
            Require(!grader.Validate(question, null, "1 = " + answer, answer).IsCorrect,
                "Independent measurement validation stopped checking the conversion relation.");
        }

        // Exercise the user's compact examples and all common target units,
        // including keyboard notation and the difference between length, area and volume.
        var example = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Measurement, ElementaryQuizType.LengthConversion, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        foreach (var (type, unit) in new[] {
            (ElementaryQuizType.MapScale, "m"), (ElementaryQuizType.MapScale, "km"),
            (ElementaryQuizType.LengthConversion, "m"), (ElementaryQuizType.LengthConversion, "cm"),
            (ElementaryQuizType.LengthConversion, "dm"), (ElementaryQuizType.LengthConversion, "km"),
            (ElementaryQuizType.MassConversion, "kg"), (ElementaryQuizType.MassConversion, "g"),
            (ElementaryQuizType.CapacityConversion, "l"), (ElementaryQuizType.CapacityConversion, "ml"),
            (ElementaryQuizType.AreaConversion, "cm²"), (ElementaryQuizType.VolumeConversion, "cm³") })
        {
            var question = example with { ElementaryProblem = new(QuizProblemKind.Measurement, type,
                AppLanguage.Vietnamese, "", "", ["5"], ["0", "1"],
                [new ElementaryAnswer("Kết quả", new(5, 1), unit, "5")], false) };
            foreach (string input in new[] { "5" + unit, "5 " + unit, "Đáp số: 5" + unit,
                "5" + unit.Replace("²", "2").Replace("³", "3"),
                "5" + unit.Replace("²", "^2").Replace("³", "^3"), "Kết quả = 5" + unit })
                Require(grader.ValidateSubmission(question, input).IsCorrect, "Compact example rejected: " + input);
            Require(grader.ValidateSubmission(question, "5").IsCorrect == (type != ElementaryQuizType.MapScale),
                "Unit conversions should accept a unitless result; map scale must retain its unit.");
            Require(!grader.ValidateSubmission(question, "5" + unit + "kg").IsCorrect, "An invalid compound unit was accepted.");
            if (unit is "cm²" or "cm³")
                Require(!grader.ValidateSubmission(question, "5cm").IsCorrect, "Length was accepted instead of area/volume.");
        }
        Console.WriteLine($"  Checked {count} direct and worked unit-conversion/map-scale submissions across both languages, five stars and legacy/expanded contexts; optional conversion units, explicit units, decimal spellings and incorrect answers checked.");
    }
}
