using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssayCalculationUnits()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;

        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            ArithmeticQuizQuestion algorithm = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, language, 13000 + count);
            CheckEquationUnit(validator, algorithm,
                $"Algorithm/{kind}/{subtype}/{language}");

            string aiUnit = EssayAnswerValidator.GetExpectedUnit(algorithm);
            if (aiUnit.Length == 0)
                aiUnit = language == AppLanguage.Vietnamese ? "cây bút" : "pens";
            ArithmeticQuizQuestion ai = algorithm with
            {
                WordProblem = new MathWordProblem(
                    "A word problem with the same contract.",
                    "The requested amount is:", aiUnit, "The problem")
            };
            CheckEquationUnit(validator, ai,
                $"AI/{kind}/{subtype}/{language}");
            if (kind is QuizProblemKind.Arithmetic or QuizProblemKind.Fraction or
                QuizProblemKind.FindX or QuizProblemKind.Geometry or QuizProblemKind.Proportion)
            {
                string formatted = ElementaryWordProblemSolutionFormatter.Format(
                    ai, language, CultureInfo.InvariantCulture);
                string formattedValue = ai.FractionProblem?.CorrectAnswer.ToString() ??
                                        ai.CorrectAnswer.ToString("N0", CultureInfo.InvariantCulture);
                Require(formatted.Contains($"= {formattedValue} {aiUnit}", StringComparison.Ordinal),
                    $"AI/{kind}/{subtype}/{language}: worked solution omits calculation unit.");
            }
            count += 2;
        }

        ArithmeticQuizQuestion vietnameseArithmetic = Generate(
            QuizProblemKind.Arithmetic, ArithmeticOperation.Add,
            ArithmeticQuizMode.Essay, AppLanguage.Vietnamese, 14000)
            with { WordProblem = new MathWordProblem(
                "Lan có cây bút.", "Số cây bút là:", "cây bút", "Lan") };
        string equivalentEquation = BuildEquation(vietnameseArithmetic) + " cái bút";
        string equivalentAnswer = vietnameseArithmetic.CorrectAnswer + " cái bút";
        EssayAnswerValidationResult equivalent = validator.Validate(
            vietnameseArithmetic, null, equivalentEquation, equivalentAnswer);
        Require(equivalent.EquationIsCorrect && equivalent.AnswerIsCorrect,
            "Calculation and answer must accept the same Vietnamese unit equivalence.");

        CheckMetricUnitAbbreviations(validator);

        Console.WriteLine($"  Checked {count} algorithm/AI equations with correct, missing, and wrong units.");
    }

    private static void CheckMetricUnitAbbreviations(
        EssayAnswerValidator validator)
    {
        ArithmeticQuizQuestion sample = Generate(
            QuizProblemKind.Proportion, ProportionQuizType.Direct,
            ArithmeticQuizMode.Essay, AppLanguage.Vietnamese, 14001);

        foreach ((string writtenUnit, string symbol, string wrongSymbol) in new[]
        {
            ("mét vải", "m", "m²"),
            ("gam", "g", "kg"),
            ("kilomet", "km", "m"),
            ("kilogam", "kg", "g")
        })
        {
            ArithmeticQuizQuestion question = sample with
            {
                ProportionProblem = sample.ProportionProblem! with
                {
                    AnswerUnit = writtenUnit
                }
            };
            string equation = BuildEquation(question);
            string answer = question.CorrectAnswer.ToString();
            EssayAnswerValidationResult accepted = validator.Validate(
                question, $"Số {symbol} là:", equation + symbol,
                answer + symbol);
            Require(accepted.IsCorrect,
                $"{writtenUnit} must accept adjacent {symbol} in solution, equation and answer: " +
                $"{accepted.SolutionError}/{accepted.EquationError}/{accepted.AnswerError}.");

            EssayAnswerValidationResult wrongEquation = validator.Validate(
                question, $"Số {symbol} là:", equation + wrongSymbol,
                answer + symbol);
            Require(wrongEquation.EquationError == EssayAnswerError.WrongEquationUnit,
                $"{writtenUnit} incorrectly accepted {wrongSymbol} in the equation.");

            EssayAnswerValidationResult wrongAnswer = validator.Validate(
                question, $"Số {symbol} là:", equation + symbol,
                answer + wrongSymbol);
            Require(wrongAnswer.AnswerError == EssayAnswerError.WrongAnswerUnit,
                $"{writtenUnit} incorrectly accepted {wrongSymbol} in the answer.");
        }
    }

    private static void CheckEquationUnit(
        EssayAnswerValidator validator,
        ArithmeticQuizQuestion question,
        string label)
    {
        string equation = BuildEquation(question);
        string unit = EssayAnswerValidator.GetExpectedUnit(question);
        string answerValue = question.FractionProblem?.CorrectAnswer.ToString() ??
                             question.CorrectAnswer.ToString();
        string suffix = unit == "%" ? "%" : " " + unit;
        string answer = answerValue + suffix;

        EssayAnswerValidationResult withUnit = validator.Validate(
            question, null, equation + suffix, answer);
        Require(withUnit.EquationIsCorrect && withUnit.AnswerIsCorrect,
            $"{label}: correct calculation and answer unit rejected: " +
            $"{withUnit.EquationError}/{withUnit.AnswerError}; {equation + suffix}");

        EssayAnswerValidationResult withoutUnit = validator.Validate(
            question, null, equation, answer);
        if (unit.Length == 0)
        {
            Require(withoutUnit.EquationIsCorrect,
                $"{label}: unit-free basic calculation should be accepted.");
            return;
        }

        Require(!withoutUnit.EquationIsCorrect &&
                withoutUnit.EquationError == EssayAnswerError.WrongEquationUnit &&
                withoutUnit.AnswerIsCorrect,
            $"{label}: missing calculation unit was not reported separately.");

        EssayAnswerValidationResult wrongEquationUnit = validator.Validate(
            question, null, equation + " wrongunitxyz", answer);
        Require(!wrongEquationUnit.EquationIsCorrect &&
                wrongEquationUnit.EquationError == EssayAnswerError.WrongEquationUnit &&
                wrongEquationUnit.AnswerIsCorrect,
            $"{label}: wrong calculation unit was accepted.");

        EssayAnswerValidationResult wrongAnswerUnit = validator.Validate(
            question, null, equation + suffix, answerValue + " wrongunitxyz");
        Require(wrongAnswerUnit.EquationIsCorrect &&
                !wrongAnswerUnit.AnswerIsCorrect &&
                wrongAnswerUnit.AnswerError == EssayAnswerError.WrongAnswerUnit,
            $"{label}: calculation and answer units were not checked independently.");
    }

    private static string BuildEquation(ArithmeticQuizQuestion question)
    {
        if (question.FractionProblem is FractionQuizContract fraction)
            return $"{fraction.ExpressionText} = {fraction.CorrectAnswer}";
        if (question.GeometryProblem is GeometryQuizContract geometry)
            return geometry.EquationText;
        if (question.ProportionProblem is ProportionQuizContract proportion)
        {
            string answer = proportion.CorrectAnswer.ToString();
            if (proportion.IsDirect)
                return $"{proportion.B} ÷ {proportion.A} × {proportion.C} = {answer}";
            return proportion.AsksForAdditionalPeople
                ? $"{proportion.A} × {proportion.B} ÷ {proportion.C} − {proportion.A} = {answer}"
                : $"{proportion.A} × {proportion.B} ÷ {proportion.C} = {answer}";
        }
        if (question.MotionProblem is MotionQuizContract motion)
            return motion.EquationText;
        if (question.AverageProblem is AverageQuizContract average)
            return average.EquationText;
        if (question.PercentageProblem is PercentageQuizContract percentage)
            return percentage.EquationText;

        IntegerArithmeticExpression expression = question.Expression;
        return $"{expression.LeftOperand} " +
               $"{BasicArithmeticEngine.GetSymbol(expression.Operation)} " +
               $"{expression.RightOperand} = {question.CorrectAnswer}";
    }
}
