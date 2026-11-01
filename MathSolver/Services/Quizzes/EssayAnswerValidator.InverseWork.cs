using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    private static bool IsInverseWork(ArithmeticQuizQuestion q) => q.ProportionProblem is
        { Type: ProportionQuizType.Inverse, Scenario: ProportionScenarioKind.WorkersDays, AsksForAdditionalPeople: false } p
        && p.NarrativeId.StartsWith(InverseWorkPilot.Prefix, StringComparison.Ordinal);

    private static (bool IsCorrect, EssayAnswerError Error) ValidateInverseWorkCalculations(
        ArithmeticQuizQuestion q, string? text, out IReadOnlyList<EssayStepValidationResult> steps)
    {
        var p = q.ProportionProblem!;
        if (!IsInverseWork(q) || p.A <= 0 || p.B <= 0 || p.C <= 0 || p.A == p.C
            || (System.Numerics.BigInteger)p.A * p.B % p.C != 0
            || p.CorrectAnswer != (System.Numerics.BigInteger)p.A * p.B / p.C)
        { steps = []; return (false, EssayAnswerError.WrongOperandsOrOperation); }
        var work = (System.Numerics.BigInteger)p.A * p.B;
        string[] lines = (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is < 1 or > 2)
        { steps = []; return (false, EssayAnswerError.InvalidEquationFormat); }
        var checkedSteps = new List<EssayStepValidationResult>();
        for (int i = 0; i < lines.Length; i++)
        {
            bool singleWorker = lines.Length == 2 && i == 0;
            var (calculation, unit) = SplitEquationResult(q, lines[i]);
            string normalized = NormalizeProportionEquation(calculation);
            string[] forms = singleWorker
                ? [$"{p.A} × {p.B} = {work}", $"{p.B} × {p.A} = {work}"]
                : [$"{p.A} × {p.B} ÷ {p.C} = {p.CorrectAnswer}",
                    $"{p.B} × {p.A} ÷ {p.C} = {p.CorrectAnswer}",
                    $"{p.A} ÷ {p.C} × {p.B} = {p.CorrectAnswer}",
                    $"{p.B} ÷ {p.C} × {p.A} = {p.CorrectAnswer}"];
            bool shape = forms.Any(form => NormalizeProportionEquation(form) == normalized)
                || !singleWorker && lines.Length == 2
                    && normalized == NormalizeProportionEquation($"{work} ÷ {p.C} = {p.CorrectAnswer}");
            (bool IsCorrect, EssayAnswerError Error) result = shape
                // Evaluate the original expression as well: stripping parentheses
                // for operand matching must never certify changed grouping.
                ? ValidateCalculatedEquation(singleWorker ? q with { CorrectAnswer = work } : q, calculation)
                : (false, EssayAnswerError.WrongOperandsOrOperation);
            if (result.IsCorrect && !IsExpectedUnit(q, unit)) result = (false, EssayAnswerError.WrongEquationUnit);
            checkedSteps.Add(new(i + 1, lines[i], result.IsCorrect, result.Error));
        }
        steps = checkedSteps;
        var failed = checkedSteps.FirstOrDefault(s => !s.IsCorrect);
        return failed is null ? (true, EssayAnswerError.None) : (false, failed.Error);
    }
}
