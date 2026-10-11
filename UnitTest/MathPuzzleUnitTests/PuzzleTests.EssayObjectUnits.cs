using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckEssayObjectUnits()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        var cake = new ArithmeticQuizQuestion(new(0, ArithmeticOperation.Add, 0),
            ArithmeticQuizMode.Essay, 0, null, null, [],
            new MathWordProblem(
                "Anh Tiến có 2/5 chiếc bánh, một bạn khác có 4/5 chiếc bánh. Hỏi hai bạn có tất cả bao nhiêu chiếc bánh?",
                "Tổng lượng bánh là (chiếc):", "chiếc", "anh Tiến"),
            FractionProblem: new(new(2, 5), FractionOperation.Add, new(4, 5), new(6, 5), null, []));

        int count = 0;
        foreach (string lead in new[] { "Số bánh hai bạn có là :", "Tổng số chiếc bánh là:",
                     "Lượng bánh của hai bạn là:", "Tổng lượng bánh là (chiếc):" })
        foreach (string unit in new[] { "chiếc bánh", "chiếc" })
        {
            string submission = $"{lead}\n2/5 + 4/5 = 30/25 = 6/5 {unit}\nĐáp số : 6/5 {unit}";
            var result = validator.ValidateSubmission(cake, submission);
            Require(result.IsCorrect, $"Natural cake solution was rejected: {submission}; {result}");
            count++;
        }

        var wrongObject = validator.ValidateSubmission(cake,
            "Số bút hai bạn có là:\n2/5 + 4/5 = 6/5 chiếc bánh\nĐáp số: 6/5 chiếc bánh");
        Require(wrongObject.SolutionError == EssayAnswerError.WrongSolutionContent,
            "An unrelated solution object was accepted.");
        var wrongUnit = validator.ValidateSubmission(cake,
            "Số bánh hai bạn có là:\n2/5 + 4/5 = 6/5 chiếc bút\nĐáp số: 6/5 chiếc bút");
        Require(wrongUnit.EquationError == EssayAnswerError.WrongEquationUnit &&
                wrongUnit.AnswerError == EssayAnswerError.WrongAnswerUnit,
            "An unrelated calculation/answer unit was accepted.");
        var wrongNumber = validator.ValidateSubmission(cake,
            "Số bánh hai bạn có là:\n2/5 + 4/5 = 7/5 chiếc bánh\nĐáp số: 7/5 chiếc bánh");
        Require(!wrongNumber.EquationIsCorrect && !wrongNumber.AnswerIsCorrect,
            "Correct object wording concealed an incorrect result.");
        Require(!validator.ValidateSubmission(cake,
            "Bài giải:\n2/5 + 4/5 = 6/5 chiếc bánh\nĐáp số: 6/5 chiếc bánh").SolutionIsCorrect,
            "A generic heading was accepted as a solution sentence.");

        foreach (var (unit, noun, other) in new[]
        {
            ("chiếc", "bánh", "bút"), ("chiếc", "bánh mì", "bánh ngọt"),
            ("tấm", "vải", "giấy"), ("cây", "bút chì", "bút bi")
        })
        foreach (string question in new[]
        {
            $"Hỏi có tất cả bao nhiêu {unit} {noun}?",
            $"Hỏi có bao nhiêu {unit} {noun} trong mỗi hộp?",
            $"Còn lại mấy {unit} {noun}?",
            $"Hỏi có BAO NHIÊU {unit.ToUpperInvariant()} {noun.ToUpperInvariant()}?"
        })
        {
            string problem = $"Lan có một {unit} {other}. {question}";
            Require(WordProblemUnitEquivalence.ContainsVietnameseUnit($"Số {noun} là:", unit, problem),
                "The requested object was not recognized: " + question);
            Require(!WordProblemUnitEquivalence.ContainsVietnameseUnit($"Số {other} là:", unit, problem),
                "An object from the givens replaced the requested object: " + question);
            count++;
        }
        Require(!WordProblemUnitEquivalence.ContainsVietnameseUnit("Số bánh là:", "chiếc",
                "Hỏi bao nhiêu chiếc bánh mì?"),
            "Compound object names must remain intact.");
        Require(!WordProblemUnitEquivalence.ContainsVietnameseUnit("Số bánh là:", "chiếc"),
            "A bare classifier must not accept arbitrary object names without context.");
        CheckBreadObjectUnits(validator);
        Console.WriteLine($"  Checked {count} natural object leads, the exact cake submission, compound nouns and incorrect quantities/units.");
    }

    private static void CheckBreadObjectUnits(EssayAnswerValidator validator)
    {
        var bread = new ArithmeticQuizQuestion(new(0, ArithmeticOperation.Add, 0),
            ArithmeticQuizMode.Essay, 0, null, null, [],
            new MathWordProblem(
                "Anh Phú có 1/5 ổ bánh mì, một bạn khác có 2/5 ổ bánh mì. Hỏi hai bạn có tất cả bao nhiêu ổ bánh mì?",
                "Tổng lượng bánh mì là (ổ):", "ổ", "anh Phú"),
            FractionProblem: new(new(1, 5), FractionOperation.Add, new(2, 5), new(3, 5), null, []));

        foreach (string expected in new[] { "ổ", "ổ bánh mì" })
        foreach (string unit in new[] { "bánh mì", "ổ bánh mì", "chiếc bánh mì", "BÁNH MÌ", "bánh   mì" })
        foreach (string lead in new[] { "Số bánh mì hai bạn có tất cả là:", "Tổng số ổ bánh mì là:" })
        {
            var question = bread with { WordProblem = bread.WordProblem! with { AnswerUnit = expected } };
            string submission = $"{lead}\n1/5 + 2/5 = 15/25 = 3/5 {unit}\nĐáp số : 3/5 {unit}";
            var result = validator.ValidateSubmission(question, submission);
            Require(result.IsCorrect, $"Natural bread submission was rejected: {submission}; {result}");
        }
        foreach (string unit in new[] { "bánh", "bánh ngọt", "kg", "kg bánh mì", "bánh mì ngọt", "bánh mì và bút" })
        {
            var result = validator.ValidateSubmission(bread,
                $"Số bánh mì là:\n1/5 + 2/5 = 3/5 {unit}\nĐáp số: 3/5 {unit}");
            Require(result.EquationError == EssayAnswerError.WrongEquationUnit &&
                    result.AnswerError == EssayAnswerError.WrongAnswerUnit,
                "An unrelated or dimensional bread unit was accepted: " + unit);
        }
        var wrongLead = validator.ValidateSubmission(bread,
            "Số bánh ngọt là:\n1/5 + 2/5 = 3/5 bánh mì\nĐáp số: 3/5 bánh mì");
        Require(wrongLead.SolutionError == EssayAnswerError.WrongSolutionContent,
            "Bread target allowed an unrelated solution sentence.");
        var wrongValue = validator.ValidateSubmission(bread,
            "Số bánh mì là:\n1/5 + 2/5 = 4/5 bánh mì\nĐáp số: 4/5 bánh mì");
        Require(!wrongValue.EquationIsCorrect && !wrongValue.AnswerIsCorrect,
            "Bread wording concealed an incorrect quantity.");
        Require(!WordProblemUnitEquivalence.AreVietnameseUnitsEquivalent("bánh mì", "ổ"),
            "Bread must not replace a bare classifier without the problem context.");
        Require(!WordProblemUnitEquivalence.ContainsVietnameseUnit("Số bánh mì là:", "kg", bread.WordProblem!.ProblemText),
            "Omitting a count classifier must not omit a measurement unit.");
        Console.WriteLine("  Checked bread wording in solution, calculation and answer, compound nouns, wrong units and wrong quantities.");
    }
}
