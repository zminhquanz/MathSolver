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
                ? vietnamese
                    ? $"Lời giải: em chưa viết câu lời giải; {guidance}."
                    : $"Solution: no solution sentence was entered; {guidance}."
                : vietnamese
                    ? $"Lời giải: câu em viết chưa nêu đúng đại lượng cần tìm; {guidance}."
                    : $"Solution: the sentence does not name the requested quantity; {guidance}.");
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
        string prefix = vi ? $"Bước {step.Number}: " : $"Step {step.Number}: ";
        return prefix + (step.Error switch
        {
            EssayAnswerError.WrongEquationResult when step.ComputedValue != step.WrittenValue => vi
                ? $"vế trái tính được {step.ComputedValue}, nhưng em ghi {step.WrittenValue}."
                : $"the left side evaluates to {step.ComputedValue}, but you wrote {step.WrittenValue}.",
            EssayAnswerError.WrongEquationResult => vi
                ? $"kết quả cuối là {step.ComputedValue}, nhưng bài toán cần {expectedAnswer}."
                : $"the final result is {step.ComputedValue}, but the problem needs {expectedAnswer}.",
            EssayAnswerError.WrongEquationUnit when string.IsNullOrWhiteSpace(step.EnteredUnit) => vi
                ? $"phép tính cuối thiếu đơn vị “{expectedUnit}”."
                : $"the final calculation is missing the unit “{expectedUnit}”.",
            EssayAnswerError.WrongEquationUnit => vi
                ? $"đơn vị “{step.EnteredUnit}” chưa đúng; cần “{expectedUnit}”."
                : $"the unit “{step.EnteredUnit}” is incorrect; use “{expectedUnit}”.",
            EssayAnswerError.WrongOperandsOrOperation => vi
                ? "dữ kiện hoặc quan hệ tính chưa khớp đề bài. Dùng dữ kiện trong đề hoặc kết quả đã tính đúng ở bước trước; phép tính cuối cần tìm trung bình cộng."
                : "the facts or calculation relationships do not match the problem. Use the given facts or a correctly derived earlier result; the final calculation must find the average.",
            _ => vi ? "không đọc được phép tính; hãy ghi đầy đủ biểu thức = kết quả."
                : "the calculation cannot be read; write a complete expression = result."
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
            return vietnamese
                ? $"hãy nêu đối tượng “{subject ?? "cần tính tỉ lệ"}” hoặc tỉ lệ phần trăm (%)"
                : $"name “{subject ?? "the requested share"}” or use percent (%)";
        }

        if (!string.IsNullOrWhiteSpace(subject) &&
            !string.IsNullOrWhiteSpace(expectedUnit))
        {
            return vietnamese
                ? $"hãy nêu “{subject}” hoặc đơn vị “{expectedUnit}”"
                : $"name “{subject}” or its unit “{expectedUnit}”";
        }

        if (!string.IsNullOrWhiteSpace(expectedUnit))
        {
            return vietnamese
                ? $"hãy nêu đơn vị “{expectedUnit}”"
                : $"include the unit “{expectedUnit}”";
        }

        return vietnamese
            ? $"hãy nêu đại lượng “{subject ?? "cần tìm"}”"
            : $"name the requested quantity “{subject ?? "answer"}”";
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
                ? vietnamese
                    ? $"thiếu đơn vị “{expectedUnit}” sau kết quả"
                    : $"the result is missing the unit “{expectedUnit}”"
                : vietnamese
                    ? $"đơn vị em ghi là “{enteredUnit}”, cần “{expectedUnit}”"
                    : $"the unit is “{enteredUnit}”, but “{expectedUnit}” is required";
            return vietnamese ? $"Phép tính: {unitProblem}." : $"Calculation: {unitProblem}.";
        }

        if (error == EssayAnswerError.InvalidEquationFormat)
        {
            string detail = string.IsNullOrWhiteSpace(equationText)
                ? vietnamese ? "chưa nhập phép tính" : "no calculation was entered"
                : vietnamese
                    ? "không đọc được biểu thức hoặc thiếu dấu “=” và kết quả"
                    : "the expression cannot be read, or “=” and a result are missing";
            return vietnamese
                ? $"Phép tính: {detail}. Ví dụ đúng: {sample}."
                : $"Calculation: {detail}. One valid form is {sample}.";
        }

        if (error == EssayAnswerError.WrongOperandsOrOperation)
        {
            return vietnamese
                ? $"Phép tính: dữ kiện hoặc phép toán chưa khớp đề bài. Một cách tính đúng: {sample}."
                : $"Calculation: the numbers or operation do not match the problem. One valid calculation is {sample}.";
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
                return vietnamese
                    ? $"Phép tính: vế trái tính được {computed}, nhưng em ghi {enteredResult}; bài toán cần {expectedAnswer}.{unitNote}"
                    : $"Calculation: the left side evaluates to {computed}, but you wrote {enteredResult}; this problem needs {expectedAnswer}.{unitNote}";
            }

            return vietnamese
                ? $"Phép tính: cách tính em viết ra {computed}, nhưng bài toán cần {expectedValue}; hãy kiểm tra dữ kiện hoặc phép toán. Một cách tính đúng: {sample}.{unitNote}"
                : $"Calculation: your expression evaluates to {computed}, but the problem needs {expectedValue}; check the numbers or operation. One valid calculation is {sample}.{unitNote}";
        }

        return vietnamese
            ? $"Phép tính: kết quả chưa đúng; bài toán cần {expectedAnswer}. Một cách tính đúng: {sample}.{unitNote}"
            : $"Calculation: the result is incorrect; the problem needs {expectedAnswer}. One valid calculation is {sample}.{unitNote}";
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
                ? vietnamese
                    ? $"thiếu đơn vị “{expectedUnit}”"
                    : $"the unit “{expectedUnit}” is missing"
                : vietnamese
                    ? $"em ghi đơn vị “{enteredUnit}”, cần “{expectedUnit}”"
                    : $"you wrote the unit “{enteredUnit}”, but “{expectedUnit}” is required";
            return vietnamese ? $"Đáp số: {unitProblem}." : $"Answer: {unitProblem}.";
        }

        if (error == EssayAnswerError.InvalidAnswerFormat)
        {
            string detail = string.IsNullOrWhiteSpace(answerText)
                ? vietnamese ? "chưa nhập đáp số" : "no answer was entered"
                : vietnamese ? "không đọc được giá trị số" : "the numeric value cannot be read";
            return vietnamese
                ? $"Đáp số: {detail}; cần ghi {expectedAnswer}."
                : $"Answer: {detail}; write {expectedAnswer}.";
        }

        if (hasValue)
        {
            string unitNote = GetAdditionalUnitIssue(
                question, enteredUnit, expectedUnit, vietnamese);
            return vietnamese
                ? $"Đáp số: em ghi {enteredValue}, kết quả đúng là {expectedAnswer}.{unitNote}"
                : $"Answer: you wrote {enteredValue}, but the correct answer is {expectedAnswer}.{unitNote}";
        }

        return vietnamese
            ? $"Đáp số: chưa đúng; kết quả đúng là {expectedAnswer}."
            : $"Answer: incorrect; the correct answer is {expectedAnswer}.";
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
            return vietnamese
                ? $" Đồng thời thiếu đơn vị “{expectedUnit}”."
                : $" The unit “{expectedUnit}” is also missing.";
        }

        return vietnamese
            ? $" Đồng thời đơn vị “{enteredUnit}” chưa đúng; cần “{expectedUnit}”."
            : $" The unit “{enteredUnit}” is also incorrect; use “{expectedUnit}”.";
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
