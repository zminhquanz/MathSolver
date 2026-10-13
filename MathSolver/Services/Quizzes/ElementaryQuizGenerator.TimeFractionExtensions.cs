using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

public sealed record FractionOrderPracticeProfile(string Id, int MinimumStar, int MaximumStar, int[] Denominators, int Count);
public sealed record WeekdayPracticeDate(int Year, int Month, int Day);

public sealed partial class ElementaryQuizGenerator
{
    public static int CenturyOfYear(int year) => year is >= 1 and <= 9999
        ? (year - 1) / 100 + 1 : throw new ArgumentOutOfRangeException(nameof(year));
    public static ElementaryQuizType[] TimeBankTypes => [.. TimeStoryTypes,
        ElementaryQuizType.SecondsConversion, ElementaryQuizType.YearCentury, ElementaryQuizType.CalendarWeekday];
    public static ElementaryQuizType[] FractionQuantityBankTypes => [.. FractionQuantityStoryTypes, ElementaryQuizType.OrderRelatedFractions];
    public static bool IsTimeFractionExtension(ElementaryQuizType type) => type is ElementaryQuizType.SecondsConversion
        or ElementaryQuizType.YearCentury or ElementaryQuizType.CalendarWeekday or ElementaryQuizType.OrderRelatedFractions;
    internal static string[] TimeFractionContextIds(ElementaryQuizType type) => type switch
    {
        ElementaryQuizType.SecondsConversion => ["seconds-forward", "seconds-reverse"],
        ElementaryQuizType.CalendarWeekday => ["weekday-forward", "weekday-backward"],
        ElementaryQuizType.YearCentury => ["year-century"],
        ElementaryQuizType.OrderRelatedFractions => ["fractions-ascending", "fractions-descending"],
        _ => throw new ArgumentException("InvalidTimeFractionProfile")
    };

    public ArithmeticQuizQuestion GenerateTimeFractionExtension(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        if (!IsTimeFractionExtension(type) || !Enum.IsDefined(tier)) throw new ArgumentException("InvalidTimeFractionProfile");
        var contract = CreateTimeFractionExtension(type, language, tier, contextId);
        return CompleteQuestion(mode, contract, [], contract.ChoiceTexts?.ToArray());
    }

    private ElementaryQuizContract CreateTimeFractionExtension(ElementaryQuizType type, AppLanguage language,
        CurriculumTier tier, string contextId = "")
    {
        var kind = type == ElementaryQuizType.OrderRelatedFractions ? QuizProblemKind.FractionSkills : QuizProblemKind.Time;
        var t = new DifficultyBuilder(kind, type, language, tier) { RequiresSolution = false };
        string T(string key, params (string Name, string Value)[] slots) => QuizContentCatalog.Text(language, "TimeFraction." + key, slots);
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        string prompt, context;
        string[]? choices = null;
        if (type == ElementaryQuizType.SecondsConversion)
        {
            bool randomDirection = _random.Next(2) == 0;
            bool toSeconds = contextId.Length == 0 ? randomDirection : contextId == "seconds-forward";
            context = toSeconds ? "seconds-forward" : "seconds-reverse";
            int minutes = _random.Next(1, (int)tier * 12 + 1);
            t.Constant(60);
            string supplied = t.Given("duration", toSeconds ? minutes : minutes * 60, T(toSeconds ? "Minutes" : "Seconds"));
            string targetUnit = T(toSeconds ? "Seconds" : "Minutes");
            t.Answer(T("DurationResult"), supplied + (toSeconds ? "*60" : "/60"), targetUnit);
            prompt = T("SecondsFact", ("amount", supplied), ("unit", T(toSeconds ? "Minutes" : "Seconds"))) + " "
                + T("SecondsQuestion", ("unit", targetUnit));
        }
        else if (type == ElementaryQuizType.YearCentury)
        {
            context = "year-century";
            int[] years = QuizContentCatalog.LoadList<int>("CenturyPracticeYears").ToArray();
            int century = _random.Next(10, 22);
            int year = _random.Next(3) == 0 ? years[_random.Next(years.Length)] : (century - 1) * 100 + _random.Next(1, 101);
            century = CenturyOfYear(year);
            t.Given("year", year);
            t.Answer(T("CenturyResult"), N(century));
            t.Explanation = T("CenturyExplanation", ("first", N((century - 1) * 100 + 1)), ("last", N(century * 100)), ("century", N(century)));
            prompt = T("CenturyFact", ("year", N(year))) + " " + T("CenturyQuestion");
        }
        else if (type == ElementaryQuizType.CalendarWeekday)
        {
            bool randomDirection = _random.Next(2) == 0;
            bool forward = contextId.Length == 0 ? randomDirection : contextId == "weekday-forward";
            context = forward ? "weekday-forward" : "weekday-backward";
            var dates = QuizContentCatalog.LoadList<WeekdayPracticeDate>("WeekdayPracticeDates");
            var seed = dates[_random.Next(dates.Count)];
            var date = new DateTime(seed.Year, seed.Month, seed.Day);
            int advance = _random.Next(1, (int)tier * 7 + 1);
            DateTime target = date.AddDays(forward ? advance : -advance);
            string[] names = Enumerable.Range(0, 7).Select(i => T("Weekday" + i)).ToArray();
            int initial = ((int)date.DayOfWeek + 6) % 7, final = ((int)target.DayOfWeek + 6) % 7;
            t.Given("year", seed.Year); t.Given("month", seed.Month); t.Given("day", seed.Day);
            t.Given("weekday", initial); t.Given("elapsed-days", advance);
            string answer = names[final];
            t.TextAnswer(T("WeekdayResult"), answer,
                language == AppLanguage.Vietnamese ? final < 6 ? "Thứ " + (final + 2) : "CN" : answer);
            choices = names.Where((_, i) => i != final).OrderBy(_ => _random.Next()).Take(3).Prepend(answer).ToArray();
            t.Explanation = T("WeekdayExplanation", ("date", target.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)), ("weekday", answer));
            prompt = T("WeekdayFact", ("date", date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)), ("weekday", names[initial]))
                + " " + T(forward ? "WeekdayAfter" : "WeekdayBefore", ("days", N(advance)));
        }
        else
        {
            bool randomDirection = _random.Next(2) == 0;
            bool descending = contextId.Length == 0 ? randomDirection : contextId == "fractions-descending";
            context = descending ? "fractions-descending" : "fractions-ascending";
            var profiles = QuizContentCatalog.LoadList<FractionOrderPracticeProfile>("FractionOrderPracticeProfiles")
                .Where(p => p.MinimumStar <= (int)tier && p.MaximumStar >= (int)tier).ToArray();
            var profile = profiles[_random.Next(profiles.Length)];
            var fractions = new List<(int Numerator, int Denominator, ReducedFraction Value)>();
            foreach (int denominator in profile.Denominators.Take(profile.Count))
            {
                var available = Enumerable.Range(1, denominator - 1).Where(n =>
                    fractions.All(f => f.Value != new ReducedFraction(n, denominator))).ToArray();
                int numerator = available.Length == 0 ? 1 : available[_random.Next(available.Length)];
                fractions.Add((numerator, denominator, new(numerator, denominator)));
            }
            if (fractions.Select(f => f.Value).Distinct().Count() != profile.Count)
                throw new InvalidOperationException("AmbiguousFractionOrderProfile");
            for (int i = fractions.Count - 1; i > 0; i--)
            { int j = _random.Next(i + 1); (fractions[i], fractions[j]) = (fractions[j], fractions[i]); }
            for (int i = 0; i < fractions.Count; i++)
            { t.Given("numerator-" + i, fractions[i].Numerator); t.Given("denominator-" + i, fractions[i].Denominator); }
            string F((int Numerator, int Denominator, ReducedFraction Value) f) => $"{f.Numerator}/{f.Denominator}";
            var sorted = fractions.OrderBy(f => f.Value, Comparer<ReducedFraction>.Create((a,b) =>
                (a.Numerator * b.Denominator).CompareTo(b.Numerator * a.Denominator))).ToArray();
            if (descending) Array.Reverse(sorted);
            string Join(IEnumerable<(int Numerator, int Denominator, ReducedFraction Value)> items) => string.Join(" -> ", items.Select(F));
            var wrong = new List<string> { Join(sorted.Reverse()) };
            for (int i = 1; i < sorted.Length; i++)
            { var copy = sorted.ToArray(); (copy[0], copy[i]) = (copy[i], copy[0]); wrong.Add(Join(copy)); }
            var adjacent = sorted.ToArray();
            (adjacent[1], adjacent[2]) = (adjacent[2], adjacent[1]);
            wrong.Add(Join(adjacent));
            string answer = Join(sorted);
            t.TextAnswer(T("OrderResult"), answer);
            choices = wrong.Prepend(answer).Distinct().Take(4).ToArray();
            int common = fractions.Aggregate(1, (lcm,f) => lcm / (int)BigInteger.GreatestCommonDivisor(lcm, f.Denominator) * f.Denominator);
            t.Explanation = T("CommonDenominatorExplanation", ("denominator", N(common))) + "\n"
                + string.Join(", ", fractions.Select(f => F(f) + " = " + f.Numerator * (common / f.Denominator) + "/" + common))
                + "\n" + string.Join(descending ? " > " : " < ", sorted.Select(F));
            prompt = T("FractionFact" + fractions.Count, fractions.Select((f,i) => ("fraction" + i, F(f))).ToArray())
                + " " + T(descending ? "FractionDescending" : "FractionAscending");
        }
        if (contextId.Length > 0 && context != contextId) throw new ArgumentException("InvalidTimeFractionContext");
        return t.Build(context, prompt) with { StoryContextId = context, RequiresCalculation = false, ChoiceTexts = choices };
    }
}
