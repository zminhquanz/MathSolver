using MathSolver.Testing;
using static MathSolver.Services.ReadingColumnPolicy;

internal static class ReadingColumnTests
{
    internal static void Run()
    {
        TestAssert.That(Width(2000, 1320, 40, true) == 1280, "PDF's inset must be subtracted after its viewport cap.");
        TestAssert.That(Width(2000, 1160, 48, false) == 1160, "Review's reading cap must apply after its margins.");
        TestAssert.That(Width(1176, 1160, 48, false) == 1128, "Review changed below its maximum reading width.");
        TestAssert.That(Width(1320, 1320, 32, true) == 1288, "PDF changed at its cap boundary.");
        TestAssert.That(Width(2000, 600, 0, false) == 600, "A comparison column lost its independent cap.");
        foreach (double viewport in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            TestAssert.That(Width(viewport, 1320, 40, true) == 280, "PDF initial/unbounded measurement must use the 320-DIP fallback.");
            TestAssert.That(Width(viewport, 1160, 48, false) == 272, "Review initial/unbounded measurement must use the 320-DIP fallback.");
        }
        foreach (double viewport in new[] { 1d, 16d, 32d, 48d })
        {
            TestAssert.That(Width(viewport, 1320, 64, true) == 1, "PDF requested a nonpositive native width.");
            TestAssert.That(Width(viewport, 1160, 64, false) == 1, "Review requested a nonpositive native width.");
        }
        Console.WriteLine("PASS shared reading columns: distinct PDF/review cap order, comparison caps, narrow bounds and initial/nonfinite measurements.");
    }
}
