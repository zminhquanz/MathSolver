using System.Globalization;
using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>One mask for the drawing, visible data caption and screen-reader description.</summary>
public static class QuizChartPresentation
{
    public static bool IsChart(QuizVisualData visual) => visual.Kind is "table" or "bar" or "pie";

    public static QuizVisualData ForDisplay(QuizVisualData visual, bool revealSolution)
    {
        if (!IsChart(visual)) return revealSolution ? visual with { HiddenValueIndices = null } : visual;
        decimal[] displayed = visual.Values.ToArray();
        if (!revealSolution && visual.HiddenValueIndices is { Count: > 0 } hidden)
        {
            // Pie angles use only the visible percentages and the known 100% total.
            // Multiple unknown sectors share the remainder as a schematic placeholder.
            decimal remainder = visual.Kind == "pie"
                ? Math.Max(0, 100 - displayed.Where((_, i) => !hidden.Contains(i)).Sum()) / hidden.Count : 0;
            foreach (int index in hidden)
                if (index >= 0 && index < displayed.Length) displayed[index] = remainder;
        }
        return visual with { Values = Array.AsReadOnly(displayed),
            HiddenValueIndices = revealSolution ? null : visual.HiddenValueIndices,
            AccessibleDescription = null, Annotations = null };
    }

    public static string DescribeValues(QuizVisualData visual, bool revealSolution) =>
        string.Join(" · ", visual.Labels.Select((label, index) => $"{label}: "
            + (!revealSolution && visual.HiddenValueIndices?.Contains(index) == true ? "?"
                : visual.Values[index].ToString(CultureInfo.CurrentCulture)) + " " + visual.Unit));
}
