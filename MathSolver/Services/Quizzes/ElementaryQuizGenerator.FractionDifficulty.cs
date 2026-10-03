using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateFractionDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.FractionSkills, type, language, tier);
        int level = (int)tier;
        t.RequiresSolution = level >= 4;
        int denominator = level == 1 ? 2 : level == 2 ? 4 : level == 3 ? 5 : level == 4 ? 6 : 8;
        int numerator = level <= 2 ? 1 : denominator - 1;
        string fraction = $"{numerator}/{denominator}";
        if (type is ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)
        {
            t.RequiresSolution = true;
            var contexts = FractionQuantityStoryContextCatalog.GetProfile(language);
            var context = contexts[_random.Next(contexts.Count)];
            int whole = denominator * (level == 5 ? 8 : 1) * _random.Next(3, 9);
            string unit = context.Unit, wholeExpression, problem;
            bool findPart = type == ElementaryQuizType.FractionOfNumber;
            string n = t.Given("numerator", numerator), d = t.Given("denominator", denominator);
            if (level <= 2)
            {
                int supplied = findPart ? whole : whole * numerator / denominator;
                string given = t.Given("quantity", supplied, unit);
                string expression = findPart ? $"{given}*{n}/{d}" : $"{given}/{n}*{d}";
                problem = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    findPart ? context.PartProblemTemplate : context.WholeProblemTemplate, given, fraction);
                t.Answer(findPart ? context.PartLabel : context.WholeLabel, expression, unit);
                return t.Build("direct-fraction-" + level, problem);
            }
            if (level < 5)
            {
                int supplied = findPart ? whole : whole * numerator / denominator;
                int first = supplied / 2;
                string a = t.Given("quantity-first", first, unit), b = t.Given("quantity-second", supplied - first, unit);
                wholeExpression = $"({a}+{b})";
                problem = t.L($"Lượng đã cho bằng tổng hai phần {a} và {b} {unit}. ",
                    $"The supplied quantity is the sum of {a} and {b} {unit}. ");
                if (level == 4)
                {
                    string removed = t.Given("removed", _random.Next(2, 6), unit);
                    t.Givens.RemoveAll(given => given.Role == "quantity-first");
                    a = t.Given("quantity-first", first + int.Parse(removed), unit);
                    wholeExpression = $"({a}+{b}-{removed})";
                    problem = t.L($"Hai phần có {a} và {b} {unit}; bớt {removed} {unit} trước khi xét phân số. ",
                        $"Two portions contain {a} and {b} {unit}; remove {removed} {unit} before considering the fraction. ");
                }
                t.Step(t.L("Lượng dùng để xét phân số", "Quantity used for the fraction"), wholeExpression, unit);
                problem += t.L(findPart
                    ? $"{context.PartLabel} bằng {fraction} lượng trên. Hỏi {context.PartLabel.ToLowerInvariant()} là bao nhiêu {unit}?"
                    : $"Lượng trên là {context.PartLabel.ToLowerInvariant()}, bằng {fraction} của {context.WholeLabel.ToLowerInvariant()}. Hỏi {context.WholeLabel.ToLowerInvariant()} là bao nhiêu {unit}?",
                    findPart
                    ? $"{context.PartLabel} is {fraction} of this quantity. What is {context.PartLabel.ToLowerInvariant()} in {unit}?"
                    : $"This quantity is {context.PartLabel.ToLowerInvariant()}, representing {fraction} of {context.WholeLabel.ToLowerInvariant()}. What is {context.WholeLabel.ToLowerInvariant()} in {unit}?");
                t.Answer(findPart ? context.PartLabel : context.WholeLabel,
                    findPart ? $"{wholeExpression}*{n}/{d}" : $"{wholeExpression}/{n}*{d}", unit);
            }
            else
            {
                string removedNumerator = t.Given("removed-numerator", 1), removedDenominator = t.Given("removed-denominator", 4);
                int supplied = findPart ? whole : whole * 3 / 4 * numerator / denominator;
                string quantity = t.Given("quantity", supplied, unit);
                string retained = $"(1-{removedNumerator}/{removedDenominator})";
                problem = t.L(findPart
                        ? $"{context.WholeLabel} là {quantity} {unit}. Phần thứ nhất chiếm {removedNumerator}/{removedDenominator} lượng ban đầu. {context.PartLabel} chiếm {n}/{d} lượng còn lại. Hỏi {context.PartLabel.ToLowerInvariant()} là bao nhiêu {unit}?"
                        : $"Phần thứ nhất chiếm {removedNumerator}/{removedDenominator} của {context.WholeLabel.ToLowerInvariant()}. {context.PartLabel} chiếm {n}/{d} lượng còn lại và có {quantity} {unit}. Hỏi {context.WholeLabel.ToLowerInvariant()} là bao nhiêu {unit}?",
                    findPart
                        ? $"{context.WholeLabel} is {quantity} {unit}. A first portion accounts for {removedNumerator}/{removedDenominator} of the initial quantity. {context.PartLabel} accounts for {n}/{d} of the remainder. What is {context.PartLabel.ToLowerInvariant()} in {unit}?"
                        : $"A first portion accounts for {removedNumerator}/{removedDenominator} of {context.WholeLabel.ToLowerInvariant()}. {context.PartLabel} accounts for {n}/{d} of the remainder and contains {quantity} {unit}. What is {context.WholeLabel.ToLowerInvariant()} in {unit}?");
                t.Step(t.L("Phân số còn lại sau phần đầu", "Fraction remaining after the first portion"), retained);
                if (findPart) t.Step(t.L("Lượng còn lại", "Remaining quantity"), $"{quantity}*{retained}", unit);
                else t.Step(t.L("Phần lượng ban đầu được dùng lần sau", "Fraction of the initial quantity used the second time"), $"{retained}*{n}/{d}");
                t.Answer(findPart ? context.PartLabel : context.WholeLabel,
                    findPart ? $"{quantity}*{retained}*{n}/{d}" : $"{quantity}/({retained}*{n}/{d})", unit);
            }
            return t.Build("inferred-fraction-" + level, problem);
        }
        if (type == ElementaryQuizType.ReduceFraction)
        {
            int gcd = level <= 2 ? level + 1 : _random.Next(5, 10);
            int baseD = _random.Next(3, 9), baseN = baseD - 1;
            string nExpression, dExpression, problem;
            if (level <= 3)
            {
                nExpression = t.Given("numerator", baseN * gcd); dExpression = t.Given("denominator", baseD * gcd);
                problem = t.L($"Rút gọn {nExpression}/{dExpression} đến tối giản.", $"Reduce {nExpression}/{dExpression} to lowest terms.");
            }
            else
            {
                string first = t.Given("numerator-first", baseN * gcd - 2), second = t.Given("numerator-second", 2);
                nExpression = $"({first}+{second})";
                if (level == 4) dExpression = t.Given("denominator", baseD * gcd);
                else
                {
                    string upper = t.Given("denominator-before", baseD * gcd + 3), removed = t.Given("denominator-removed", 3);
                    dExpression = $"({upper}-{removed})";
                    t.Step(t.L("Mẫu số", "Denominator"), dExpression);
                }
                t.Step(t.L("Tử số", "Numerator"), nExpression);
                problem = t.L($"Phân số có tử số bằng {first} + {second}, mẫu số bằng {dExpression}. Viết phân số tối giản.",
                    $"A fraction has numerator {first} + {second} and denominator {dExpression}. Write it in lowest terms.");
            }
            t.Answer(t.L("Phân số tối giản", "Reduced fraction"), $"{nExpression}/{dExpression}", reduced: true);
            return t.Build("reduce-inferred-components-" + level, problem);
        }
        if (type == ElementaryQuizType.CompareFractions)
        {
            string n1 = t.Given("numerator-first", _random.Next(1, denominator));
            string d1 = t.Given("denominator-first", denominator);
            int secondD = level == 1 ? denominator : level == 2 ? 2 * denominator : denominator - 1;
            string n2 = t.Given("numerator-second", _random.Next(1, secondD));
            string d2 = t.Given("denominator-second", secondD);
            string left = $"{n1}/{d1}", right = $"{n2}/{d2}";
            if (level >= 4)
            {
                string n3 = t.Given("extra-numerator", 1), d3 = t.Given("extra-denominator", denominator + 1);
                left = $"({left}+{n3}/{d3})";
                if (level == 5)
                {
                    string n4 = t.Given("right-extra-numerator", 1), d4 = t.Given("right-extra-denominator", denominator + 2);
                    right = $"({right}+{n4}/{d4})";
                }
            }
            EssayCalculationEvaluator.TryEvaluate(left, out var leftValue, out _);
            EssayCalculationEvaluator.TryEvaluate(right, out var rightValue, out _);
            int compare = (leftValue.Numerator * rightValue.Denominator).CompareTo(rightValue.Numerator * leftValue.Denominator);
            t.TextAnswer(t.L("Dấu so sánh", "Comparison"), compare < 0 ? "<" : compare > 0 ? ">" : "=");
            return t.Build("compare-relations-" + level, t.L($"So sánh {left} và {right}.", $"Compare {left} and {right}.")) with
                { ComparisonLeftExpression = left, ComparisonRightExpression = right };
        }
        if (type == ElementaryQuizType.CommonDenominator)
        {
            int[] denominators = level switch { 1 => [3, 6], 2 => [4, 8], 3 => [4, 5], 4 => [4, 6, 9], _ => [5, 7, 8] };
            int common = denominators.Aggregate(1, (a, b) => a * b / (int)BigInteger.GreatestCommonDivisor(a, b));
            t.Constant(common);
            var fractionTexts = new List<string>();
            for (int index = 0; index < denominators.Length; index++)
            {
                string n = t.Given("numerator-" + index, _random.Next(1, denominators[index]));
                string d = t.Given("denominator-" + index, denominators[index]);
                fractionTexts.Add($"{n}/{d}");
                t.Answer(t.L($"Phân số thứ {index + 1}", $"Fraction {index + 1}"), $"{n}/{d}", denominator: common);
            }
            // The required common denominator is the LCM, so the instruction has one definite target.
            string problem = level <= 2
                ? t.L($"Quy đồng {string.Join("; ", fractionTexts)} về mẫu số {common}.", $"Express {string.Join("; ", fractionTexts)} with denominator {common}.")
                : t.L($"Quy đồng {string.Join("; ", fractionTexts)} với mẫu chung nhỏ nhất.", $"Express {string.Join("; ", fractionTexts)} using the least common denominator.");
            t.RequiresSolution = false;
            return t.Build("common-denominator-" + level, problem);
        }
        // Mixed numbers combine fractions before normalization at higher levels.
        string den = t.Given("denominator", denominator);
        string num = t.Given("numerator", denominator * _random.Next(2, 6) + 1);
        string mixedExpression = $"{num}/{den}";
        if (level >= 3)
        {
            string additional = t.Given("extra-numerator", 1), additionalD = t.Given("extra-denominator", level == 3 ? denominator : denominator + 1);
            mixedExpression = $"({mixedExpression}+{additional}/{additionalD})";
            if (level == 5)
            {
                string sub = t.Given("subtract-numerator", 1), subD = t.Given("subtract-denominator", denominator + 2);
                mixedExpression = $"({mixedExpression}-{sub}/{subD})";
            }
        }
        t.Answer(t.L("Hỗn số", "Mixed number"), mixedExpression, mixed: true);
        return t.Build("mixed-number-relations-" + level,
            t.L($"Tính {mixedExpression} và viết kết quả dưới dạng hỗn số tối giản.", $"Calculate {mixedExpression} and write the result as a reduced mixed number."));
    }
}
