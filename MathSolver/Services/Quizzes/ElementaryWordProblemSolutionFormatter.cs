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

        if (question.FractionProblem is FractionQuizContract fraction)
        {
            return FormatFraction(
                fraction,
                wordProblem,
                language);
        }

        if (question.ProportionProblem is ProportionQuizContract proportion)
        {
            return FormatProportion(
                proportion,
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

    private static string FormatProportion(
        ProportionQuizContract contract,
        MathWordProblem wordProblem,
        AppLanguage language,
        CultureInfo culture)
    {
        string lead = NormalizeSolutionLeadPunctuation(wordProblem.SolutionLead);
        string answer = contract.CorrectAnswer.ToString("N0", culture);
        string answerLabel = language == AppLanguage.Vietnamese ? "Đáp số" : "Answer";

        if (contract.IsDirect)
        {
            int unitRate = contract.B / contract.A;
            string step1 = language == AppLanguage.Vietnamese
                ? $"Giá trị ứng với 1 đơn vị: {contract.B.ToString("N0", culture)} ÷ {contract.A.ToString("N0", culture)} = {unitRate.ToString("N0", culture)}"
                : $"Value for 1 unit: {contract.B.ToString("N0", culture)} ÷ {contract.A.ToString("N0", culture)} = {unitRate.ToString("N0", culture)}";
            string step2 = $"{unitRate.ToString("N0", culture)} × {contract.C.ToString("N0", culture)} = {answer}";
            return $"{lead}{Environment.NewLine}{step1}{Environment.NewLine}{step2}{Environment.NewLine}{answerLabel}: {answer} {wordProblem.AnswerUnit}";
        }

        int total = contract.A * contract.B;
        if (contract.AsksForAdditionalPeople)
        {
            int newPeople = total / contract.C;
            string step1 = $"{contract.A.ToString("N0", culture)} × {contract.B.ToString("N0", culture)} = {total.ToString("N0", culture)}";
            string step2 = $"{total.ToString("N0", culture)} ÷ {contract.C.ToString("N0", culture)} = {newPeople.ToString("N0", culture)}";
            string step3 = $"{newPeople.ToString("N0", culture)} − {contract.A.ToString("N0", culture)} = {answer}";
            return $"{lead}{Environment.NewLine}{step1}{Environment.NewLine}{step2}{Environment.NewLine}{step3}{Environment.NewLine}{answerLabel}: {answer} {wordProblem.AnswerUnit}";
        }

        string inverseStep1 = $"{contract.A.ToString("N0", culture)} × {contract.B.ToString("N0", culture)} = {total.ToString("N0", culture)}";
        string inverseStep2 = $"{total.ToString("N0", culture)} ÷ {contract.C.ToString("N0", culture)} = {answer}";
        return $"{lead}{Environment.NewLine}{inverseStep1}{Environment.NewLine}{inverseStep2}{Environment.NewLine}{answerLabel}: {answer} {wordProblem.AnswerUnit}";
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

    private static string FormatFraction(
        FractionQuizContract fraction,
        MathWordProblem wordProblem,
        AppLanguage language)
    {
        string answerLabel =
            language == AppLanguage.Vietnamese
                ? "Đáp số"
                : "Answer";
        string solutionLead =
            NormalizeSolutionLeadPunctuation(
                wordProblem.SolutionLead);

        return
            $"{solutionLead}{Environment.NewLine}" +
            $"{fraction.ExpressionText} = {fraction.CorrectAnswer}{Environment.NewLine}" +
            $"{answerLabel}: {fraction.CorrectAnswer} {wordProblem.AnswerUnit}";
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
