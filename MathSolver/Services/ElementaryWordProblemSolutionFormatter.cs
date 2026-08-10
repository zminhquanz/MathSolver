using MathSolver.Models;
using System.Globalization;

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
