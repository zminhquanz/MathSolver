using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssayCombinedInput()
    {
        CheckUnifiedEssaySubmission();
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
        string sentence = $"Số {average.AnswerUnit} trung bình là:";
        string equation = $"{average.EquationText} {average.AnswerUnit}";
        string answer = $"{average.CorrectAnswer} {average.AnswerUnit}";

        foreach (ArithmeticQuizQuestion question in new[] { algorithm, contextual })
        {
            var parts = EssayCombinedInputParser.Split(
                $"{sentence}\r\n{equation}", requiresSolution: true);
            Require(parts.Solution == sentence && parts.Equation == equation,
                "Combined editor did not separate the sentence and calculation.");
            Require(validator.Validate(question, parts.Solution,
                parts.Equation, answer).IsCorrect,
                "Combining the editor changed grading for a valid essay.");

            var oneLine = EssayCombinedInputParser.Split(
                $"{sentence} {equation}", requiresSolution: true);
            Require(oneLine.Solution == sentence && oneLine.Equation == equation &&
                    validator.Validate(question, oneLine.Solution,
                        oneLine.Equation, answer).IsCorrect,
                "One-line sentence and calculation were not separated.");

            var onlyEquation = EssayCombinedInputParser.Split(
                equation, requiresSolution: true);
            EssayAnswerValidationResult missingSolution = validator.Validate(
                question, onlyEquation.Solution, onlyEquation.Equation, answer);
            Require(missingSolution.SolutionError == EssayAnswerError.MissingSolution &&
                    missingSolution.EquationIsCorrect,
                "A missing solution sentence was hidden by the combined editor.");

            var onlySentence = EssayCombinedInputParser.Split(
                sentence, requiresSolution: true);
            EssayAnswerValidationResult missingEquation = validator.Validate(
                question, onlySentence.Solution, onlySentence.Equation, answer);
            Require(missingEquation.SolutionIsCorrect &&
                    missingEquation.EquationError == EssayAnswerError.InvalidEquationFormat,
                "A missing calculation was hidden by the combined editor.");
        }

        ArithmeticQuizQuestion numeric = Generate(
            QuizProblemKind.Arithmetic, ArithmeticOperation.Add,
            ArithmeticQuizMode.Essay, AppLanguage.Vietnamese, 31415);
        string numericEquation = $"{numeric.Expression.LeftOperand} + " +
                                 $"{numeric.Expression.RightOperand} = {numeric.CorrectAnswer}";
        var numericParts = EssayCombinedInputParser.Split(
            numericEquation, requiresSolution: false);
        Require(numericParts.Solution.Length == 0 &&
                numericParts.Equation == numericEquation &&
                validator.Validate(numeric, numericParts.Solution,
                    numericParts.Equation, numeric.CorrectAnswer.ToString()).IsCorrect,
            "Numeric-only questions should still grade the calculation directly.");
    }

    private static void CheckUnifiedEssaySubmission()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            ArithmeticQuizQuestion algorithm = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, language, 19000 + count);
            string algorithmUnit = EssayAnswerValidator.GetExpectedUnit(algorithm);
            string sentence = EssayAnswerValidator.RequiresSolution(algorithm)
                ? BuildSolutionSentence(algorithm, language) : string.Empty;
            string value = algorithm.ExactAnswer.ToString();
            string label = language == AppLanguage.Vietnamese ? "Đáp số" : "Answer";
            ArithmeticQuizQuestion contextual = algorithm with
            {
                WordProblem = new MathWordProblem("", sentence,
                    algorithmUnit.Length > 0 ? algorithmUnit :
                        language == AppLanguage.Vietnamese ? "quả trứng" : "eggs", "")
            };

            foreach (ArithmeticQuizQuestion question in new[] { algorithm, contextual })
            {
                string unit = EssayAnswerValidator.GetExpectedUnit(question);
                string solution = sentence.Length > 0 ? sentence :
                    language == AppLanguage.Vietnamese ? $"Số {unit} cần tìm là:" : $"The number of {unit} is:";
                string equation = BuildEquation(question) + (unit.Length > 0 ? " " + unit : "");
                string answer = value + (unit.Length > 0 ? " " + unit : "");
                bool requiresSolution = EssayAnswerValidator.RequiresSolution(question);
                bool multiStep = question.AverageProblem?.Type == AverageQuizType.IndirectData;
                string work = (requiresSolution ? solution + "\r\n" : "") + equation;

                foreach (string submission in new[]
                         { $"{work}\r\n{label}: {answer}", $"{work}\n{answer}" })
                {
                    var parts = EssayCombinedInputParser.Parse(submission, requiresSolution, multiStep);
                    var result = validator.Validate(question, parts.Solution, parts.Equation, parts.Answer);
                    Require(parts.Equation == equation && parts.Answer == answer && result.IsCorrect,
                        $"Unified essay rejected {kind}/{subtype}/{language}: " +
                        $"{result.SolutionError}/{result.EquationError}/{result.AnswerError}");
                    count++;
                }

                var missing = EssayCombinedInputParser.Parse(work, requiresSolution, multiStep);
                Require(missing.Answer.Length == 0 && !validator.Validate(question,
                        missing.Solution, missing.Equation, missing.Answer).AnswerIsCorrect,
                    "A missing final answer was silently inferred from the equation.");

                var wrong = EssayCombinedInputParser.Parse($"{work}\n{label}: 999999999 {unit}",
                    requiresSolution, multiStep);
                Require(validator.Validate(question, wrong.Solution, wrong.Equation, wrong.Answer)
                        .AnswerError == EssayAnswerError.WrongAnswer,
                    "An explicit incorrect answer was replaced with the calculation result.");
            }
        }

        var numeric = new ArithmeticQuizQuestion(
            new IntegerArithmeticExpression(12, ArithmeticOperation.Divide, 3),
            ArithmeticQuizMode.Essay, 4, null, null, []);
        foreach (string label in new[] { "Đáp số", "ĐÁP ÁN", "Dap so", "Answer", "Final answer" })
        {
            var parts = EssayCombinedInputParser.Parse($"12 : 3 = 4\n{label}: 4", false);
            Require(validator.Validate(numeric, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                "Answer labels or division by colon were parsed incorrectly.");
        }
        var duplicate = EssayCombinedInputParser.Parse("12 : 3 = 4\nAnswer: 99\nAnswer: 4", false);
        Require(!validator.Validate(numeric, duplicate.Solution, duplicate.Equation, duplicate.Answer).IsCorrect,
            "Conflicting final answers were silently discarded.");
        var empty = EssayCombinedInputParser.Parse(null, true);
        foreach (string submission in new[]
                 { "Ta có:\n12 : 3 = 4\nĐáp số: 4", "Ta có: 12 : 3 = 4\nĐáp số: 4" })
        {
            var parts = EssayCombinedInputParser.Parse(submission, false);
            Require(validator.Validate(numeric, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                "An optional solution sentence should be accepted for numeric questions.");
        }
        Require(empty == (string.Empty, string.Empty, string.Empty), "Blank input should stay blank.");

        var averageQuestion = new AverageQuizGenerator(new Random(91)).GenerateAlgorithm(
            ArithmeticQuizMode.Essay, AverageQuizType.Direct, AppLanguage.Vietnamese);
        AverageQuizContract average = averageQuestion.AverageProblem!;
        string lead = $"Số {average.AnswerUnit} trung bình là:";
        string calculation = $"{average.EquationText} {average.AnswerUnit}";
        string finalAnswer = $"{average.CorrectAnswer} {average.AnswerUnit}";
        var inline = EssayCombinedInputParser.Parse(
            $"{lead} {calculation} Đáp số: {finalAnswer}".Normalize(System.Text.NormalizationForm.FormD), true);
        Require(validator.Validate(averageQuestion, inline.Solution, inline.Equation, inline.Answer).IsCorrect,
            "A one-line submission or decomposed Vietnamese text was rejected.");
        foreach (string submission in new[]
                 { $"{lead}\n{average.EquationText}\nĐáp số: {finalAnswer}",
                   $"{lead}\n{calculation}\nĐáp số: {average.CorrectAnswer}" })
        {
            var parts = EssayCombinedInputParser.Parse(submission, true);
            var result = validator.Validate(averageQuestion, parts.Solution, parts.Equation, parts.Answer);
            Require(result.EquationError == EssayAnswerError.WrongEquationUnit ||
                    result.AnswerError == EssayAnswerError.WrongAnswerUnit,
                "Unifying the editor removed separate calculation and answer unit checks.");
        }
        foreach (string submission in new[]
                 { $"{lead}\nĐáp số: {finalAnswer}", $"{calculation}\nĐáp số: {finalAnswer}" })
        {
            var parts = EssayCombinedInputParser.Parse(submission, true);
            var result = validator.Validate(averageQuestion, parts.Solution, parts.Equation, parts.Answer);
            Require(result.AnswerIsCorrect && !result.IsCorrect,
                "A labeled answer must not substitute for a missing solution or calculation.");
        }
        Console.WriteLine($"  Checked {count} unified submissions across languages and question sources.");
    }
}
