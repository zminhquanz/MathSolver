using MathSolver.Models;
using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public sealed class QuizDiagramDrawable(QuizDiagram diagram) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 80 || dirtyRect.Height < 80) return;
        if (diagram.GeometryShape is string shape)
        {
            GeometryShapeType type = shape switch
            {
                "square" => GeometryShapeType.Square,
                "rectangle" => GeometryShapeType.Rectangle,
                "triangle" => GeometryShapeType.Triangle,
                "trapezoid" => GeometryShapeType.Trapezoid,
                "rhombus" => GeometryShapeType.Rhombus,
                "parallelogram" => GeometryShapeType.Parallelogram,
                "circle" => GeometryShapeType.Circle,
                "cube" => GeometryShapeType.Cube,
                "rectangular_prism" => GeometryShapeType.RectangularPrism,
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };
            new GeometryShapeDrawable { ShapeType = type, DimensionLabels = diagram.DimensionLabels }.Draw(canvas, dirtyRect);
            return;
        }
        canvas.SaveState();
        try
        {
            canvas.Translate(dirtyRect.X, dirtyRect.Y);
            Color accent = ThemeResource.GetColor("PrimaryColor", "#16A34A");
            Color text = ThemeResource.GetColor("WallpaperTextPrimaryColor", "#1E293B");
            canvas.StrokeColor = accent;
            canvas.StrokeSize = 2;
            canvas.FontColor = text;
            canvas.FontSize = 14;
            float width = dirtyRect.Width - 28;
            float rowHeight = dirtyRect.Height / Math.Max(1, diagram.Rows.Count);
            void Text(string value, float x, float y, float w, float h = 30) =>
                canvas.DrawString(value, x, y, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);
            void FractionLabel(int numerator, int denominator, float y, float height)
            {
                float fontSize = Math.Min(14, Math.Max(1, (height - 7) / 2));
                float fractionWidth = Math.Max(24, Math.Max(numerator.ToString().Length, denominator.ToString().Length) * fontSize * .65f);
                float x = (dirtyRect.Width - fractionWidth) / 2;
                float top = y + (height - fontSize * 2 - 5) / 2;
                canvas.FontSize = fontSize;
                Text(numerator.ToString(), x, top, fractionWidth, fontSize + 2);
                canvas.DrawLine(x, top + fontSize + 3, x + fractionWidth, top + fontSize + 3);
                Text(denominator.ToString(), x, top + fontSize + 5, fractionWidth, fontSize + 2);
                canvas.FontSize = 14;
            }
            for (int index = 0; index < diagram.Rows.Count; index++)
            {
                QuizDiagramRow row = diagram.Rows[index];
                float y = index * rowHeight + 4;
                float labelHeight = Math.Min(42, rowHeight * .4f);
                bool fractionLabel = diagram.Kind == "fractions" && row.FractionNumerator is int labelNumerator &&
                    row.FractionDenominator is int labelDenominator && row.Label == $"{labelNumerator}/{labelDenominator}";
                if (fractionLabel) FractionLabel(row.FractionNumerator!.Value, row.FractionDenominator!.Value, y, labelHeight);
                else Text(row.Label, 14, y, width, labelHeight);
                float top = y + labelHeight + 6;
                if (diagram.Kind == "fractions")
                {
                    // Cap density, never silently truncate an improper fraction into a proper one.
                    if (row.FractionNumerator is not int n || row.FractionDenominator is not int d || d <= 0 ||
                        n < 0 || d > 32 || n > d * 6)
                    {
                        if (!fractionLabel) Text(row.Label, 14, top, width);
                        continue;
                    }
                    int wholes = Math.Max(1, (n + d - 1) / d);
                    float barHeight = Math.Min(20, (rowHeight - 54) / wholes - 3);
                    for (int whole = 0; whole < wholes; whole++)
                    for (int part = 0; part < d; part++)
                    {
                        float x = 14 + width * part / d, w = width / d;
                        float cellY = top + whole * (barHeight + 3);
                        canvas.FillColor = accent.WithAlpha(whole * d + part < n ? .65f : .08f);
                        canvas.FillRectangle(x, cellY, w, barHeight);
                        canvas.DrawRectangle(x, cellY, w, barHeight);
                    }
                }
                else if (row.Direction != 0)
                {
                    float left = 28, right = dirtyRect.Width - 28, lineY = top + 16;
                    canvas.DrawLine(left, lineY, right, lineY);
                    float tip = row.Direction > 0 ? right : left;
                    canvas.DrawLine(tip, lineY, tip - row.Direction * 12, lineY - 8);
                    canvas.DrawLine(tip, lineY, tip - row.Direction * 12, lineY + 8);
                    // Movement is schematic; arrow lengths do not encode computed travel distances.
                    foreach (QuizDiagramSegment segment in row.Segments) Text(segment.Text, 14, lineY + 10, width);
                }
                else if (row.Segments.Count > 0)
                {
                    float maxParts = diagram.Rows.Max(item => item.Segments.Sum(segment => segment.Parts));
                    // Keep the complete bar, text and stroke inside its row,
                    // including bottom padding on the last row of a three-row diagram.
                    float barHeight = Math.Min(42, Math.Max(1, rowHeight - labelHeight - 18));
                    float x = 14;
                    foreach (QuizDiagramSegment segment in row.Segments)
                    {
                        float segmentWidth = width * segment.Parts / Math.Max(1, maxParts);
                        canvas.FillColor = accent.WithAlpha(segment.Highlight ? .3f : .1f);
                        canvas.FillRectangle(x, top, segmentWidth, barHeight);
                        canvas.DrawRectangle(x, top, segmentWidth, barHeight);
                        Text(segment.Text, x + 2, top + 2, segmentWidth - 4, Math.Max(1, barHeight - 4));
                        x += segmentWidth;
                    }
                }
            }
        }
        finally { canvas.RestoreState(); }
    }
}
