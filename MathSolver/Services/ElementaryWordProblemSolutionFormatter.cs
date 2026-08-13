using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

public static class ElementaryWordProblemSolutionFormatter
{
    public static string Format(
        ArithmeticQuizQuestion question,
        AppLanguage language,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(culture);

        MathWordProblem wordProblem =
            question.WordProblem ??
            throw new ArgumentException(
                "The question does not contain a word problem.",
                nameof(question));

        if (question.GeometryProblem is GeometryQuizContract geometry)
        {
            return FormatGeometry(
                geometry,
                wordProblem,
                language,
                culture);
        }

        if (question.FindXProblem is FindXQuizContract findX)
        {
            return FormatFindX(
                findX,
                wordProblem,
                language,
                culture);
        }

        string left =
            question.Expression.LeftOperand.ToString(
                "N0",
                culture);

        string right =
            question.Expression.RightOperand.ToString(
                "N0",
                culture);

        string answer =
            question.CorrectAnswer.ToString(
                "N0",
                culture);

        string symbol =
            BasicArithmeticEngine.GetSymbol(
                question.Expression.Operation);

        string answerLabel =
            language == AppLanguage.Vietnamese
                ? "Đáp số"
                : "Answer";

        string solutionLead =
            NormalizeSolutionLeadPunctuation(
                wordProblem.SolutionLead);

        return
            $"{solutionLead}{Environment.NewLine}" +
            $"{left} {symbol} {right} = {answer}{Environment.NewLine}" +
            $"{answerLabel}: {answer} {wordProblem.AnswerUnit}";
    }

    private static string FormatFindX(
        FindXQuizContract findX,
        MathWordProblem wordProblem,
        AppLanguage language,
        CultureInfo culture)
    {
        string left =
            findX.SolutionExpression.LeftOperand.ToString(
                "N0",
                culture);
        string right =
            findX.SolutionExpression.RightOperand.ToString(
                "N0",
                culture);
        string answer =
            findX.CorrectAnswer.ToString(
                "N0",
                culture);
        string symbol =
            BasicArithmeticEngine.GetSymbol(
                findX.SolutionExpression.Operation);
        string answerLabel =
            language == AppLanguage.Vietnamese
                ? "Đáp số"
                : "Answer";
        string solutionLead =
            NormalizeSolutionLeadPunctuation(
                wordProblem.SolutionLead);

        return
            $"{solutionLead}{Environment.NewLine}" +
            $"{findX.EquationText}{Environment.NewLine}" +
            $"x = {left} {symbol} {right}{Environment.NewLine}" +
            $"x = {answer}{Environment.NewLine}" +
            $"{answerLabel}: {answer} {wordProblem.AnswerUnit}";
    }

    private static string FormatGeometry(
        GeometryQuizContract geometry,
        MathWordProblem wordProblem,
        AppLanguage language,
        CultureInfo culture)
    {
        string answer =
            geometry.CorrectAnswer.ToString("N0", culture);

        string substitution = geometry.SubstitutionExpression;

        foreach (BigInteger dimension in geometry.Dimensions.Values
                     .Distinct()
                     .OrderByDescending(value =>
                         value.ToString(CultureInfo.InvariantCulture).Length))
        {
            substitution = substitution.Replace(
                dimension.ToString(CultureInfo.InvariantCulture),
                dimension.ToString("N0", culture),
                StringComparison.Ordinal);
        }

        string answerLabel =
            language == AppLanguage.Vietnamese
                ? "Đáp số"
                : "Answer";

        string solutionLead =
            NormalizeSolutionLeadPunctuation(
                wordProblem.SolutionLead);

        return
            $"{solutionLead}{Environment.NewLine}" +
            $"{geometry.Formula}{Environment.NewLine}" +
            $"{substitution} = {answer}{Environment.NewLine}" +
            $"{answerLabel}: {answer} {geometry.AnswerUnit}";
    }

    internal static string NormalizeSolutionLeadPunctuation(
        string solutionLead)
    {
        string text =
            solutionLead.TrimEnd();

        while (text.Length > 0 &&
               ".,!?:;…".Contains(text[^1]))
        {
            text =
                text[..^1].TrimEnd();
        }

        return text.Length == 0
            ? string.Empty
            : $"{text}:";
    }
}
