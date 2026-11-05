using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckUnitaryGroupCalculations()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            bool vi = language == AppLanguage.Vietnamese;
            string items = vi ? "cây bút" : "pens", group = vi ? "hộp" : "box", groups = vi ? "hộp" : "boxes";
            // Four boxes hold 24 pens; 42 pens need seven boxes, with six per box.
            var contract = new ProportionQuizContract(ProportionQuizType.UnitaryGroupCount,
                ProportionScenarioKind.ProductionItems, 4, 24, 42, 7, groups, groups, "fixture")
                { GroupCountUnits = new(items, group, groups) };
            var question = new ArithmeticQuizQuestion(new(42, ArithmeticOperation.Divide, 6),
                ArithmeticQuizMode.Essay, 7, null, null, [], ProportionProblem: contract);
            EssayAnswerValidationResult Grade(string equation) => validator.Validate(question, null, equation, $"7 {groups}");
            foreach (string expression in new[]
            {
                "42 / (24 / 4)", "(42 / (24 / 4)) + 0", "0 + (42 / (24 / 4))",
                "(42 * 4 / 24) - 0", "(4 * 42 / 24) * 1", "(42 / 24 * 4) / 1"
            })
                Require(Grade($"{expression} = 7 {groups}").EquationIsCorrect,
                    $"{language}: valid source-based group-count calculation rejected: {expression}");
            Require(Grade($"(24 / 4) + 0 = 6 {items}/{group}\n(42 / 6) * 1 = 7 {groups}").EquationIsCorrect,
                "Neutral operations must preserve both rate and final group-count steps.");
            foreach (string expression in new[] { "7 + 0", "42 / 6", "42 / (4 / 24)", "42 / 24 / 4", "24 / (42 / 4)" })
                Require(!Grade($"{expression} = 7 {groups}").EquationIsCorrect,
                    "Unproved rate, reversed grouping or answer-only arithmetic accepted: " + expression);
            Require(Grade($"1 + 0 = 8 {groups}").EquationError == EssayAnswerError.WrongEquationResult,
                "An incorrect written result must remain a result error.");
            Require(Grade($"42 / (24 / 4) = 7 {items}").EquationError == EssayAnswerError.WrongEquationUnit,
                "Group count cannot be labelled as an item count.");
            Require(!Grade($"42 / (24 / 4) = 8 = 7 {groups}").EquationIsCorrect,
                "A false intermediate equality must not be hidden by the final correct answer.");
        }
    }
}
