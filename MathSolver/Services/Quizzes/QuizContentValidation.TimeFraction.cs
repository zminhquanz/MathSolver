using System.Text.Json;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckTimeFractionLists(QuizContentPack pack)
    {
        if (pack.Lists.TryGetValue("FractionOrderPracticeProfiles", out var profiles))
        {
            var ids = new HashSet<string>();
            var coveredStars = new HashSet<int>();
            foreach (var row in profiles.EnumerateArray())
            {
                var p = row.Deserialize<FractionOrderPracticeProfile>() ?? throw new InvalidDataException("Invalid fraction ordering profile.");
                if (!ids.Add(p.Id) || string.IsNullOrWhiteSpace(p.Id) || p.MinimumStar < 1 || p.MaximumStar > 5 || p.MaximumStar < p.MinimumStar
                    || p.Count is < 3 or > 4 || p.Denominators is null || p.Denominators.Length != p.Count
                    || p.Denominators.Any(d => d is < 2 or > 100) || p.Denominators.Distinct().Count() != p.Count
                    || !p.Denominators.SequenceEqual(p.Denominators.Order()))
                    throw new InvalidDataException("Invalid fraction ordering profile: " + p.Id);
                foreach (int star in Enumerable.Range(p.MinimumStar, p.MaximumStar - p.MinimumStar + 1)) coveredStars.Add(star);
            }
            if (coveredStars.Count != 5) throw new InvalidDataException("Missing fraction ordering difficulty.");
        }
        if (pack.Lists.TryGetValue("WeekdayPracticeDates", out var dates))
        {
            if (dates.GetArrayLength() == 0) throw new InvalidDataException("Missing weekday dates.");
            foreach (var row in dates.EnumerateArray())
            {
                var d = row.Deserialize<WeekdayPracticeDate>() ?? throw new InvalidDataException("Invalid weekday date.");
                try { _ = new DateTime(d.Year, d.Month, d.Day); }
                catch (ArgumentOutOfRangeException e) { throw new InvalidDataException("Invalid weekday date.", e); }
            }
        }
        if (pack.Lists.TryGetValue("CenturyPracticeYears", out var years)
            && (years.GetArrayLength() == 0 || years.EnumerateArray().Any(y => !y.TryGetInt32(out int n) || n is < 1 or > 9999)))
            throw new InvalidDataException("Invalid century practice years.");
    }
}
