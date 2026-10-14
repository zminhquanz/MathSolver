using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed record DecimalSkillProfile(int Star, int Places, int MaximumWhole);

public sealed partial class ElementaryQuizGenerator
{
    public static ElementaryQuizType[] DecimalSkillTypes => [ElementaryQuizType.ReadDecimal, ElementaryQuizType.WriteDecimal,
        ElementaryQuizType.DecimalPlace, ElementaryQuizType.DecimalParts, ElementaryQuizType.OrderDecimals,
        ElementaryQuizType.DecimalToFraction, ElementaryQuizType.FractionToDecimal];
    public static ElementaryQuizType[] SpatialSkillTypes => [ElementaryQuizType.SpatialFront, ElementaryQuizType.SpatialBehind, ElementaryQuizType.SpatialBetween];
    public static ElementaryQuizType[] DecimalBankTypes => [.. DecimalStoryTypes, .. DecimalSkillTypes, ElementaryQuizType.DecimalRound, ElementaryQuizType.DecimalCompare];
    public static bool IsDecimalSpatialSkill(ElementaryQuizType type) => DecimalSkillTypes.Contains(type) || SpatialSkillTypes.Contains(type);
    internal static string[] DecimalSpatialContexts(ElementaryQuizType type) => type == ElementaryQuizType.OrderDecimals
        ? ["decimals-ascending", "decimals-descending"] : [type.ToString()];

    public ArithmeticQuizQuestion GenerateDecimalSpatialSkill(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!IsDecimalSpatialSkill(type) || !Enum.IsDefined(tier)) throw new ArgumentException("InvalidDecimalSpatialSkill");
        var c = CreateDecimalSpatialSkill(type, language, tier, contextId);
        return CompleteQuestion(mode, c, [], c.ChoiceTexts?.ToArray());
    }

    private ElementaryQuizContract CreateDecimalSpatialSkill(ElementaryQuizType type, AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        bool spatial = SpatialSkillTypes.Contains(type), vi = language == AppLanguage.Vietnamese;
        var t = new DifficultyBuilder(spatial ? QuizProblemKind.VisualGeometry : QuizProblemKind.Decimal, type, language, tier) { RequiresSolution = false };
        string T(string key, params (string Name, string Value)[] slots) => QuizContentCatalog.Text(language, "DecimalSpatial." + key, slots);
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        string D(decimal n) => n.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', vi ? ',' : '.');
        string context = type.ToString(), prompt;
        string[] choices;
        QuizVisualData? visual = null;
        if (spatial)
        {
            var labels = QuizContentCatalog.LoadList<string>("SpatialPracticeLabels").ToArray();
            _random.Shuffle(labels);
            var row = labels.Take(3).ToArray();
            int target = type == ElementaryQuizType.SpatialFront ? 0 : type == ElementaryQuizType.SpatialBehind ? 2 : 1;
            t.Given("target-position", target);
            t.TextAnswer(T("PositionResult"), row[target]);
            t.Explanation = T("PositionResult") + ": " + row[target];
            choices = row.Append(labels[3]).ToArray();
            prompt = T("SpatialFact", ("first", row[0]), ("second", row[1]), ("third", row[2])) + " "
                + (type == ElementaryQuizType.SpatialBetween
                    ? T("BetweenQuestion", ("first", row[0]), ("third", row[2]))
                    : T(type == ElementaryQuizType.SpatialFront ? "FrontQuestion" : "BehindQuestion", ("second", row[1])));
            visual = new("foundation-queue", row.Append(T("ForwardDirection")).ToArray(), [0, 1, 2], "",
                AccessibleDescription: T("SpatialFact", ("first", row[0]), ("second", row[1]), ("third", row[2])));
        }
        else
        {
            var profile = QuizContentCatalog.LoadList<DecimalSkillProfile>("DecimalSkillProfiles").Single(p => p.Star == (int)tier);
            int places = _random.Next(1, profile.Places + 1), denominator = (int)Math.Pow(10, places);
            int whole = _random.Next(profile.MaximumWhole + 1), fractional = _random.Next(1, denominator);
            // Include zeroes inside the fractional part, while avoiding insignificant trailing zeroes.
            if (fractional % 10 == 0) fractional++;
            int numerator = whole * denominator + fractional;
            decimal value = numerator / (decimal)denominator;
            string digits = fractional.ToString("D" + places, CultureInfo.InvariantCulture);
            string reading = DecimalReading(whole, digits, language);
            if (type != ElementaryQuizType.OrderDecimals) t.Given("decimal", value);
            t.Constant(denominator);
            string fact = T("DecimalFact", ("value", D(value)));
            switch (type)
            {
                case ElementaryQuizType.ReadDecimal:
                    t.TextAnswer(T("ReadingResult"), reading, DecimalReadingAliases(whole, digits, language));
                    t.Explanation = D(value) + ": " + reading;
                    choices = Enumerable.Range(0, 4).Select(i => DecimalReading(whole + i, digits, language)).ToArray();
                    prompt = fact + " " + T("ReadQuestion");
                    break;
                case ElementaryQuizType.WriteDecimal:
                    t.Answer(T("NumberResult"), value.ToString(CultureInfo.InvariantCulture));
                    t.Answers[0] = t.Answers[0] with { DisplayValue = D(value) };
                    t.Explanation = reading + " = " + D(value);
                    choices = Enumerable.Range(0, 4).Select(i => D(value + i)).ToArray();
                    prompt = T("ReadingFact", ("reading", reading)) + " " + T("WriteQuestion");
                    break;
                case ElementaryQuizType.DecimalPlace:
                    int index = _random.Next(places);
                    t.Given("place-index", index);
                    string place = T("Place" + index);
                    t.TextAnswer(T("DigitResult"), digits[index].ToString());
                    t.Explanation = D(value) + ": " + place + ": " + digits[index];
                    choices = Enumerable.Range(0, 4).Select(i => ((digits[index] - '0' + i) % 10).ToString()).ToArray();
                    prompt = fact + " " + T("PlaceQuestion", ("place", place));
                    break;
                case ElementaryQuizType.DecimalParts:
                    t.TextAnswer(T("WholePart"), N(whole));
                    t.TextAnswer(T("DecimalPart"), digits);
                    t.Explanation = T("WholePart") + ": " + whole + "\n" + T("DecimalPart") + ": " + digits;
                    string Join(int w, string f) => T("WholePart") + ": " + w + "; " + T("DecimalPart") + ": " + f;
                    choices = Enumerable.Range(0, 4).Select(i => Join(whole + i, digits)).ToArray();
                    prompt = fact + " " + T("PartsQuestion");
                    break;
                case ElementaryQuizType.OrderDecimals:
                    bool randomDirection = _random.Next(2) == 0;
                    bool descending = contextId.Length == 0 ? randomDirection : contextId == "decimals-descending";
                    context = descending ? "decimals-descending" : "decimals-ascending";
                    // Close values and different numbers of digits expose lexical sorting errors.
                    decimal basis = whole;
                    decimal[] values = profile.Places == 1 ? [basis + .1m, basis + .2m, basis + .3m, basis + .4m]
                        : profile.Places == 2 ? [basis + .09m, basis + .1m, basis + .11m, basis + .2m]
                        : [basis + .09m, basis + .1m, basis + .101m, basis + .11m];
                    _random.Shuffle(values);
                    for (int i = 0; i < values.Length; i++) t.Given("ordered-" + i, values[i]);
                    var sorted = values.Order().ToArray();
                    if (descending) Array.Reverse(sorted);
                    string JoinOrder(IEnumerable<decimal> a) => string.Join("; ", a.Select(D));
                    t.TextAnswer(T("OrderResult"), JoinOrder(sorted));
                    t.Explanation = JoinOrder(sorted);
                    var swap = sorted.ToArray(); (swap[0], swap[1]) = (swap[1], swap[0]);
                    var swap2 = sorted.ToArray(); (swap2[1], swap2[2]) = (swap2[2], swap2[1]);
                    choices = [JoinOrder(sorted), JoinOrder(sorted.Reverse()), JoinOrder(swap), JoinOrder(swap2)];
                    prompt = T("OrderFact", values.Select((v,i) => ("value" + i, D(v))).ToArray()) + " "
                        + T(descending ? "DescendingQuestion" : "AscendingQuestion");
                    break;
                case ElementaryQuizType.DecimalToFraction:
                    t.Answer(T("FractionResult"), numerator + "/" + denominator, denominator: denominator);
                    t.Answers[0] = t.Answers[0] with { DisplayValue = null };
                    t.Steps.Clear();
                    t.Explanation = D(value) + " = " + numerator + "/" + denominator;
                    choices = Enumerable.Range(0, 4).Select(i => (numerator + i) + "/" + denominator).ToArray();
                    prompt = fact + " " + T("ToFractionQuestion", ("denominator", N(denominator)));
                    break;
                default:
                    t.Given("numerator", numerator); t.Given("denominator", denominator);
                    t.Answer(T("NumberResult"), numerator + "/" + denominator);
                    t.Answers[0] = t.Answers[0] with { DisplayValue = D(value) };
                    t.Steps.Clear(); t.Explanation = numerator + "/" + denominator + " = " + D(value);
                    choices = Enumerable.Range(0, 4).Select(i => D(value + i)).ToArray();
                    prompt = T("FractionFact", ("numerator", N(numerator)), ("denominator", N(denominator))) + " " + T("ToDecimalQuestion");
                    break;
            }
        }
        if (contextId.Length > 0 && context != contextId) throw new ArgumentException("InvalidDecimalSpatialContext");
        return t.Build(context, prompt, visual) with { StoryContextId = context, RequiresCalculation = false, ChoiceTexts = choices };
    }

    internal static string DecimalReading(int whole, string digits, AppLanguage language)
    {
        var grammar = PracticeNumberNames.Grammar(language);
        string fractional = language == AppLanguage.Vietnamese
            ? string.Concat(digits.TakeWhile(c => c == '0').Select(_ => grammar.Digits[0] + " "))
                + PracticeNumberNames.Format(int.Parse(digits, CultureInfo.InvariantCulture), language)
            : string.Join(" ", digits.Select(c => grammar.Digits[c - '0']));
        return PracticeNumberNames.Format(whole, language) + (language == AppLanguage.Vietnamese ? " phẩy " : " point ") + fractional;
    }

    private static string[] DecimalReadingAliases(int whole, string digits, AppLanguage language)
    {
        string canonical = DecimalReading(whole, digits, language);
        var aliases = PracticeNumberNames.Aliases(canonical, language).ToList();
        if (language == AppLanguage.Vietnamese)
        {
            string byDigit = PracticeNumberNames.Format(whole, language) + " phẩy "
                + string.Join(" ", digits.Select(c => PracticeNumberNames.Grammar(language).Digits[c - '0']));
            aliases.Add(byDigit); aliases.AddRange(PracticeNumberNames.Aliases(byDigit, language));
        }
        return aliases.Distinct().ToArray();
    }
}
