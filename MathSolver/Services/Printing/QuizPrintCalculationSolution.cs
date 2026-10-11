using MathSolver.Models;
using MathSolver.Services.Core;
using System.Numerics;

namespace MathSolver.Services.Printing;

/// <summary>Worked calculations for numeric practice, without a separate answer label.</summary>
internal static class QuizPrintCalculationSolution
{
    internal static bool AppliesTo(ArithmeticQuizQuestion question) => question.WordProblem is null
        && (question.ElementaryProblem is { } elementary
            ? elementary.IsNumericDecimalCalculation
                || (elementary.IsComparison && !elementary.RequiresSolution && elementary.StoryContextId is null)
            : !EssayAnswerValidator.RequiresSolution(question));

    internal static string Format(ArithmeticQuizQuestion question)
    {
        if (question.ElementaryProblem is { } elementary)
            return elementary.IsComparison
                ? elementary.FormatComparison(elementary.Answers[0].Text!)
                : elementary.FormatDecimalCalculation(elementary.AnswerText);

        if (question.FindXProblem is { } findX)
        {
            var inverse = findX.SolutionExpression;
            return $"{findX.EquationText}\nx = {inverse.LeftOperand} {BasicArithmeticEngine.GetSymbol(inverse.Operation)} {inverse.RightOperand}\nx = {findX.CorrectAnswer}";
        }

        if (question.FractionProblem is { } fraction) return FormatFraction(fraction);
        if (question.ExpressionProblem is { } expression)
            return $"{expression.ExpressionText} = {expression.CorrectAnswer}";

        var arithmetic = question.Expression;
        return $"{arithmetic.LeftOperand} {BasicArithmeticEngine.GetSymbol(arithmetic.Operation)} {arithmetic.RightOperand} = {question.CorrectAnswer}";
    }

    internal static string FormatFraction(FractionQuizContract fraction)
    {
        if (fraction.Operation is not (FractionOperation.Add or FractionOperation.Subtract))
            return $"{fraction.ExpressionText} = {fraction.CorrectAnswer}";

        var left = fraction.LeftOperand;
        var right = fraction.RightOperand;
        BigInteger denominator = left.Denominator / BigInteger.GreatestCommonDivisor(left.Denominator, right.Denominator) * right.Denominator;
        BigInteger leftFactor = denominator / left.Denominator, rightFactor = denominator / right.Denominator;
        BigInteger leftNumerator = left.Numerator * leftFactor, rightNumerator = right.Numerator * rightFactor;
        var lines = new List<string>();
        if (leftFactor != BigInteger.One)
            lines.Add($"{left} = ({left.Numerator} × {leftFactor})/({left.Denominator} × {leftFactor}) = {leftNumerator}/{denominator}");
        if (rightFactor != BigInteger.One)
            lines.Add($"{right} = ({right.Numerator} × {rightFactor})/({right.Denominator} × {rightFactor}) = {rightNumerator}/{denominator}");

        var steps = new List<string> { fraction.ExpressionText };
        if (left.Denominator != right.Denominator)
            steps.Add($"{leftNumerator}/{denominator} {fraction.OperationSymbol} {rightNumerator}/{denominator}");
        steps.Add($"({leftNumerator} {fraction.OperationSymbol} {rightNumerator})/{denominator}");
        BigInteger numerator = fraction.Operation == FractionOperation.Add
            ? leftNumerator + rightNumerator : leftNumerator - rightNumerator;
        string unreduced = $"{numerator}/{denominator}";
        steps.Add(unreduced);
        if (unreduced != fraction.CorrectAnswer.ToString()) steps.Add(fraction.CorrectAnswer.ToString());
        lines.Add(string.Join(" = ", steps));
        return string.Join("\n", lines);
    }
}
