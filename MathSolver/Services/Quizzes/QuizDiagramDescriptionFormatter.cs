using System.Globalization;
using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>Describe presented labels only. Hidden dimensions and answers remain unknown before grading.</summary>
internal static class QuizDiagramDescriptionFormatter
{
    internal static string Format(QuizDiagram? diagram, QuizVisualData? visual, bool revealSolution, AppLanguage language)
    {
        var parts = new List<string>();
        bool vi = language == AppLanguage.Vietnamese;
        if (visual is not null)
        {
            if (visual.AccessibleDescription is { } accessible)
                return AccessibleMathText.Format(accessible, language);
            parts.AddRange(visual.Labels);
            if (visual.Kind is "table" or "bar" or "pie" or "composite" or "rectangle" or "angle")
            {
                for (int i = 0; i < visual.Values.Count; i++)
                {
                    string label = i < visual.Labels.Count ? visual.Labels[i] : "";
                    string value = !revealSolution && visual.HiddenValueIndices?.Contains(i) == true
                        ? "?" : visual.Values[i].ToString(CultureInfo.CurrentCulture);
                    parts.Add($"{label}: {value} {visual.Unit}");
                }
            }
            if (visual.Kind == "pictograph" && visual.PictographKey is > 0)
            {
                parts.Add(QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.001") + visual.PictographKey + " " + visual.Unit);
                for (int row = 0; row < visual.Labels.Count; row++)
                    parts.Add(visual.Labels[row] + ": " + visual.Values[row] / visual.PictographKey + QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.002"));
            }
            if (visual.Kind == "shape")
                parts.Add(visual.ScenarioId switch
                {
                    "circle" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.003"),
                    "triangle" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.004"),
                    "square" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.005"),
                    "rectangle" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.006"),
                    "trapezoid" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.007"),
                    "parallelogram" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.008"),
                    "rhombus" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.009"),
                    "cube" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.010"),
                    "cuboid" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.011"),
                    "cylinder" => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.012"),
                    _ => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.013")
                });
            if (visual.Annotations is { } annotations) parts.AddRange(annotations.Select(item => item.Text));
            if (visual.Polygons is { } polygons)
                parts.AddRange(polygons.Select(polygon => polygon.Label + " " + string.Join("–", polygon.Vertices.Select(point => point.Label))));
            if (visual.Lines is { } lines)
                parts.AddRange(lines.Select(line => QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.014") + $" {line.Label}: " +
                    line.DirectionDegrees.ToString(CultureInfo.CurrentCulture) + " " +
                    QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.015")));
            if (visual.Kind == "clock" && visual.Values.Count >= 2)
            {
                if (!revealSolution && visual.HiddenValueIndices is { Count: > 0 }) parts.Add("?");
                else
                {
                    // Describe hand positions, not a calculated end time or an answer.
                    decimal hourPosition = visual.Values[0] % 12 + visual.Values[1] / 60;
                    decimal minutePosition = visual.Values[1] / 5;
                    string Position(decimal position)
                    {
                        int lower = (int)decimal.Floor(position) % 12;
                        int upper = (lower + 1) % 12;
                        string first = (lower == 0 ? 12 : lower).ToString(CultureInfo.CurrentCulture);
                        string second = (upper == 0 ? 12 : upper).ToString(CultureInfo.CurrentCulture);
                        return position == decimal.Floor(position) ? QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.016") + first
                            : QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.017") + first + QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.018") + second;
                    }
                    parts.Add(QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.019") + Position(hourPosition));
                    parts.Add(QuizContentCatalog.Text(language, "QuizDiagramDescriptionFormatter.Format.020") + Position(minutePosition));
                }
            }
        }
        else if (diagram is not null)
        {
            parts.Add(diagram.Caption);
            if (diagram.DimensionLabels is { } dimensions)
                parts.AddRange(dimensions.Select(pair => $"{pair.Key}: {pair.Value}"));
            foreach (var row in diagram.Rows)
            {
                parts.Add(row.Label + ": " + string.Join("; ", row.Segments.Select(segment => segment.Text)));
                if (row.FractionNumerator is { } numerator && row.FractionDenominator is { } denominator)
                    parts.Add($"{numerator}/{denominator}");
            }
            if (revealSolution && diagram.Explanation is { } explanation) parts.Add(explanation);
        }
        return AccessibleMathText.Format(string.Join(". ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Distinct()), language);
    }
}
