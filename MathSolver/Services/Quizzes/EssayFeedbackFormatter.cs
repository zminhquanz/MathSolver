using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>
/// Explains every failed essay field using the validator's result, without
/// changing which answers the validator accepts.
/// </summary>
public static class EssayFeedbackFormatter
{
    public static string Format(
        ArithmeticQuizQuestion question,
        EssayAnswerValidationResult validation,
        string? solutionText,
        string? equationText,
        string? answerText,
        AppLanguage language,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(culture);

        if (validation.IsCorrect)
        {
            return string.Empty;
        }

        if (validation.Details.Count > 0) return string.Join(Environment.NewLine, validation.Details);

        bool vietnamese = language == AppLanguage.Vietnamese;
        string expectedUnit = EssayAnswerValidator.GetExpectedUnit(question);
        string expectedAnswer = GetExpectedAnswer(question, expectedUnit, culture);
        var messages = new List<string>(3);

        if (!validation.SolutionIsCorrect)
        {
            string guidance = GetSolutionGuidance(question, expectedUnit, vietnamese);
            bool missingSolution = validation.SolutionError == EssayAnswerError.MissingSolution ||
                                   string.IsNullOrWhiteSpace(solutionText);
            messages.Add(missingSolution
                ? QuizContentCatalog.Text(language, "EssayFeedbackFormatter.Format.001", ("guidance", $"{guidance}"))
                : QuizContentCatalog.Text(language, "EssayFeedbackFormatter.Format.002", ("guidance", $"{guidance}")));
        }

        if (!validation.EquationIsCorrect)
        {
            if (validation.Steps.Count > 0)
                messages.AddRange(validation.Steps.Where(step => !step.IsCorrect)
                    .Select(step => DescribeStep(step, expectedUnit, expectedAnswer, vietnamese)));
            else messages.Add(DescribeEquation(
                question, validation.EquationError, equationText,
                expectedUnit, expectedAnswer, vietnamese, culture));
        }

        if (!validation.AnswerIsCorrect)
        {
            messages.Add(DescribeAnswer(
                question, validation.AnswerError, answerText, expectedUnit,
                expectedAnswer, vietnamese));
        }

        return string.Join(Environment.NewLine, messages);
    }

    private static string DescribeStep(EssayStepValidationResult step, string expectedUnit,
        string expectedAnswer, bool vi)
    {
        string prefix = QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.003", ("step_Number", $"{step.Number}"));
        return prefix + (step.Error switch
        {
            EssayAnswerError.WrongEquationResult when step.ComputedValue != step.WrittenValue => QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.004", ("step_ComputedValue", $"{step.ComputedValue}"), ("step_WrittenValue", $"{step.WrittenValue}")),
            EssayAnswerError.WrongEquationResult => QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.005", ("step_ComputedValue", $"{step.ComputedValue}"), ("expectedAnswer", $"{expectedAnswer}")),
            EssayAnswerError.WrongEquationUnit when string.IsNullOrWhiteSpace(step.EnteredUnit) => QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.006", ("expectedUnit", $"{expectedUnit}")),
            EssayAnswerError.WrongEquationUnit => QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.007", ("step_EnteredUnit", $"{step.EnteredUnit}"), ("expectedUnit", $"{expectedUnit}")),
            EssayAnswerError.WrongOperandsOrOperation => QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.008"),
            _ => QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vi), "EssayFeedbackFormatter.DescribeStep.009")
        });
    }

    private static string GetSolutionGuidance(
        ArithmeticQuizQuestion question,
        string expectedUnit,
        bool vietnamese)
    {
        string? subject = question.ProportionProblem?.SubjectName ??
                          question.MotionProblem?.SubjectName ??
                          question.AverageProblem?.SubjectName ??
                          question.PercentageProblem?.SubjectName ??
                          question.GeometryProblem?.ObjectName ??
                          question.WordProblem?.SubjectName;

        if (expectedUnit == "%")
        {
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.GetSolutionGuidance.032", ("subject_c_n_t_nh_t_l", $"{subject ?? "cần tính tỉ lệ"}"), ("subject_the_requested_share", $"{subject ?? "the requested share"}"));
        }

        if (!string.IsNullOrWhiteSpace(subject) &&
            !string.IsNullOrWhiteSpace(expectedUnit))
        {
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.GetSolutionGuidance.010", ("subject", $"{subject}"), ("expectedUnit", $"{expectedUnit}"));
        }

        if (!string.IsNullOrWhiteSpace(expectedUnit))
        {
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.GetSolutionGuidance.011", ("expectedUnit", $"{expectedUnit}"));
        }

        return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.GetSolutionGuidance.033", ("subject_c_n_t_m", $"{subject ?? "cần tìm"}"), ("subject_answer", $"{subject ?? "answer"}"));
    }

    private static string DescribeEquation(
        ArithmeticQuizQuestion question,
        EssayAnswerError error,
        string? equationText,
        string expectedUnit,
        string expectedAnswer,
        bool vietnamese,
        CultureInfo culture)
    {
        string sample = GetSampleEquation(question) +
                        (expectedUnit == "%" ? "%" :
                            expectedUnit.Length == 0 ? string.Empty : $" {expectedUnit}");
        string? resultText = equationText is null || !equationText.Contains('=')
            ? null
            : equationText[(equationText.LastIndexOf('=') + 1)..];
        bool hasResult = TryReadValueAndUnit(resultText, out string enteredResult,
            out string enteredUnit);

        if (error == EssayAnswerError.WrongEquationUnit)
        {
            string unitProblem = string.IsNullOrWhiteSpace(enteredUnit)
                ? QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.012", ("expectedUnit", $"{expectedUnit}"))
                : QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.013", ("enteredUnit", $"{enteredUnit}"), ("expectedUnit", $"{expectedUnit}"));
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.014", ("unitProblem", $"{unitProblem}"));
        }

        if (error == EssayAnswerError.InvalidEquationFormat)
        {
            string detail = string.IsNullOrWhiteSpace(equationText)
                ? QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.015")
                : QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.016");
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.017", ("detail", $"{detail}"), ("sample", $"{sample}"));
        }

        if (error == EssayAnswerError.WrongOperandsOrOperation)
        {
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.018", ("sample", $"{sample}"));
        }

        string expectedValue = GetExpectedValue(question, culture);
        string unitNote = hasResult
            ? GetAdditionalUnitIssue(question, enteredUnit, expectedUnit, vietnamese)
            : string.Empty;
        string? leftText = equationText is null || !equationText.Contains('=')
            ? null
            : equationText[..equationText.IndexOf('=')].Trim();
        EssayCalculationEvaluator.Value leftValue = default;
        bool hasLeftValue = leftText is not null &&
            EssayCalculationEvaluator.TryEvaluate(leftText,
                out leftValue, out _);

        if (hasLeftValue)
        {
            string computed = FormatValue(leftValue, culture);
            if (hasResult &&
                EssayCalculationEvaluator.TryEvaluate(enteredResult,
                    out EssayCalculationEvaluator.Value writtenValue, out _) &&
                writtenValue != leftValue)
            {
                return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.019", ("computed", $"{computed}"), ("enteredResult", $"{enteredResult}"), ("expectedAnswer", $"{expectedAnswer}"), ("unitNote", $"{unitNote}"));
            }

            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.020", ("computed", $"{computed}"), ("expectedValue", $"{expectedValue}"), ("sample", $"{sample}"), ("unitNote", $"{unitNote}"));
        }

        return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeEquation.021", ("expectedAnswer", $"{expectedAnswer}"), ("sample", $"{sample}"), ("unitNote", $"{unitNote}"));
    }

    private static string DescribeAnswer(
        ArithmeticQuizQuestion question,
        EssayAnswerError error,
        string? answerText,
        string expectedUnit,
        string expectedAnswer,
        bool vietnamese)
    {
        bool hasValue = TryReadValueAndUnit(
            StripAnswerLabel(answerText), out string enteredValue,
            out string enteredUnit);

        if (error == EssayAnswerError.WrongAnswerUnit)
        {
            string unitProblem = string.IsNullOrWhiteSpace(enteredUnit)
                ? QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.022", ("expectedUnit", $"{expectedUnit}"))
                : QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.023", ("enteredUnit", $"{enteredUnit}"), ("expectedUnit", $"{expectedUnit}"));
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.024", ("unitProblem", $"{unitProblem}"));
        }

        if (error == EssayAnswerError.InvalidAnswerFormat)
        {
            string detail = string.IsNullOrWhiteSpace(answerText)
                ? QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.025")
                : QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.026");
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.027", ("detail", $"{detail}"), ("expectedAnswer", $"{expectedAnswer}"));
        }

        if (hasValue)
        {
            string unitNote = GetAdditionalUnitIssue(
                question, enteredUnit, expectedUnit, vietnamese);
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.028", ("enteredValue", $"{enteredValue}"), ("expectedAnswer", $"{expectedAnswer}"), ("unitNote", $"{unitNote}"));
        }

        return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.DescribeAnswer.029", ("expectedAnswer", $"{expectedAnswer}"));
    }

    private static string GetExpectedAnswer(
        ArithmeticQuizQuestion question,
        string unit,
        CultureInfo culture) =>
        GetExpectedValue(question, culture) +
        (unit == "%" ? "%" : unit.Length == 0 ? string.Empty : $" {unit}");

    private static string GetAdditionalUnitIssue(
        ArithmeticQuizQuestion question,
        string enteredUnit,
        string expectedUnit,
        bool vietnamese)
    {
        if (expectedUnit.Length == 0 ||
            EssayAnswerValidator.IsExpectedUnitForFeedback(question, enteredUnit))
        {
            return string.Empty;
        }

        if (enteredUnit.Length == 0)
        {
            return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.GetAdditionalUnitIssue.030", ("expectedUnit", $"{expectedUnit}"));
        }

        return QuizContentCatalog.Text(QuizContentCatalog.CompatibilityLanguage(vietnamese), "EssayFeedbackFormatter.GetAdditionalUnitIssue.031", ("enteredUnit", $"{enteredUnit}"), ("expectedUnit", $"{expectedUnit}"));
    }

    private static string GetExpectedValue(
        ArithmeticQuizQuestion question,
        CultureInfo culture) =>
        question.ExpressionProblem?.CorrectAnswer.ToString() ??
        question.FractionProblem?.CorrectAnswer.ToString() ??
        question.CorrectAnswer.ToString("N0", culture);

    private static string GetSampleEquation(ArithmeticQuizQuestion question)
    {
        if (question.ExpressionProblem is ExpressionQuizContract expressionProblem)
            return $"{expressionProblem.ExpressionText} = {expressionProblem.CorrectAnswer}";
        if (question.FractionProblem is FractionQuizContract fraction)
            return $"{fraction.ExpressionText} = {fraction.CorrectAnswer}";
        if (question.GeometryProblem is GeometryQuizContract geometry)
            return geometry.EquationText;
        if (question.MotionProblem is MotionQuizContract motion)
            return motion.EquationText;
        if (question.AverageProblem is AverageQuizContract average)
            return average.EquationText;
        if (question.PercentageProblem is PercentageQuizContract percentage)
            return percentage.EquationText;
        if (question.ProportionProblem is ProportionQuizContract proportion)
        {
            if (proportion.IsDirect)
                return $"{proportion.B} ÷ {proportion.A} × {proportion.C} = {proportion.CorrectAnswer}";
            string calculation = $"{proportion.A} × {proportion.B} ÷ {proportion.C}";
            if (proportion.AsksForAdditionalPeople)
                calculation += $" − {proportion.A}";
            return $"{calculation} = {proportion.CorrectAnswer}";
        }

        IntegerArithmeticExpression expression = question.FindXProblem?.SolutionExpression ??
                                                 question.Expression;
        string symbol = BasicArithmeticEngine.GetSymbol(expression.Operation);
        return $"{expression.LeftOperand} {symbol} {expression.RightOperand} = {question.CorrectAnswer}";
    }

    private static string FormatValue(
        EssayCalculationEvaluator.Value value,
        CultureInfo culture) =>
        value.Denominator.IsOne
            ? value.Numerator.ToString("N0", culture)
            : $"{value.Numerator}/{value.Denominator}";

    private static string StripAnswerLabel(string? answerText) =>
        Regex.Replace(answerText ?? string.Empty,
            @"^\s*(?:đáp\s*số|answer)\s*:?\s*", string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static bool TryReadValueAndUnit(
        string? text,
        out string value,
        out string unit)
    {
        value = string.Empty;
        unit = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 256)
            return false;

        Match match = Regex.Match(text,
            @"^\s*(?<value>[+-]?\d(?:[\d.,\u00A0\u202F ]*\d)?(?:\s*/\s*[+-]?\d+)?)\s*(?<unit>.*?)\s*[.!]?\s*$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return false;

        value = match.Groups["value"].Value.Trim();
        unit = match.Groups["unit"].Value.Trim().TrimEnd('.', '!');
        return true;
    }
}
