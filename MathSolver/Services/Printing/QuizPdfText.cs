using PdfSharp.Drawing;
using System.Text.RegularExpressions;
using static MathSolver.Services.TextbookFractionParser;

namespace MathSolver.Services.Printing;

/// <summary>Paragraph wrapping with atomic textbook fractions and nested expression layout.</summary>
internal static class QuizPdfText
{
    internal sealed record Box(double Width, double Height, Action<XGraphics, double, double> Paint);
    internal sealed record Line(IReadOnlyList<Box> Boxes, double Width, double Height);
    private static Box Plain(XGraphics g, string value, XFont font)
    {
        double height = font.Size * 1.4, width = g.MeasureString(value, font).Width;
        return new(width, height, (canvas, x, y) => canvas.DrawString(value, font, XBrushes.Black,
            new XRect(x, y, width + .1, height), XStringFormats.TopLeft));
    }
    private static Box Join(params Box[] children)
    {
        double height = children.Max(c => c.Height);
        return new(children.Sum(c => c.Width), height, (g, x, y) =>
        {
            foreach (var child in children) { child.Paint(g, x, y + (height - child.Height) / 2); x += child.Width; }
        });
    }
    private static Box MathBox(XGraphics g, Node node, XFont font, int depth = 0)
    {
        switch (node)
        {
            case Number number: return Plain(g, number.Text, font);
            case Unary unary: return Join(Plain(g, unary.Operator, font), MathBox(g, unary.Content, font, depth));
            case TextbookFractionParser.Group group: return Join(Plain(g, "(", font), MathBox(g, group.Content, font, depth), Plain(g, ")", font));
            case Mixed mixed: return Join(Plain(g, mixed.Whole.Text + " ", font), MathBox(g, new Binary("/", mixed.Numerator, mixed.Denominator), font, depth));
            case Binary { Operator: "/" } fraction:
                var smaller = new XFont("QuizPrint", Math.Max(7, font.Size * .9), font.Style);
                var numerator = MathBox(g, fraction.Left, smaller, depth + 1);
                var denominator = MathBox(g, fraction.Right, smaller, depth + 1);
                double width = Math.Max(numerator.Width, denominator.Width) + 6, height = numerator.Height + denominator.Height + 4;
                return new(width, height, (canvas, x, y) =>
                {
                    numerator.Paint(canvas, x + (width - numerator.Width) / 2, y);
                    canvas.DrawLine(new XPen(XColors.Black, .7), x, y + numerator.Height + 2, x + width, y + numerator.Height + 2);
                    denominator.Paint(canvas, x + (width - denominator.Width) / 2, y + numerator.Height + 4);
                });
            case Binary binary: return Join(MathBox(g, binary.Left, font, depth), Plain(g, " " + binary.Operator.Replace("*", "×") + " ", font), MathBox(g, binary.Right, font, depth));
            default: throw new InvalidOperationException("Unsupported print expression.");
        }
    }
    internal static IReadOnlyList<Line> Wrap(XGraphics g, string text, XFont font, double width)
    {
        var result = new List<Line>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            var boxes = new List<Box>(); double used = 0;
            void Flush()
            {
                result.Add(new(boxes.ToArray(), used, boxes.Count > 0 ? boxes.Max(b => b.Height) + 3 : font.Size * 1.4));
                boxes.Clear(); used = 0;
            }
            void Add(Box box)
            {
                if (used + box.Width > width && boxes.Count > 0) Flush();
                if (box.Width > width)
                {
                    double scale = width / box.Width;
                    var original = box;
                    box = new(width, box.Height * scale, (canvas, x, y) =>
                    { var state = canvas.Save(); canvas.TranslateTransform(x, y); canvas.ScaleTransform(scale); original.Paint(canvas, 0, 0); canvas.Restore(state); });
                }
                boxes.Add(box); used += box.Width;
            }
            foreach (var fragment in ParseLine(paragraph))
            {
                if (fragment.Math is { } math)
                {
                    // The same display tree is used by FractionExpressionView.
                    Add(MathBox(g, math, font)); continue;
                }
                foreach (Match match in Regex.Matches(fragment.Text, @"\S+\s*|\s+")) Add(Plain(g, match.Value, font));
            }
            if (boxes.Count > 0 || result.Count == 0 || paragraph.Length == 0) Flush();
        }
        return result;
    }
    internal static void DrawLine(XGraphics graphics, Line line, double x, double y)
    {
        foreach (var box in line.Boxes) { box.Paint(graphics, x, y + (line.Height - 3 - box.Height) / 2); x += box.Width; }
    }
}
