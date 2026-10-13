using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>Practice/PDF ranges. Bank preview generation retains QuizCurriculumLayer's existing policy.</summary>
public static partial class PracticeArithmeticDifficulty
{
    public static int Maximum(CurriculumTier tier, PracticeArithmeticStyle? style = null) => tier switch
    {
        CurriculumTier.FourStars when style == PracticeArithmeticStyle.Mental => 999_999_999,
        CurriculumTier.FourStars => 99_999_999,
        CurriculumTier.FiveStars => 999_999_999,
        _ => QuizCurriculumLayer.GetMaximumOperandValue(tier)
    };

    public static string Prompt(PracticeArithmeticStyle style, ArithmeticQuizMode mode, AppLanguage language) =>
        QuizContentCatalog.Text(language, $"PracticeArithmetic.{style}.{mode}");

    public static int MoneyMaximum(CurriculumTier tier, AppLanguage language)
    {
        if (!Enum.IsDefined(tier)) throw new ArgumentOutOfRangeException(nameof(tier));
        if (!Enum.IsDefined(language)) throw new ArgumentOutOfRangeException(nameof(language));
        return language == AppLanguage.Vietnamese
            ? new[] { 20_000, 50_000, 100_000, 99_999_999, 999_999_999 }[(int)tier - 1]
            : new[] { 10, 30, 100, 10_000, 100_000 }[(int)tier - 1];
    }

    // Select a digit band first: high stars continue to include smaller facts.
    public static int NextPrimary(Random random, CurriculumTier tier, int? maximum = null)
    {
        int minimum = QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier);
        int cap = Math.Min(Maximum(tier), maximum ?? int.MaxValue);
        if (cap < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (cap < minimum) minimum = 1; // Real-world context capacities take priority.
        var bands = new List<(int Min, int Max)>();
        for (long lower = minimum; lower <= cap; lower *= 10)
            bands.Add(((int)lower, (int)Math.Min(cap, lower * 10 - 1)));
        var band = bands[random.Next(bands.Count)];
        return random.Next(band.Min, band.Max + 1);
    }

    public static int NextSecondary(Random random, CurriculumTier tier, int? maximum = null)
    {
        int cap = Math.Min(Maximum(tier), maximum ?? int.MaxValue);
        if (cap < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        var bands = new List<(int Min, int Max)>();
        for (long lower = 1; lower <= cap; lower *= 10)
            bands.Add(((int)lower, (int)Math.Min(cap, lower * 10 - 1)));
        var band = bands[random.Next(bands.Count)];
        return random.Next(band.Min, band.Max + 1);
    }

    public static int NextMultiple(Random random, CurriculumTier tier, int divisor)
    {
        if (divisor < 1) throw new ArgumentOutOfRangeException(nameof(divisor));
        int sample = NextPrimary(random, tier);
        int minimum = QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier);
        int quotient = Math.Max((minimum + divisor - 1) / divisor, sample / divisor);
        if (quotient > Maximum(tier) / divisor) throw new ArgumentOutOfRangeException(nameof(divisor));
        return quotient * divisor;
    }

    /// <summary>Count carries/borrows from right to left; four stars follow the grade-four restriction.</summary>
    public static bool HasAppropriateRegrouping(int left, int right, ArithmeticOperation operation, CurriculumTier tier)
    {
        if (tier != CurriculumTier.FourStars || operation is not (ArithmeticOperation.Add or ArithmeticOperation.Subtract)) return true;
        int incoming = 0, count = 0; bool previous = false;
        while (left > 0 || right > 0)
        {
            bool current = operation == ArithmeticOperation.Add ? left % 10 + right % 10 + incoming >= 10
                : left % 10 - incoming < right % 10;
            if (current && (previous || ++count > 3)) return false;
            incoming = current ? 1 : 0; previous = current; left /= 10; right /= 10;
        }
        return true;
    }

    public static int NextRegroupingPartner(Random random, CurriculumTier tier, int left, ArithmeticOperation operation, int maximum)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            int right = NextSecondary(random, tier, maximum);
            if (HasAppropriateRegrouping(left, right, operation, tier)) return right;
        }
        // Alter only the leading digit: at most one carry, and no borrow.
        int leadingPlace = 1;
        for (int n = left; n >= 10; n /= 10) leadingPlace *= 10;
        if (leadingPlace <= maximum) return leadingPlace;
        return 0; // Caller can round the primary datum and use one.
    }
}
