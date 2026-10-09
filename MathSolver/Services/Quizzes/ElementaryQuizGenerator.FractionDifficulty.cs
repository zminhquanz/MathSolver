using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    public static readonly ElementaryQuizType[] FractionQuantityStoryTypes =
        [ElementaryQuizType.FractionOfNumber, ElementaryQuizType.WholeFromFraction];

    public ArithmeticQuizQuestion GenerateFractionQuantityStory(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId)
    {
        if (!Enum.IsDefined(tier) || type is not (ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)
            || string.IsNullOrWhiteSpace(contextId)) throw new ArgumentException("InvalidFractionQuantityProfile");
        return CompleteQuestion(mode, CreateFractionDifficulty(type, language, tier, contextId), [], null);
    }

    private ElementaryQuizContract CreateFractionDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier,
        string contextId = "")
    {
        var t = new DifficultyBuilder(QuizProblemKind.FractionSkills, type, language, tier);
        int level = (int)tier;
        t.RequiresSolution = level >= 4;
        int denominator = level == 1 ? 2 : level == 2 ? 4 : level == 3 ? 5 : level == 4 ? 6 : 8;
        int numerator = level <= 2 ? 1 : denominator - 1;
        if (_expandNarratives && type is ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)
        {
            int[] choices = level switch { 1 => [2, 3], 2 => [3, 4, 5], 3 => [4, 5, 6], 4 => [5, 6, 8], _ => [4, 5, 8] };
            denominator = choices[_random.Next(choices.Length)];
            numerator = level == 1 ? 1 : _random.Next(1, denominator);
        }
        string fraction = $"{numerator}/{denominator}";
        if (type is ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)
        {
            t.RequiresSolution = true;
            var contexts = FractionQuantityStoryContextCatalog.GetProfile(language, _expandNarratives);
            var context = contextId.Length == 0 ? contexts[_random.Next(contexts.Count)]
                : contexts.SingleOrDefault(c => c.ContextId == contextId) ?? throw new ArgumentException("InvalidFractionQuantityProfile");
            string unit = context.Unit, wholeExpression, problem;
            bool findPart = type == ElementaryQuizType.FractionOfNumber;
            var activity = _expandNarratives ? FractionQuantityActivityCatalog.Get(language, context.ContextId) : null;
            string ContextText(string suffix, params (string Key, string Value)[] values) => QuizContentCatalog.Text(language,
                "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.Context." + suffix, values);
            int supplied;
            if (context.Quantity == WordProblemQuantity.Count)
            {
                // All counted quantities, including the retained 3/4 at five stars,
                // must stay integral. Never truncate a fractional item count.
                int quantum = denominator * (level == 5 ? 4 : 1);
                int maximum = Math.Min(8, context.Capacity / quantum);
                if (maximum < 1) throw new InvalidDataException("Fraction context capacity is too small: " + context.ContextId);
                int whole = quantum * _random.Next(1, maximum + 1);
                supplied = findPart ? whole : whole * numerator * (level == 5 ? 3 : 1) / (denominator * (level == 5 ? 4 : 1));
            }
            else if (context.Quantity is WordProblemQuantity.Mass or WordProblemQuantity.Distance or WordProblemQuantity.Capacity)
            {
                // Supply whole units, then let exact rational arithmetic compute the
                // requested part/whole. These dimensions permit fractional results.
                int maximum = Math.Min(8, findPart ? context.Capacity
                    : (int)((long)context.Capacity * numerator * (level == 5 ? 3 : 1) / (denominator * (level == 5 ? 4 : 1))));
                int minimum = level is 3 or 4 ? 2 : 1;
                if (maximum < minimum) throw new InvalidDataException("Fraction context capacity is too small: " + context.ContextId);
                supplied = _random.Next(minimum, maximum + 1);
            }
            else throw new InvalidDataException("Unsupported fraction quantity: " + context.ContextId);
            string n = t.Given("numerator", numerator), d = t.Given("denominator", denominator);
            if (level <= 2)
            {
                string given = t.Given("quantity", supplied, unit);
                string expression = findPart ? $"{given}*{n}/{d}" : $"{given}/{n}*{d}";
                problem = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    findPart ? context.PartProblemTemplate : context.WholeProblemTemplate, given, fraction);
                QuizNarrativeCapture.Current?.Record(findPart ? context.PartProblemTemplate : context.WholeProblemTemplate,
                    new Dictionary<string, string> { ["0"] = given, ["1"] = fraction }, problem);
                t.Answer(findPart ? context.PartLabel : context.WholeLabel, expression, unit);
                return t.Build("direct-fraction-" + level, problem) with { StoryContextId = context.ContextId };
            }
            if (level < 5)
            {
                int first = supplied / 2;
                string a = t.Given("quantity-first", first, unit), b = t.Given("quantity-second", supplied - first, unit);
                wholeExpression = $"({a}+{b})";
                problem = activity is null ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.001", ("a", $"{a}"), ("b", $"{b}"), ("unit", $"{unit}"))
                    : ContextText("001", ("source", findPart ? activity.PartSource : activity.WholeSource), ("a", a), ("b", b), ("unit", unit));
                if (level == 4)
                {
                    // The contextual story describes an overlap of two records.
                    // An overlap cannot contain more items than either record.
                    string removed = t.Given("removed", activity is null ? _random.Next(2, 6)
                        : _random.Next(1, Math.Min(5, supplied - first) + 1), unit);
                    t.Givens.RemoveAll(given => given.Role == "quantity-first");
                    a = t.Given("quantity-first", first + int.Parse(removed), unit);
                    wholeExpression = $"({a}+{b}-{removed})";
                    problem = activity is null ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.002", ("a", $"{a}"), ("b", $"{b}"), ("unit", $"{unit}"), ("removed", $"{removed}"))
                        : ContextText("002", ("source", findPart ? activity.PartSource : activity.WholeSource), ("a", a), ("b", b), ("unit", unit), ("removed", removed));
                }
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.003"), wholeExpression, unit);
                problem += (findPart ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.017", ("context_PartLabel", $"{context.PartLabel}"), ("fraction", $"{fraction}"), ("context_PartLabel_ToLowerInvariant", $"{context.PartLabel.ToLowerInvariant()}"), ("unit", $"{unit}")) : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.018", ("context_PartLabel_ToLowerInvariant", $"{context.PartLabel.ToLowerInvariant()}"), ("fraction", $"{fraction}"), ("context_WholeLabel_ToLowerInvariant", $"{context.WholeLabel.ToLowerInvariant()}"), ("unit", $"{unit}")));
                t.Answer(findPart ? context.PartLabel : context.WholeLabel,
                    findPart ? $"{wholeExpression}*{n}/{d}" : $"{wholeExpression}/{n}*{d}", unit);
            }
            else
            {
                string removedNumerator = t.Given("removed-numerator", 1), removedDenominator = t.Given("removed-denominator", 4);
                string quantity = t.Given("quantity", supplied, unit);
                string retained = $"(1-{removedNumerator}/{removedDenominator})";
                problem = (findPart ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.019", ("context_WholeLabel", $"{context.WholeLabel}"), ("quantity", $"{quantity}"), ("unit", $"{unit}"), ("removedNumerator", $"{removedNumerator}"), ("removedDenominator", $"{removedDenominator}"), ("context_PartLabel", $"{context.PartLabel}"), ("n", $"{n}"), ("d", $"{d}"), ("context_PartLabel_ToLowerInvariant", $"{context.PartLabel.ToLowerInvariant()}")) : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.020", ("removedNumerator", $"{removedNumerator}"), ("removedDenominator", $"{removedDenominator}"), ("context_WholeLabel_ToLowerInvariant", $"{context.WholeLabel.ToLowerInvariant()}"), ("context_PartLabel", $"{context.PartLabel}"), ("n", $"{n}"), ("d", $"{d}"), ("quantity", $"{quantity}"), ("unit", $"{unit}")));
                if (activity is not null)
                    problem = ContextText(findPart ? "019" : "020", ("source", activity.PartSource),
                        ("initial_action", activity.InitialAction), ("part_action", activity.PartAction),
                        ("quantity", quantity), ("unit", unit), ("removedNumerator", removedNumerator),
                        ("removedDenominator", removedDenominator), ("n", n), ("d", d),
                        ("target", (findPart ? context.PartLabel : context.WholeLabel).ToLowerInvariant()));
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.004"), retained);
                if (findPart) t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.005"), $"{quantity}*{retained}", unit);
                else t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.006"), $"{retained}*{n}/{d}");
                t.Answer(findPart ? context.PartLabel : context.WholeLabel,
                    findPart ? $"{quantity}*{retained}*{n}/{d}" : $"{quantity}/({retained}*{n}/{d})", unit);
            }
            return t.Build("inferred-fraction-" + level, problem) with { StoryContextId = context.ContextId };
        }
        if (type == ElementaryQuizType.ReduceFraction)
        {
            int gcd = level <= 2 ? level + 1 : _random.Next(5, 10);
            int baseD = _random.Next(3, 9), baseN = baseD - 1;
            string nExpression, dExpression, problem;
            if (level <= 3)
            {
                nExpression = t.Given("numerator", baseN * gcd); dExpression = t.Given("denominator", baseD * gcd);
                problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.007", ("nExpression", $"{nExpression}"), ("dExpression", $"{dExpression}"));
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
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.008"), dExpression);
                }
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.009"), nExpression);
                problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.010", ("first", $"{first}"), ("second", $"{second}"), ("dExpression", $"{dExpression}"));
            }
            t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.011"), $"{nExpression}/{dExpression}", reduced: true);
            return t.Build("reduce-inferred-components-" + level, problem);
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
                t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.012", ("index_1", $"{index + 1}")), $"{n}/{d}", denominator: common);
            }
            // The required common denominator is the LCM, so the instruction has one definite target.
            string problem = level <= 2
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.013", ("string_Join_fractionTexts", $"{string.Join("; ", fractionTexts)}"), ("common", $"{common}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.014", ("string_Join_fractionTexts", $"{string.Join("; ", fractionTexts)}"));
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
        t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.015"), mixedExpression, mixed: true);
        return t.Build("mixed-number-relations-" + level,
            QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.FractionDifficulty.CreateFractionDifficulty.016", ("mixedExpression", $"{mixedExpression}")));
    }
}
