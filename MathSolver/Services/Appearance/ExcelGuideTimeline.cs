namespace MathSolver.Services;

/// <summary>Decorative writing, never data entry or import progress. Coordinates share the source image's canvas.</summary>
internal static class ExcelGuideTimeline
{
    public const uint DurationMilliseconds = 1800;
    public const double PencilTipX = .568, PencilTipY = .580;
    public static double FirstX(double p) => .510 + .055 * p;
    public static double FirstY(double p) => .575 - .016 * p;
    public static double SecondX(double p) => .515 + .055 * p;
    public static double SecondY(double p) => .605 - .016 * p;

    public static ExcelGuideFrame At(double progress)
    {
        double p = double.IsNaN(progress) ? 0 : Math.Clamp(progress, 0, 1);
        double first = Ease(Phase(p, .12, .38)), second = Ease(Phase(p, .49, .75));
        double x, y;
        if (p < .38) { x = FirstX(first); y = FirstY(first); }
        else if (p < .49)
        {
            double move = Ease(Phase(p, .38, .49));
            x = FirstX(1) + (SecondX(0) - FirstX(1)) * move;
            y = FirstY(1) + (SecondY(0) - FirstY(1)) * move - .015 * Math.Sin(move * Math.PI);
        }
        else { x = SecondX(second); y = SecondY(second) - .018 * Ease(Phase(p, .75, .85)); }
        double poster = Phase(p, .88, 1);
        return new(x, y, first, second, Phase(p, 0, .10) * (1 - Phase(p, .80, .88)), poster);
    }

    private static double Phase(double p, double start, double end) => Math.Clamp((p - start) / (end - start), 0, 1);

    // Icons8 Motion's ease-in-out: cubic-bezier(.77, 0, .175, 1).
    internal static double Ease(double x)
    {
        if (x is <= 0 or >= 1) return x;
        double low = 0, high = 1;
        for (int i = 0; i < 24; i++)
        {
            double t = (low + high) / 2, inv = 1 - t;
            double curveX = 3 * inv * inv * t * .77 + 3 * inv * t * t * .175 + t * t * t;
            if (curveX < x) low = t; else high = t;
        }
        double u = (low + high) / 2;
        return 3 * (1 - u) * u * u + u * u * u;
    }
}

internal readonly record struct ExcelGuideFrame(double TipX, double TipY, double FirstInk, double SecondInk,
    double PencilOpacity, double PosterOpacity);
