using MathSolver.Models;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using V = MathSolver.Services.EssayCalculationEvaluator.Value;

namespace MathSolver.Services;

/// <summary>Grades chosen steps and independently labeled answers, without prescribing step count.</summary>
internal static partial class ElementaryEssayValidator
{
    internal static EssayAnswerValidationResult Validate(ArithmeticQuizQuestion question,
        string? solution, string? equations, string? answerText)
    {
        var contract = question.ElementaryProblem!;
        if (contract.IsComparison) return ComparisonEssayValidator.Validate(contract, equations, answerText);
        bool vi = contract.Language == AppLanguage.Vietnamese;
        bool preferDecimals = contract.Kind is QuizProblemKind.Decimal or QuizProblemKind.Measurement;
        bool solutionOkay = !contract.RequiresSolution || !string.IsNullOrWhiteSpace(solution) &&
            (contract.Answers.Any(answer => ContainsCue(solution, answer.Label) || answer.Unit.Length > 0 &&
                EssayAnswerValidator.ValidateSolution(question with { WordProblem = new(contract.ProblemText, "", answer.Unit, answer.Label) }, solution).IsCorrect)
             || ContainsCue(solution, QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.001")) || ContainsCue(solution, QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.002")));
        var reports = new List<EssayStepValidationResult>();
        var details = new List<string>();
        var tracker = new StepDerivationTracker(contract.Facts, contract.Constants, preferDecimals);
        // Keep the original facts separate from the growing set of intermediate results.
        // A result can have the same numeric value as a given without replacing its origin.
        var givensTracker = new StepDerivationTracker(contract.Facts, contract.Constants, preferDecimals);
        List<StepDerivationTracker.Origin> ReadOrigins(string expression)
        {
            var original = givensTracker.Read(expression);
            foreach (var origin in tracker.Read(expression))
                if (original.Count < 64 && !original.Any(existing => existing.Equivalent(origin))) original.Add(origin);
            return original;
        }
        var targets = contract.Answers.Select(answer => givensTracker.Read(answer.Expression)).ToArray();
        var inferenceTargets = contract.Reasoning?.Steps.Select(step => givensTracker.Read(step.Expression)).ToArray();
        var knownWork = new Dictionary<string, ElementaryInferenceStep>(StringComparer.Ordinal);
        var knownSteps = contract.Reasoning?.Steps ?? [];
        for (int stepIndex = 0; stepIndex < knownSteps.Count; stepIndex++)
        {
            var step = knownSteps[stepIndex];
            knownWork.TryAdd(NormalizeWorkExpression(step.Expression), step);
            string split = step.Expression;
            foreach (var previous in knownSteps.Take(stepIndex).OrderByDescending(previous => previous.Expression.Length))
                split = split.Replace(previous.Expression, "(" + previous.DisplayValue + ")", StringComparison.Ordinal);
            knownWork.TryAdd(NormalizeWorkExpression(split), step);
        }
        var achieved = new bool[contract.Answers.Count];
        string[] lines = (equations ?? "").Replace("\r", "", StringComparison.Ordinal).Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 64 || (equations?.Length ?? 0) > 32768 || (answerText?.Length ?? 0) > 4096)
            return new(false, false, false, EssayAnswerError.WrongSolutionContent,
                EssayAnswerError.InvalidEquationFormat, EssayAnswerError.InvalidAnswerFormat)
            { Details = [QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.003")] };
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            // A school division-with-remainder is one relation with two results.
            Match division = DivisionRegex().Match(line);
            if (division.Success && (contract.Kind == QuizProblemKind.Remainder || contract.Type == ElementaryQuizType.MixedNumber))
            {
                BigInteger dividend = BigInteger.Parse(division.Groups["a"].Value), divisor = BigInteger.Parse(division.Groups["b"].Value);
                BigInteger quotient = BigInteger.Parse(division.Groups["q"].Value), remainder = BigInteger.Parse(division.Groups["r"].Value);
                bool okay = divisor > 0 && remainder >= 0 && remainder < divisor && dividend == divisor * quotient + remainder
                    && contract.Facts.Contains(dividend.ToString()) && contract.Facts.Contains(divisor.ToString());
                reports.Add(new(index + 1, line, okay, okay ? EssayAnswerError.None : EssayAnswerError.WrongEquationResult,
                    divisor > 0 ? $"{dividend / divisor} ({(QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.004"))} {dividend % divisor})" : (QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.005")),
                    $"{quotient} ({(QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.006"))} {remainder})"));
                if (okay)
                {
                    tracker.Remember(V.Create(quotient, 1), tracker.Read($"({dividend}-{remainder})/{divisor}"));
                    tracker.Remember(V.Create(remainder, 1), tracker.Read($"{dividend}-{quotient}*{divisor}"));
                    for (int target = 0; contract.Type != ElementaryQuizType.MinimumGroups && target < achieved.Length; target++)
                        if (contract.Type == ElementaryQuizType.MixedNumber
                            ? contract.Answers[target].Value == new ReducedFraction(dividend, divisor)
                            : contract.Answers[target].Value == new ReducedFraction(quotient, 1) || contract.Answers[target].Value == new ReducedFraction(remainder, 1))
                        { achieved[target] = true; tracker.Remember(ToValue(contract.Answers[target].Value), targets[target]); }
                }
                continue;
            }
            var parsed = SplitValueUnit(line[(line.LastIndexOf('=') + 1)..]);
            string calculation = line.LastIndexOf('=') < 0 ? line : line[..(line.LastIndexOf('=') + 1)] + parsed.Number;
            string[] members = calculation.Split('=', StringSplitOptions.TrimEntries);
            EssayAnswerError error = EssayAnswerError.None;
            List<StepDerivationTracker.Origin> origins = [];
            V result = default;
            string? computed = null, written = parsed.Number, expectedUnit = null;
            if (members.Length < 2) error = EssayAnswerError.InvalidEquationFormat;
            else
                for (int member = 0; member < members.Length; member++)
                {
                    if (!TryValue(members[member], out V value, preferDecimals)) { error = EssayAnswerError.InvalidEquationFormat; break; }
                    if (member == 0) { result = value; computed = EssayCalculationEvaluator.Format(value); origins = ReadOrigins(members[member]); }
                    else if (value != result) { error = EssayAnswerError.WrongEquationResult; written = members[member]; break; }
                    else if (member < members.Length - 1)
                    {
                        var candidates = ReadOrigins(members[member]);
                        origins = origins.Where(origin => candidates.Any(other => origin.Equivalent(other))).ToList();
                    }
                }
            if (error == EssayAnswerError.None && origins.Count == 0) error = EssayAnswerError.WrongOperandsOrOperation;
            if (error == EssayAnswerError.None)
            {
                tracker.Remember(result, origins);
                bool matched = false;
                bool matchedUnit = false;
                string? candidateUnit = null;
                for (int target = 0; target < achieved.Length; target++)
                {
                    if (contract.Answers[target].IsText || result != ToValue(contract.Answers[target].Value) ||
                        !origins.Any(origin => targets[target].Any(expected => origin.Equivalent(expected)))) continue;
                    matched = true;
                    candidateUnit ??= contract.Answers[target].Unit;
                    // Equal numeric results can represent distinct quantities (e.g.
                    // one full box and one remaining gift). Match the stated dimension
                    // before marking a target; a different candidate must not override it.
                    if (parsed.Unit.Length == 0 && index < lines.Length - 1 || UnitsMatch(question, parsed.Unit, contract.Answers[target].Unit))
                    { achieved[target] = true; matchedUnit = true; }
                }
                if (matched && !matchedUnit)
                {
                    // Unit conversion may preserve the numeric value (e.g. a 1 : 100000 map).
                    // A declared intermediate with another dimension is valid work, but it
                    // must not count as deriving the final quantity in the requested unit.
                    bool declaredIntermediate = knownWork.TryGetValue(NormalizeWorkExpression(members[0]), out var declaredStep)
                        && !contract.Answers.Any(answer => NormalizeWorkExpression(answer.Expression) == NormalizeWorkExpression(declaredStep.Expression))
                        && result == ToValue(declaredStep.Value) && UnitsMatch(question, parsed.Unit, declaredStep.Unit);
                    if (!declaredIntermediate)
                    { error = EssayAnswerError.WrongEquationUnit; expectedUnit = candidateUnit; }
                }
                // Intermediate units can name quantities, people, or equal parts.
                if (!matched && parsed.Unit.Length > 0 && !contract.Answers.Any(answer => UnitsMatch(question, parsed.Unit, answer.Unit))
                    && !(contract.Reasoning?.IntermediateUnits.Any(unit => UnitsMatch(question, parsed.Unit, unit)) == true)
                    && !new[] { "phần", "parts", "bạn", "people", "người", "học sinh", "students" }.Contains(parsed.Unit.ToLowerInvariant()))
                { error = EssayAnswerError.WrongEquationUnit; expectedUnit = string.Join(" / ", contract.Answers.Select(answer => answer.Unit).Where(unit => unit.Length > 0).Distinct()); }
            }
            knownWork.TryGetValue(NormalizeWorkExpression(members.FirstOrDefault() ?? ""), out var canonical);
            if (error == EssayAnswerError.None && canonical is not null && parsed.Unit.Length > 0 &&
                !UnitsMatch(question, parsed.Unit, canonical.Unit))
            { error = EssayAnswerError.WrongEquationUnit; expectedUnit = canonical.Unit; }
            if (error == EssayAnswerError.None && parsed.Unit.Length > 0 && contract.Reasoning is { } reasoning && inferenceTargets is not null)
            {
                var inferredUnits = reasoning.Steps.Where((step, stepIndex) => result == ToValue(step.Value)
                    && origins.Any(origin => inferenceTargets[stepIndex].Any(expected => origin.Equivalent(expected))))
                    .Select(step => step.Unit).Distinct().ToArray();
                if (inferredUnits.Length > 0 && !inferredUnits.Any(unit => UnitsMatch(question, parsed.Unit, unit)))
                { error = EssayAnswerError.WrongEquationUnit; expectedUnit = string.Join(" / ", inferredUnits); }
            }
            reports.Add(new(index + 1, line, error == EssayAnswerError.None, error, computed, written, parsed.Unit, expectedUnit));
        }
        // Once one number is derived, a correct pair determines the second
        // through the given sum/difference/ratio; no extra written step is required.
        bool implicitSecondAnswer = contract.Kind == QuizProblemKind.TwoNumbers && achieved.Any(value => value)
            && CheckAnswers(question, answerText);
        for (int target = 0; target < achieved.Length; target++)
        {
            var expected = contract.Answers[target];
            // Identification and direct readings need only an answer, not invented arithmetic.
            if (!expected.IsText && !achieved[target] && !implicitSecondAnswer && expected.Expression.Any(character => "+-*/".Contains(character)) &&
                !reports.Any(report => !report.IsCorrect))
                details.Add(QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.007", ("expected_Label", $"{expected.Label}")));
        }
        bool equationOkay = reports.All(report => report.IsCorrect) && details.Count == 0;
        EssayAnswerError equationError = reports.FirstOrDefault(report => !report.IsCorrect)?.Error ??
            (equationOkay ? EssayAnswerError.None : EssayAnswerError.WrongOperandsOrOperation);
        bool answerOkay = CheckAnswers(question, answerText, details);
        if (!solutionOkay) details.Insert(0, QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.008"));
        foreach (var step in reports.Where(step => !step.IsCorrect))
            details.Add((QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.009", ("step_Number", $"{step.Number}"))) + (step.Error switch
            {
                EssayAnswerError.WrongEquationResult => QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.010", ("step_ComputedValue", $"{step.ComputedValue}"), ("step_WrittenValue", $"{step.WrittenValue}")),
                EssayAnswerError.WrongEquationUnit => (string.IsNullOrEmpty(step.ExpectedUnit) ? QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.018", ("step_EnteredUnit", $"{step.EnteredUnit}")) : QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.019", ("step_EnteredUnit", $"{step.EnteredUnit}"), ("step_ExpectedUnit", $"{step.ExpectedUnit}"))),
                EssayAnswerError.WrongOperandsOrOperation => QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.011"),
                _ => QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.Validate.012")
            }));
        return new(solutionOkay, equationOkay, answerOkay, solutionOkay ? EssayAnswerError.None : string.IsNullOrWhiteSpace(solution) ? EssayAnswerError.MissingSolution : EssayAnswerError.WrongSolutionContent,
            equationError, answerOkay ? EssayAnswerError.None : EssayAnswerError.WrongAnswer) { Steps = reports, Details = details };
    }

    internal static bool CheckAnswers(ArithmeticQuizQuestion question, string? text, List<string>? details = null)
    {
        var contract = question.ElementaryProblem!; bool vi = contract.Language == AppLanguage.Vietnamese;
        if (contract.Type == ElementaryQuizType.ReadClock)
        {
            if (EssayCombinedInputParser.TryNormalizeClockAnswer(text, out string clockAnswer)) text = clockAnswer;
        }
        var entries = (text ?? "").Split([';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool correct = entries.Length == contract.Answers.Count;
        if (!correct) details?.Add(QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.013", ("contract_Answers_Count", $"{contract.Answers.Count}"), ("entries_Length", $"{entries.Length}")));
        var used = new HashSet<int>();
        for (int index = 0; index < entries.Length; index++)
        {
            string entry = entries[index]; int colon = entry.IndexOf(':'); int target = index;
            if (colon >= 0 && entry[..colon].Any(char.IsLetter))
            {
                string label = entry[..colon].Trim();
                target = Array.FindIndex(contract.Answers.ToArray(), answer => ContainsCue(label, answer.Label)
                    || ContainsCue(answer.Label, label));
                entry = entry[(colon + 1)..].Trim();
            }
            else if (contract.Kind == QuizProblemKind.TwoNumbers)
            {
                var number = SplitValueUnit(entry);
                target = Enumerable.Range(0, contract.Answers.Count).FirstOrDefault(candidate =>
                    !used.Contains(candidate) && TryValue(number.Number, out V value, false)
                    && value == ToValue(contract.Answers[candidate].Value), -1);
            }
            if (target < 0 || target >= contract.Answers.Count || !used.Add(target)) { correct = false; details?.Add(QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.014")); continue; }
            var expected = contract.Answers[target];
            if (expected.IsText)
            {
                bool matches = string.Equals(entry.Trim().TrimEnd('.'), expected.Text, StringComparison.OrdinalIgnoreCase)
                    || expected.Aliases?.Any(alias => string.Equals(entry.Trim().TrimEnd('.'), alias, StringComparison.OrdinalIgnoreCase)) == true
                    || MatchesLineNames(contract, entry, expected.Text!);
                if (!matches) { correct = false; details?.Add(QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.015", ("expected_Label", $"{expected.Label}"), ("entry", $"{entry}"), ("expected_Text", $"{expected.Text}"))); }
                continue;
            }
            var parsed = SplitValueUnit(entry);
            bool valueOkay = TryValue(parsed.Number, out V value, contract.Kind is QuizProblemKind.Decimal or QuizProblemKind.Measurement) && value == ToValue(expected.Value);
            bool formOkay = CorrectForm(parsed.Number, expected);
            bool unitOkay = UnitsMatch(question, parsed.Unit, expected.Unit);
            if (!valueOkay || !formOkay || !unitOkay)
            {
                correct = false;
                details?.Add(!valueOkay ? QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.016", ("expected_Label", $"{expected.Label}"), ("entry", $"{entry}"), ("ElementaryQuizContract_FormatAnswer_expected", $"{ElementaryQuizContract.FormatAnswer(expected)}"))
                    : !formOkay ? (expected.RequireMixedNumber ? QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.020", ("expected_Label", $"{expected.Label}")) : (expected.RequiredDenominator.HasValue ? QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.021", ("expected_Label", $"{expected.Label}"), ("expected_RequiredDenominator", $"{expected.RequiredDenominator}")) : QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.022", ("expected_Label", $"{expected.Label}"))))
                    : QuizContentCatalog.Text(contract.Language, "ElementaryEssayValidator.CheckAnswers.017", ("expected_Label", $"{expected.Label}"), ("expected_Unit", $"{expected.Unit}")));
            }
        }
        return correct;
    }
    private static string NormalizeWorkExpression(string expression)
    {
        string text = Regex.Replace(expression, @"\s", "").Replace("\u00d7", "*", StringComparison.Ordinal)
            .Replace("\u00f7", "/", StringComparison.Ordinal).Replace("\u2212", "-", StringComparison.Ordinal)
            .Replace(',', '.');
        // Parentheses around one numeric intermediate do not change its role.
        while (true)
        {
            string next = Regex.Replace(text, @"\(([+-]?\d+(?:\.\d+)?)\)", "$1");
            if (next == text) return text;
            text = next;
        }
    }
    private static bool CorrectForm(string text, ElementaryAnswer answer)
    {
        Match mixed = MixedRegex().Match(text);
        if (answer.RequireMixedNumber) return mixed.Success && BigInteger.Parse(mixed.Groups["n"].Value) < BigInteger.Parse(mixed.Groups["d"].Value)
            && BigInteger.GreatestCommonDivisor(BigInteger.Parse(mixed.Groups["n"].Value), BigInteger.Parse(mixed.Groups["d"].Value)).IsOne;
        string[] parts = text.Split('/');
        if (answer.RequiredDenominator is int denominator) return parts.Length == 2 && BigInteger.TryParse(parts[1].Trim(), out var d) && d == denominator;
        return !answer.RequireReduced || parts.Length == 1 && answer.Value.Denominator.IsOne && BigInteger.TryParse(text, out _)
            || parts.Length == 2 && BigInteger.TryParse(parts[0].Trim(), out var n)
            && BigInteger.TryParse(parts[1].Trim(), out var den) && BigInteger.GreatestCommonDivisor(n, den).IsOne;
    }
    internal static (string Number, string Unit) SplitValueUnit(string text)
    {
        Match match = ResultRegex().Match(text);
        return match.Success ? (match.Groups["number"].Value.Trim(), match.Groups["unit"].Value.Trim().TrimEnd('.')) : (text.Trim(), "");
    }
    private static bool TryValue(string text, out V value, bool preferDecimals)
    {
        Match mixed = MixedRegex().Match(text);
        if (mixed.Success) text = mixed.Groups["whole"].Value + "+" + mixed.Groups["n"].Value + "/" + mixed.Groups["d"].Value;
        return EssayCalculationEvaluator.TryEvaluate(text, out value, out _, preferDecimals);
    }
    private static V ToValue(ReducedFraction value) => V.Create(value.Numerator, value.Denominator);
    private static bool ContainsCue(string? text, string cue) => (text ?? "").Contains(cue, StringComparison.OrdinalIgnoreCase);
    private static bool UnitsMatch(ArithmeticQuizQuestion question, string entered, string expected) => expected.Length == 0 ? entered.Length == 0
        : EssayAnswerValidator.IsExpectedUnitForFeedback(question with { WordProblem = new(question.ElementaryProblem!.ProblemText, "", expected, "") }, entered);
    [GeneratedRegex(@"^\s*(?<number>[+-]?\d+(?:[.,]\d+)?(?:\s+\d+\s*/\s*\d+|\s*/\s*\d+)?)(?<unit>.*)$")]
    private static partial Regex ResultRegex();
    [GeneratedRegex(@"^\s*(?<whole>\d+)\s+(?<n>\d+)\s*/\s*(?<d>[1-9]\d*)\s*$")]
    private static partial Regex MixedRegex();
    [GeneratedRegex(@"^\s*(?<a>\d+)\s*(?:÷|/|:)\s*(?<b>\d+)\s*=\s*(?<q>\d+)\s*(?:dư|du|remainder|r)\s*(?<r>\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DivisionRegex();
}
