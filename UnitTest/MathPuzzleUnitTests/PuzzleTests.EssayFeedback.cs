using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssayFeedback()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        ArithmeticQuizQuestion algorithm = new AverageQuizGenerator(new Random(91))
            .GenerateAlgorithm(ArithmeticQuizMode.Essay,
                AverageQuizType.Direct, AppLanguage.Vietnamese);
        AverageQuizContract average = algorithm.AverageProblem!;
        ArithmeticQuizQuestion contextual = algorithm with
        {
            WordProblem = new MathWordProblem(
                average.ProblemText, "Trung bình mỗi ngày là:",
                average.AnswerUnit, average.SubjectName)
        };

        foreach (ArithmeticQuizQuestion question in new[] { algorithm, contextual })
        {
            EssayAnswerValidationResult missing = validator.Validate(
                question, string.Empty, string.Empty, string.Empty);
            string missingFeedback = EssayFeedbackFormatter.Format(
                question, missing, string.Empty, string.Empty, string.Empty,
                AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(missingFeedback.Contains("Lời giải:", StringComparison.Ordinal) &&
                    missingFeedback.Contains("Phép tính:", StringComparison.Ordinal) &&
                    missingFeedback.Contains("Đáp số:", StringComparison.Ordinal) &&
                    missingFeedback.Contains(average.AnswerUnit, StringComparison.Ordinal),
                "Feedback did not report all three missing essay fields.");

            string wrongEquation = $"2 + 3 = 9 {average.AnswerUnit}";
            string wrongAnswer = $"999 {average.AnswerUnit}";
            EssayAnswerValidationResult arithmeticErrors = validator.Validate(
                question, $"Số {average.AnswerUnit} là:",
                wrongEquation, wrongAnswer);
            string arithmeticFeedback = EssayFeedbackFormatter.Format(
                question, arithmeticErrors, $"Số {average.AnswerUnit} là:",
                wrongEquation, wrongAnswer,
                AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(arithmeticFeedback.Contains("vế trái tính được 5", StringComparison.Ordinal) &&
                    arithmeticFeedback.Contains("em ghi 9", StringComparison.Ordinal) &&
                    arithmeticFeedback.Contains("em ghi 999", StringComparison.Ordinal) &&
                    !arithmeticFeedback.Contains("Lời giải:", StringComparison.Ordinal),
                $"Feedback did not distinguish the calculated and written results: {arithmeticFeedback}");

            EssayAnswerValidationResult combinedErrors = validator.Validate(
                question, $"Số {average.AnswerUnit} là:",
                "2 + 3 = 9", "999");
            string combinedFeedback = EssayFeedbackFormatter.Format(
                question, combinedErrors, $"Số {average.AnswerUnit} là:",
                "2 + 3 = 9", "999",
                AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(combinedFeedback.Contains("Phép tính: vế trái tính được 5", StringComparison.Ordinal) &&
                    combinedFeedback.Contains("Đáp số: em ghi 999", StringComparison.Ordinal) &&
                    combinedFeedback.Split("Đồng thời thiếu đơn vị", StringSplitOptions.None).Length == 3,
                $"Feedback hid missing units behind incorrect values: {combinedFeedback}");

            string equationWithoutUnit = average.EquationText;
            string answerWithoutUnit = average.CorrectAnswer.ToString();
            EssayAnswerValidationResult unitErrors = validator.Validate(
                question, $"Số {average.AnswerUnit} là:",
                equationWithoutUnit, answerWithoutUnit);
            string unitFeedback = EssayFeedbackFormatter.Format(
                question, unitErrors, $"Số {average.AnswerUnit} là:",
                equationWithoutUnit, answerWithoutUnit,
                AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(unitFeedback.Contains("Phép tính: thiếu đơn vị", StringComparison.Ordinal) &&
                    unitFeedback.Contains("Đáp số: thiếu đơn vị", StringComparison.Ordinal) &&
                    unitFeedback.Contains(average.AnswerUnit, StringComparison.Ordinal),
                $"Feedback did not identify both missing units: {unitFeedback}");

            string wrongUnitEquation = $"{average.EquationText} con chó";
            string wrongUnitAnswer = $"{average.CorrectAnswer} con chó";
            EssayAnswerValidationResult wrongUnits = validator.Validate(
                question, $"Số {average.AnswerUnit} là:",
                wrongUnitEquation, wrongUnitAnswer);
            string wrongUnitFeedback = EssayFeedbackFormatter.Format(
                question, wrongUnits, $"Số {average.AnswerUnit} là:",
                wrongUnitEquation, wrongUnitAnswer,
                AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(wrongUnitFeedback.Contains("Phép tính: đơn vị em ghi là “con chó”", StringComparison.Ordinal) &&
                    wrongUnitFeedback.Contains("Đáp số: em ghi đơn vị “con chó”", StringComparison.Ordinal) &&
                    wrongUnitFeedback.Contains($"cần “{average.AnswerUnit}”", StringComparison.Ordinal),
                $"Feedback did not identify both wrong units: {wrongUnitFeedback}");

            EssayAnswerValidationResult wrongLead = validator.Validate(
                question, "Số con chó là:",
                $"{average.EquationText} {average.AnswerUnit}",
                $"{average.CorrectAnswer} {average.AnswerUnit}");
            string leadFeedback = EssayFeedbackFormatter.Format(
                question, wrongLead, "Số con chó là:",
                $"{average.EquationText} {average.AnswerUnit}",
                $"{average.CorrectAnswer} {average.AnswerUnit}",
                AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(leadFeedback.StartsWith("Lời giải:", StringComparison.Ordinal) &&
                    leadFeedback.Contains(average.AnswerUnit, StringComparison.Ordinal) &&
                    !leadFeedback.Contains("Phép tính:", StringComparison.Ordinal),
                $"Feedback did not identify the wrong solution cue: {leadFeedback}");
        }

        ArithmeticQuizQuestion percentage = new PercentageQuizGenerator(new Random(23))
            .GenerateAlgorithm(ArithmeticQuizMode.Essay,
                PercentageQuizType.FindPercentageRatio, AppLanguage.Vietnamese);
        PercentageQuizContract ratio = percentage.PercentageProblem!;
        EssayAnswerValidationResult percentUnits = validator.Validate(
            percentage, $"Tỉ lệ {ratio.SubjectName} là:",
            ratio.EquationText, ratio.CorrectAnswer.ToString());
        string percentFeedback = EssayFeedbackFormatter.Format(
            percentage, percentUnits, $"Tỉ lệ {ratio.SubjectName} là:",
            ratio.EquationText, ratio.CorrectAnswer.ToString(),
            AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
        Require(percentFeedback.Contains("Phép tính: thiếu đơn vị “%”", StringComparison.Ordinal) &&
                percentFeedback.Contains("Đáp số: thiếu đơn vị “%”", StringComparison.Ordinal),
            $"Percentage feedback did not identify missing percent signs: {percentFeedback}");

        ArithmeticQuizQuestion numeric = new(
            new IntegerArithmeticExpression(2, ArithmeticOperation.Add, 3),
            ArithmeticQuizMode.Essay, 5, null, null, []);
        EssayAnswerValidationResult numericErrors = validator.Validate(
            numeric, string.Empty, "2 + 3 = 6", "7");
        string englishFeedback = EssayFeedbackFormatter.Format(
            numeric, numericErrors, string.Empty, "2 + 3 = 6", "7",
            AppLanguage.English, CultureInfo.InvariantCulture);
        Require(!englishFeedback.Contains("Solution:", StringComparison.Ordinal) &&
                englishFeedback.Contains("Calculation:", StringComparison.Ordinal) &&
                englishFeedback.Contains("Answer:", StringComparison.Ordinal) &&
                englishFeedback.Contains("left side evaluates to 5", StringComparison.Ordinal),
            $"Numeric-only English feedback listed the wrong fields: {englishFeedback}");
    }
}
