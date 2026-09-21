using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

int passed = 0;
foreach (bool mixed in new[] { false, true })
foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
{
    var context = new QuizCurriculumContext(tier, mixed);
    var rules = QuizCurriculumLayer.GetFractionRules(context);
    if (!rules.IsAvailable) continue;
    var generator = new FractionQuizGenerator(new FractionCalculationEngine(), new Random(1234));
    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
    foreach (var operation in Enum.GetValues<FractionOperation>())
    for (int i = 0; i < 100; i++)
    {
        var question = generator.Generate(mode, operation, context);
        if (question.FractionProblem is not { } fraction) throw new Exception("Missing fraction");
        foreach (var operand in new[] { fraction.LeftOperand, fraction.RightOperand })
        {
            if (operand.Denominator < 2 || operand.Denominator > rules.MaximumDenominator ||
                operand.Numerator <= 0 || operand.Numerator >= operand.Denominator)
                throw new Exception($"Invalid operand: {tier}, mixed={mixed}, {operand}");
        }
        passed++;
    }
}
Console.WriteLine($"PASS {passed} fraction questions across tiers, modes and operations.");
