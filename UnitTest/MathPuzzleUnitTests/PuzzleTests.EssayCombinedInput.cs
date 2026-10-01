using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssayCombinedInput()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        ArithmeticQuizQuestion algorithm = new AverageQuizGenerator(new Random(91))
            .GenerateAlgorithm(ArithmeticQuizMode.Essay,
                AverageQuizType.Direct, AppLanguage.Vietnamese);
        AverageQuizContract average = algorithm.AverageProblem!;
        ArithmeticQuizQuestion ai = algorithm with
        {
            WordProblem = new MathWordProblem(
                average.ProblemText, "Trung bình mỗi ngày là:",
                average.AnswerUnit, average.SubjectName)
        };
        string sentence = $"Số {average.AnswerUnit} trung bình là:";
        string equation = $"{average.EquationText} {average.AnswerUnit}";
        string answer = $"{average.CorrectAnswer} {average.AnswerUnit}";

        foreach (ArithmeticQuizQuestion question in new[] { algorithm, ai })
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
}
