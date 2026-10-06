using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckElementaryDifficulty()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        QuizProblemKind[] families = [QuizProblemKind.TwoNumbers, QuizProblemKind.Data,
            QuizProblemKind.Time, QuizProblemKind.Measurement, QuizProblemKind.Probability, QuizProblemKind.FractionSkills];
        int count = 0;
        foreach (var family in families)
        foreach (var type in ElementaryQuizGenerator.Types(family))
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 10; seed < 22; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay, family, type, language, tier);
            var contract = question.ElementaryProblem!;
            Require(contract.Reasoning?.Tier == tier && contract.Reasoning.Givens.Select(given => given.Value).SequenceEqual(contract.Facts),
                "Difficulty metadata and authoritative supplied facts disagree.");
            var g = contract.Reasoning!.Givens.ToDictionary(given => given.Role,
                given => decimal.Parse(given.Value, CultureInfo.InvariantCulture));
            decimal Get(string role) => g.GetValueOrDefault(role);
            decimal Value(int index = 0) => (decimal)contract.Answers[index].Value.Numerator / (decimal)contract.Answers[index].Value.Denominator;
            int level = (int)tier;
            if (type == ElementaryQuizType.ChartDifference && level == 5)
                Require(Value() != Get("extra"), "A five-star chart difference must combine relations rather than copy the supplied gap.");
            if (family == QuizProblemKind.TwoNumbers)
            {
                Require(contract.ProblemText.Split(". ").All(sentence => char.IsUpper(sentence[0])),
                    $"{language}/{tier}/{type}: two-number story sentences must start with a capital letter.");
                bool ratio = type is ElementaryQuizType.SumRatio or ElementaryQuizType.DifferenceRatio;
                Require(question.UsesFractionFormatting == ratio,
                    "Sum/difference ratio questions must use textbook fraction presentation.");
                if (ratio && level < 5)
                {
                    string fraction = $"{Get("ratio-small")}/{Get("ratio-large")}";
                    var fragments = TextbookFractionParser.ParseLine(contract.ProblemText);
                    Require(fragments.Any(fragment => fragment.Math is not null && fragment.Text == fraction) &&
                        string.Concat(fragments.Select(fragment => fragment.Text)) == contract.ProblemText,
                        "Ratio presentation must preserve the given numerator/denominator and story wording.");
                }
                decimal quantity = level == 1 ? Get("quantity") : level == 2 ? Get("double-quantity") / 2
                    : level == 3 ? Get("quantity-first") + Get("quantity-second")
                    : Get("after") + Get("previous-reduction") - Get("added") * (type == ElementaryQuizType.DifferenceRatio ? 1 : 2);
                decimal smaller = Value(), larger = Value(1);
                Require(smaller > 0 && larger > smaller &&
                    (type == ElementaryQuizType.DifferenceRatio ? larger - smaller : larger + smaller) == quantity,
                    "Original pair does not satisfy the sum/difference after undoing changes.");
                if (type == ElementaryQuizType.SumDifference)
                    Require(larger - smaller == (level <= 3 ? Get("difference") : Get("remaining-difference") + 2 * Get("transfer")),
                        "Transferring between both numbers must change their difference twice.");
                else Require(smaller * (level == 5 ? Get("ratio-small") + Get("ratio-extra") : Get("ratio-large")) == larger * Get("ratio-small"),
                    "Inferred ratio does not match the original pair.");
            }
            if (family == QuizProblemKind.Time)
            {
                decimal expected;
                if (type == ElementaryQuizType.Calendar)
                {
                    var start = new DateTime((int)Get("year"), (int)Get("start-month"), (int)Get("start-day"));
                    var end = new DateTime((int)Get("year"), (int)Get("end-month"), (int)Get("end-day"));
                    expected = (end - start).Days - Get("paused-days");
                }
                else if (type == ElementaryQuizType.ReadClock)
                {
                    int minutes = (int)(Get("start-hour") * 60 + Get("start-minute") + Get("advance-hours") * 60
                        + Get("advance-minutes") - Get("subtract-minutes"));
                    Require(Value() == minutes / 60 && Value(1) == minutes % 60, "Clock carry is incorrect.");
                    expected = Value();
                }
                else if (type == ElementaryQuizType.ElapsedTime)
                {
                    decimal start = Get("start-hour") * 60 + Get("start-minute"), end = Get("end-hour") * 60 + Get("end-minute");
                    if (end < start) end += 24 * 60;
                    expected = end - start - Get("pause-minutes") + Get("second-session");
                }
                else
                {
                    expected = (Get("first-hours") + Get("second-hours")) * 60 + Get("first-minutes")
                        + Get("extra-minutes") - Get("pause-minutes");
                    if (level == 5) expected /= 60;
                }
                Require(Value() == expected, "Date/time answer disagrees with independent interval arithmetic.");
            }
            if (family == QuizProblemKind.Measurement)
            {
                decimal factor = type switch
                {
                    ElementaryQuizType.AreaConversion => level <= 2 ? 100 : 10000,
                    ElementaryQuizType.VolumeConversion => level <= 2 ? 1000 : 1000000,
                    ElementaryQuizType.MassConversion or ElementaryQuizType.CapacityConversion => 1000,
                    _ => level == 1 ? 10 : 100
                };
                decimal expected = (Get("large-quantity") + Get("extra-quantity")) * factor
                    + Get("small-quantity") - Get("removed-quantity");
                if (level == 2 || level == 5) expected /= factor;
                Require(Value() == expected, "Length/area/volume conversion uses the wrong dimension factor or operation order.");
            }
            if (family == QuizProblemKind.FractionSkills && type == ElementaryQuizType.CommonDenominator)
            {
                int lcm = g.Where(pair => pair.Key.StartsWith("denominator-", StringComparison.Ordinal)).Select(pair => (int)pair.Value)
                    .Aggregate(1, (a, b) => a * b / (int)BigInteger.GreatestCommonDivisor(a, b));
                Require(contract.Answers.All(answer => answer.RequiredDenominator == lcm), "Requested common denominator must be the LCM.");
            }
            if (family == QuizProblemKind.FractionSkills)
            {
                ReducedFraction Fraction(string n, string d) => new((BigInteger)Get(n), (BigInteger)Get(d));
                ReducedFraction Add(ReducedFraction a, ReducedFraction b, int sign = 1) =>
                    new(a.Numerator * b.Denominator + sign * b.Numerator * a.Denominator, a.Denominator * b.Denominator);
                if (type == ElementaryQuizType.ReduceFraction)
                {
                    decimal n = level <= 3 ? Get("numerator") : Get("numerator-first") + Get("numerator-second");
                    decimal d = level <= 4 ? Get("denominator") : Get("denominator-before") - Get("denominator-removed");
                    Require(contract.Answers[0].Value == new ReducedFraction((BigInteger)n, (BigInteger)d), "Inferred fraction components do not match the reduced answer.");
                }
                else if (type == ElementaryQuizType.MixedNumber)
                {
                    var expected = Fraction("numerator", "denominator");
                    if (level >= 3) expected = Add(expected, Fraction("extra-numerator", "extra-denominator"));
                    if (level == 5) expected = Add(expected, Fraction("subtract-numerator", "subtract-denominator"), -1);
                    Require(contract.Answers[0].Value == expected && expected.Numerator > expected.Denominator, "Combined fractions do not match the mixed-number value.");
                }
                else if (type is ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)
                {
                    Require(contract.RequiresSolution, "Fraction word problems must retain their solution-sentence requirement.");
                    var fraction = Fraction("numerator", "denominator");
                    if (level == 5) fraction = new(fraction.Numerator * 3, fraction.Denominator * 4);
                    BigInteger supplied = (BigInteger)(level <= 2 || level == 5 ? Get("quantity")
                        : Get("quantity-first") + Get("quantity-second") - Get("removed"));
                    var expected = type == ElementaryQuizType.FractionOfNumber
                        ? new ReducedFraction(supplied * fraction.Numerator, fraction.Denominator)
                        : new ReducedFraction(supplied * fraction.Denominator, fraction.Numerator);
                    Require(contract.Answers[0].Value == expected, "Indirect or successive fraction-of-whole relations are incorrect.");
                }
            }
            var parsed = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true);
            var sample = grader.Validate(question, parsed.Solution, parsed.Equation, parsed.Answer);
            Require(sample.IsCorrect, $"{language}/{tier}/{type}/{seed}: {string.Join(" | ", sample.Details)}\n{contract.SolutionText}");
            if (!contract.IsComparison && contract.Reasoning.Steps.Count > 0)
            {
                string Equation(ElementaryInferenceStep step) => step.Expression + "=" + step.DisplayValue + (step.Unit.Length > 0 ? " " + step.Unit : "");
                string reversed = string.Join("\n", contract.Reasoning.Steps.Reverse().Select(Equation));
                Require(grader.Validate(question, parsed.Solution, reversed, parsed.Answer).IsCorrect,
                    $"Independent expanded steps must permit reordering: {type}/{tier}/{seed}");
                string merged = string.Join("\n", contract.Reasoning.Steps.Where(step => contract.Answers.Any(answer => answer.Expression == step.Expression)).Select(Equation));
                if (merged.Length > 0)
                    Require(grader.Validate(question, parsed.Solution, merged, parsed.Answer).IsCorrect,
                        $"Merged derivation must remain valid: {type}/{tier}/{seed}");
                var earlier = new List<ElementaryInferenceStep>();
                var split = new List<string>();
                foreach (var step in contract.Reasoning.Steps)
                {
                    string expression = step.Expression;
                    foreach (var previous in earlier.OrderByDescending(previous => previous.Expression.Length))
                        if (previous.Expression.Any(character => "+-*/".Contains(character)))
                            expression = expression.Replace(previous.Expression, "(" + previous.Value + ")", StringComparison.Ordinal);
                    split.Add(expression + "=" + step.DisplayValue + (step.Unit.Length > 0 ? " " + step.Unit : ""));
                    earlier.Add(step);
                }
                var splitResult = grader.Validate(question, parsed.Solution, string.Join("\n", split), parsed.Answer);
                Require(splitResult.IsCorrect, $"Split derivation must remain valid: {type}/{tier}/{seed}\n{string.Join("\n", split)}\n{string.Join(" | ", splitResult.Details)}");
                var last = contract.Reasoning.Steps[^1];
                string wrongResult = last.Expression + "=999999" + (last.Unit.Length > 0 ? " " + last.Unit : "");
                Require(!grader.Validate(question, parsed.Solution, parsed.Equation + "\n" + wrongResult, parsed.Answer).IsCorrect,
                    "A correct final answer must not hide an incorrect intermediate result.");
                if (last.Unit.Length > 0)
                    Require(!grader.Validate(question, parsed.Solution, last.Expression + "=" + last.DisplayValue + " invalidunit", parsed.Answer).IsCorrect,
                        "Calculation units must still be checked.");
                if (family == QuizProblemKind.Measurement && level >= 3)
                {
                    var conversion = contract.Reasoning.Steps[0];
                    string[] factors = conversion.Expression.Split('*');
                    string equivalent = factors[1] + "*" + factors[0];
                    string largeUnit = contract.Reasoning.Givens.First(given => given.Role == "large-quantity").Unit;
                    Require(!grader.Validate(question, parsed.Solution,
                        parsed.Equation + "\n" + equivalent + "=" + conversion.DisplayValue + " " + largeUnit, parsed.Answer).IsCorrect,
                        "Reversing multiplication must not bypass the intermediate conversion unit.");
                }
            }
            count++;
        }
        Console.WriteLine($"  Checked {count} new tier contracts, independent relations, dates, dimensional conversion, fraction reasoning, merged/split/reordered work and earlier errors.");
    }
}
