using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckFlexibleEssayCalculations()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;

        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, language, 15000 + count);
            string generated = BuildEquation(question);
            int lastEquals = generated.LastIndexOf('=');
            string generatedCalculation = generated[..lastEquals].Trim();
            int previousEquals = generatedCalculation.LastIndexOf('=');
            if (previousEquals >= 0)
                generatedCalculation = generatedCalculation[(previousEquals + 1)..].Trim();

            string result = question.FractionProblem?.CorrectAnswer.ToString() ??
                            question.CorrectAnswer.ToString();
            string unit = EssayAnswerValidator.GetExpectedUnit(question);
            string suffix = unit.Length == 0 ? string.Empty : unit == "%" ? "%" : " " + unit;
            string alternative = $"({generatedCalculation}) + 0 = {result}{suffix}";

            EssayAnswerValidationResult accepted = validator.Validate(
                question, null, alternative, result + suffix);
            Require(accepted.EquationIsCorrect,
                $"{kind}/{subtype}/{language}: an equivalent calculation was rejected: " +
                $"{accepted.EquationError}; {alternative}");

            string wrongResult = question.FractionProblem is null
                ? (question.CorrectAnswer + 1).ToString()
                : "999999/1";
            EssayAnswerValidationResult rejected = validator.Validate(
                question, null, $"1 + 0 = {wrongResult}{suffix}", result + suffix);
            Require(!rejected.EquationIsCorrect &&
                    rejected.EquationError == EssayAnswerError.WrongEquationResult,
                $"{kind}/{subtype}/{language}: an unrelated result was accepted.");
            count++;
        }

        foreach (ArithmeticOperation operation in new[]
                 { ArithmeticOperation.Add, ArithmeticOperation.Multiply })
        {
            ArithmeticQuizQuestion question = Generate(
                QuizProblemKind.Arithmetic, operation, ArithmeticQuizMode.Essay,
                AppLanguage.Vietnamese, 16000 + (int)operation);
            IntegerArithmeticExpression expression = question.Expression;
            string equation = $"{expression.RightOperand} " +
                              $"{BasicArithmeticEngine.GetSymbol(operation)} " +
                              $"{expression.LeftOperand} = {question.CorrectAnswer}";
            Require(validator.Validate(question, null, equation,
                    question.CorrectAnswer.ToString()).EquationIsCorrect,
                $"Commutative {operation} calculation was rejected: {equation}");
        }

        Console.WriteLine($"  Checked {count} equivalent calculations plus commutative addition/multiplication.");
    }
}
