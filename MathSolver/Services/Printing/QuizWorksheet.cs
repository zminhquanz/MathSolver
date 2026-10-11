using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MathSolver.Services.Printing;

public sealed record QuizPrintItem(ArithmeticQuizQuestion Source, string Prompt, IReadOnlyList<string> Options,
    string Answer, string Solution, QuizVisualData? Visual, QuizDiagram? Diagram, QuestionFactTable? Table, string Identity);

/// <summary>A frozen set; PDF exports never generate new values or reorder choices.</summary>
public sealed record QuizWorksheet(string Id, string Title, string Selection, AppLanguage Language,
    CurriculumTier Tier, ArithmeticQuizMode Mode, IReadOnlyList<QuizPrintItem> Questions);

public static class QuizWorksheetBuilder
{
    public const int MinimumCount = 2;
    public const int MaximumCount = 100;
    public static void ValidateCount(int count)
    {
        if (count is < MinimumCount or > MaximumCount) throw new ArgumentOutOfRangeException(nameof(count));
    }

    public static async Task<QuizWorksheet> CreateAsync(int count, string title, string selection, AppLanguage language,
        CurriculumTier tier, ArithmeticQuizMode mode, Func<CancellationToken, Task<ArithmeticQuizQuestion>> generate,
        IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        ValidateCount(count);
        var items = new List<QuizPrintItem>();
        var identities = new HashSet<string>();
        for (int attempt = 0; items.Count < count && attempt < Math.Max(200, count * 40); attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            var question = await generate(cancellation).ConfigureAwait(false);
            if (question.Mode != mode) throw new InvalidOperationException("WorksheetModeMismatch");
            var item = Format(question, language);
            if (!identities.Add(item.Identity)) continue;
            items.Add(item);
            progress?.Report(items.Count);
        }
        if (items.Count != count) throw new InvalidOperationException("WorksheetNotEnoughDistinctQuestions");
        cancellation.ThrowIfCancellationRequested();
        return new(Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), title.Trim(), selection,
            language, tier, mode, items.AsReadOnly());
    }

    public static QuizPrintItem Format(ArithmeticQuizQuestion q, AppLanguage language)
    {
        bool vi = language == AppLanguage.Vietnamese;
        var culture = CultureInfo.GetCultureInfo(vi ? "vi-VN" : "en-US");
        var e = q.ElementaryProblem;
        string expression = q.ExpressionProblem?.ExpressionText ?? q.FractionProblem?.ExpressionText
            ?? q.FindXProblem?.EquationText ?? $"{q.Expression.LeftOperand} {BasicArithmeticEngine.GetSymbol(q.Expression.Operation)} {q.Expression.RightOperand}";
        string prompt = e?.ProblemText ?? q.WordProblem?.ProblemText
            ?? q.ProportionProblem?.ProblemText ?? q.MotionProblem?.ProblemText ?? q.AverageProblem?.ProblemText
            ?? q.PercentageProblem?.ProblemText ?? expression + (q.FindXProblem is null ? " = ?" : "");
        if (e?.IsNumericDecimalCalculation == true) prompt = e.FormatDecimalCalculation("?");
        string unit = q.WordProblem?.AnswerUnit ?? q.GeometryProblem?.AnswerUnit ?? q.ProportionProblem?.AnswerUnit
            ?? q.MotionProblem?.AnswerUnit ?? q.AverageProblem?.AnswerUnit ?? q.PercentageProblem?.AnswerUnit ?? "";
        string answer = e?.AnswerText ?? (q.ExpressionProblem?.CorrectAnswer.ToString() ?? q.FractionProblem?.CorrectAnswer.ToString()
            ?? q.CorrectAnswer.ToString(CultureInfo.InvariantCulture)) + (unit.Length > 0 ? " " + unit : "");
        IReadOnlyList<string> options = e?.ChoiceTexts ?? q.ExpressionProblem?.Choices.Select(v => v.ToString()).ToArray()
            ?? q.FractionProblem?.Choices.Select(v => v.ToString()).ToArray() ?? q.Choices.Select(v => v.ToString()).ToArray();
        if (e is null && unit.Length > 0) options = options.Select(value => value + " " + unit).ToArray();
        var visual = e?.Visual is { } v ? QuizChartPresentation.ForDisplay(v, false) : null;
        var diagram = QuizDiagramBuilder.Build(q, language, revealSolution: false);
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt + JsonSerializer.Serialize(visual)
            + JsonSerializer.Serialize(diagram) + JsonSerializer.Serialize(q.WordProblem?.FactTable))));
        string solution = e?.SolutionText ?? (q.WordProblem is not null
            ? ElementaryWordProblemSolutionFormatter.Format(q, language, culture)
            : q.ProportionProblem is { } p ? ProportionQuizSolutionFormatter.Format(p, language, culture)
            : q.AverageProblem?.SolutionText ?? q.PercentageProblem?.SolutionText ?? q.MotionProblem?.SolutionText
            ?? (q.FindXProblem is not null ? expression + "\nx = " + q.CorrectAnswer : expression + " = " + answer));
        if (QuizPrintCalculationSolution.AppliesTo(q))
            solution = QuizPrintCalculationSolution.Format(q);
        else if (q.WordProblem is not null && q.FractionProblem is { } fraction)
            // Keep the story's explanation, units and final answer; expand only its calculation.
            solution = solution.Replace($"{fraction.ExpressionText} = {fraction.CorrectAnswer}",
                QuizPrintCalculationSolution.FormatFraction(fraction), StringComparison.Ordinal);
        if (q.Mode == ArithmeticQuizMode.TrueFalse)
        {
            string assertion = e?.PresentedText ?? q.ExpressionProblem?.PresentedAnswer?.ToString()
                ?? q.FractionProblem?.PresentedAnswer?.ToString() ?? q.PresentedAnswer?.ToString() ?? throw new InvalidOperationException("MissingPrintedAssertion");
            if (e?.IsComparison == true) assertion = e.FormatComparison(assertion);
            if (e?.IsNumericDecimalCalculation == true) assertion = e.FormatDecimalCalculation(assertion);
            if (e is null)
                assertion = q.WordProblem is not null || q.GeometryProblem is not null || q.ProportionProblem is not null
                    || q.MotionProblem is not null || q.AverageProblem is not null || q.PercentageProblem is not null
                    ? assertion + (unit.Length > 0 ? " " + unit : "")
                    : q.FindXProblem is not null ? "x = " + assertion : expression + " = " + assertion;
            prompt += "\n" + (vi ? "Nhận định: " : "Statement: ") + assertion;
            options = vi ? ["Đúng", "Sai"] : ["True", "False"];
            answer = q.PresentedEquationIsCorrect == true ? options[0] : options[1];
        }
        else if (q.Mode == ArithmeticQuizMode.MultipleChoice)
        {
            int correct = options.ToList().FindIndex(value => value == answer || value == (e?.AnswerText ?? q.ExactAnswer.ToString()));
            if (correct < 0 && e is null) correct = q.ExpressionProblem?.Choices.ToList().FindIndex(value => value == q.ExpressionProblem.CorrectAnswer)
                ?? q.FractionProblem?.Choices.ToList().FindIndex(value => value == q.FractionProblem.CorrectAnswer)
                ?? q.Choices.ToList().FindIndex(value => value == q.CorrectAnswer);
            if (correct < 0) throw new InvalidOperationException("MissingPrintedCorrectChoice");
            answer = $"{(char)('A' + correct)}. {options[correct]}";
        }
        return new(q, prompt, options, answer, solution, visual, diagram, q.WordProblem?.FactTable, identity);
    }
}
