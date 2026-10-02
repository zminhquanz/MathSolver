using MathSolver.Models;
using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

/// <summary>Responsive diagrams generated from the same immutable data as the grader.</summary>
public sealed class ElementaryQuizDrawable(QuizVisualData? data) : IDrawable
{
    private static readonly Color[] Series = [Color.FromArgb("#16A34A"), Color.FromArgb("#2563EB"), Color.FromArgb("#F97316")];

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (data is null || dirtyRect.Width < 80) return;
        canvas.SaveState();
        try
        {
            canvas.FontColor = ThemeResource.GetColor("WallpaperTextPrimaryColor", "#1E293B");
            canvas.StrokeColor = ThemeResource.GetColor("WallpaperTextSecondaryColor", "#64748B");
            canvas.FontSize = 14;
            canvas.StrokeSize = 2;
            float width = dirtyRect.Width, height = dirtyRect.Height;
            string Value(int index) => data.Values[index].ToString("0.##", CultureInfo.CurrentCulture) + " " + data.Unit;
            void Text(string text, float x, float y, float w, float h = 26) =>
                canvas.DrawString(text, x, y, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);

            if (data.Kind == "table")
            {
                for (int index = 0; index < data.Labels.Count; index++)
                {
                    float y = 16 + index * 64;
                    canvas.DrawRectangle(12, y, width - 24, 56);
                    canvas.DrawLine(width / 2, y, width / 2, y + 56);
                    Text(data.Labels[index], 12, y + 12, width / 2 - 12);
                    Text(Value(index), width / 2, y + 12, width / 2 - 12);
                }
            }
            else if (data.Kind == "bar")
            {
                float max = Math.Max(1, (float)data.Values.Max());
                float top = 38, baseline = height - 45, plotHeight = baseline - top;
                canvas.DrawLine(35, top, 35, baseline);
                canvas.DrawLine(35, baseline, width - 12, baseline);
                Text(data.Unit, 0, 2, width);
                float column = (width - 56) / data.Values.Count;
                for (int index = 0; index < data.Values.Count; index++)
                {
                    float x = 40 + column * index, barHeight = (float)data.Values[index] / max * (plotHeight - 24);
                    canvas.FillColor = Series[index % Series.Length];
                    canvas.FillRectangle(x + column * .2f, baseline - barHeight, column * .6f, barHeight);
                    Text(data.Values[index].ToString(CultureInfo.CurrentCulture), x, baseline - barHeight - 27, column);
                    Text(data.Labels[index], x, baseline + 6, column);
                }
            }
            else if (data.Kind == "pie")
            {
                float diameter = Math.Min(height - 100, width - 45), cx = width / 2, cy = diameter / 2 + 12;
                double angle = -Math.PI / 2, total = (double)data.Values.Sum();
                for (int index = 0; index < data.Values.Count; index++)
                {
                    double span = (double)data.Values[index] / total * 2 * Math.PI;
                    var path = new PathF(); path.MoveTo(cx, cy);
                    int slices = Math.Max(2, (int)(span * 40));
                    for (int step = 0; step <= slices; step++)
                    {
                        double a = angle + span * step / slices;
                        path.LineTo(cx + diameter / 2 * (float)Math.Cos(a), cy + diameter / 2 * (float)Math.Sin(a));
                    }
                    path.Close(); canvas.FillColor = Series[index % Series.Length]; canvas.FillPath(path);
                    angle += span;
                    float legendY = diameter + 30 + index * 22;
                    canvas.FillRectangle(14, legendY + 4, 12, 12);
                    Text(data.Labels[index] + ": " + Value(index), 32, legendY, width - 45, 22);
                }
            }
            else if (data.Kind == "clock")
            {
                float radius = Math.Min(width / 2 - 28, height / 2 - 22), cx = width / 2, cy = height / 2;
                canvas.DrawCircle(cx, cy, radius);
                for (int number = 1; number <= 12; number++)
                {
                    double a = number * Math.PI / 6 - Math.PI / 2;
                    Text(number.ToString(), cx + (radius - 19) * (float)Math.Cos(a) - 15,
                        cy + (radius - 19) * (float)Math.Sin(a) - 13, 30);
                }
                void Hand(double units, float length, float thickness)
                {
                    double angle = units * 2 * Math.PI - Math.PI / 2;
                    canvas.StrokeSize = thickness;
                    canvas.DrawLine(cx, cy, cx + length * (float)Math.Cos(angle), cy + length * (float)Math.Sin(angle));
                }
                Hand(((double)data.Values[0] % 12 + (double)data.Values[1] / 60) / 12, radius * .48f, 5);
                Hand((double)data.Values[1] / 60, radius * .74f, 3);
            }
            else if (data.Kind == "angle")
            {
                float x = width / 2, y = height / 2 - 10;
                float length = Math.Min(105, Math.Min(width - 80, height - 85) / 2);
                double rotation = (double)data.RotationDegrees * Math.PI / 180;
                double angle = (double)data.Values[0] * Math.PI / 180;
                for (int ray = 0; ray < 2; ray++)
                {
                    double direction = rotation + ray * angle;
                    float ux = (float)Math.Cos(direction), uy = -(float)Math.Sin(direction);
                    canvas.DrawLine(x, y, x + length * ux, y + length * uy);
                    if (data.Labels.Count == 3)
                        Text(data.Labels[ray == 0 ? 0 : 2], x + (length + 16) * ux - 13, y + (length + 16) * uy - 13, 26);
                }
                if (data.Values[0] == 90)
                {
                    float ux = (float)Math.Cos(rotation), uy = -(float)Math.Sin(rotation);
                    float vx = (float)Math.Cos(rotation + angle), vy = -(float)Math.Sin(rotation + angle);
                    var square = new PathF();
                    square.MoveTo(x + 15 * ux, y + 15 * uy);
                    square.LineTo(x + 15 * (ux + vx), y + 15 * (uy + vy));
                    square.LineTo(x + 15 * vx, y + 15 * vy);
                    canvas.DrawPath(square);
                }
                else
                {
                    var arc = new PathF();
                    for (int step = 0; step <= 24; step++)
                    {
                        double direction = rotation + angle * step / 24;
                        float ax = x + 25 * (float)Math.Cos(direction), ay = y - 25 * (float)Math.Sin(direction);
                        if (step == 0) arc.MoveTo(ax, ay); else arc.LineTo(ax, ay);
                    }
                    canvas.DrawPath(arc);
                }
                if (data.Labels.Count == 3)
                {
                    double opposite = rotation + angle / 2 + Math.PI;
                    Text(data.Labels[1], x + 22 * (float)Math.Cos(opposite) - 13, y - 22 * (float)Math.Sin(opposite) - 13, 26);
                }
                Text(Value(0), 0, height - 30, width);
            }
            else if (data.Kind is "parallel" or "perpendicular" or "intersecting" or "line-pairs")
            {
                IReadOnlyList<QuizVisualLine> lines = data.Lines ??
                    [new("a", 0, data.Kind == "parallel" ? -.28f : 0),
                     new("b", data.Kind == "parallel" ? 0 : data.Kind == "perpendicular" ? 90 : 45,
                         data.Kind == "parallel" ? .28f : 0)];
                float cx = width / 2, cy = height / 2;
                float radius = Math.Max(10, Math.Min(width - 60, height - 65) / 2);
                for (int index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    double angle = (double)line.DirectionDegrees * Math.PI / 180;
                    float ux = (float)Math.Cos(angle), uy = -(float)Math.Sin(angle);
                    float offset = line.OffsetRatio * radius;
                    float x = cx - uy * offset, y = cy + ux * offset;
                    float halfLength = MathF.Sqrt(Math.Max(0, radius * radius - offset * offset)) * .82f;
                    canvas.StrokeColor = Series[index % Series.Length];
                    canvas.FontColor = Series[index % Series.Length];
                    canvas.DrawLine(x - halfLength * ux, y - halfLength * uy, x + halfLength * ux, y + halfLength * uy);
                    Text(line.Label, x + (halfLength + 12) * ux - 14, y + (halfLength + 12) * uy - 13, 28);
                }
                if (lines.Count == 2 && data.Kind == "perpendicular")
                {
                    double first = (double)lines[0].DirectionDegrees * Math.PI / 180;
                    double second = (double)lines[1].DirectionDegrees * Math.PI / 180;
                    float ux = (float)Math.Cos(first), uy = -(float)Math.Sin(first);
                    float vx = (float)Math.Cos(second), vy = -(float)Math.Sin(second);
                    canvas.StrokeColor = ThemeResource.GetColor("WallpaperTextSecondaryColor", "#64748B");
                    var square = new PathF();
                    square.MoveTo(cx + 14 * ux, cy + 14 * uy);
                    square.LineTo(cx + 14 * (ux + vx), cy + 14 * (uy + vy));
                    square.LineTo(cx + 14 * vx, cy + 14 * vy);
                    canvas.DrawPath(square);
                }
            }
            else if (data.Kind == "composite")
            {
                float available = Math.Min(height - 65, width - 70), sum = (float)data.Values.Sum(), scale = available / sum;
                float first = (float)data.Values[0] * scale, second = (float)data.Values[1] * scale;
                float x = (width - available) / 2, bottom = height - 38;
                canvas.FillColor = Series[0].WithAlpha(.2f); canvas.FillRectangle(x, bottom - first, first, first);
                canvas.DrawRectangle(x, bottom - first, first, first);
                canvas.FillColor = Series[1].WithAlpha(.2f); canvas.FillRectangle(x + first, bottom - second, second, second);
                canvas.DrawRectangle(x + first, bottom - second, second, second);
                Text(Value(0), x + first / 2 - 35, bottom + 4, 70);
                Text(Value(1), x + first + second / 2 - 35, bottom + 4, 70);
            }
            else
            {
                float x = width * .2f, y = height * .22f, w = width * .55f, h = height * .55f;
                canvas.DrawRectangle(x, y, w, h);
                if (data.Values.Count > 0)
                { Text(Value(0), x, y - 30, w); Text("?", x + w + 4, y, width - x - w - 4, h); }
            }
        }
        finally { canvas.RestoreState(); }
    }
}
