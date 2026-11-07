namespace MathSolver.Services;

internal enum LearningGuideKind { Clock, Fraction }
internal readonly record struct LearningGuideFrame(double HourDegrees, double MinuteDegrees, double ShadedParts);

/// <summary>Immutable teaching data from the quiz visual, never parsed from AI prose or a proposed answer.</summary>
internal sealed record LearningGuideScene(LearningGuideKind Kind, int First, int Second)
{
    public int DialHour => First % 12 == 0 ? 12 : First % 12;
    public bool HasMotion => Kind == LearningGuideKind.Clock ? Second != 0 : First != 0;
    public static LearningGuideScene? TryCreate(string kind, IReadOnlyList<decimal> values)
    {
        if (values.Count != 2 || values.Any(v => decimal.Truncate(v) != v)) return null;
        if (kind == "clock" && values[0] is >= 0 and <= 23 && values[1] is >= 0 and <= 59)
            return new(LearningGuideKind.Clock, (int)values[0], (int)values[1]);
        if (kind == "foundation-fraction" && values[1] is >= 2 and <= 12 && values[0] >= 0 && values[0] <= values[1])
            return new(LearningGuideKind.Fraction, (int)values[0], (int)values[1]);
        return null;
    }
    public LearningGuideFrame At(double progress)
    {
        double p = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 1;
        return Kind == LearningGuideKind.Clock
            ? new((First % 12 + Second * p / 60) * 30, Second * p * 6, 0)
            : new(0, 0, First * p);
    }
}
