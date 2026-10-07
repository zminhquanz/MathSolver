using Microsoft.Maui.Graphics;
using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Graphics;

public sealed partial class ElementaryQuizDrawable
{
    private static void DrawFoundation(ICanvas canvas, QuizVisualData visual, float width, float height)
    {
        // A uniform transform keeps circles, angle markings and rulers legible in narrow layouts.
        float scale = Math.Min(width / 360, height / 260);
        canvas.Translate((width - 360 * scale) / 2, (height - 260 * scale) / 2);
        canvas.Scale(scale, scale);
        canvas.FontSize = 15;
        canvas.StrokeSize = 2;
        void Text(string value, float x, float y, float w = 50) =>
            canvas.DrawString(value, x, y, w, 25, HorizontalAlignment.Center, VerticalAlignment.Center);
        string V(int i) => visual.Values[i].ToString(CultureInfo.InvariantCulture);
        void Dot(float x, float y, string label = "")
        {
            canvas.FillColor = Series[0]; canvas.FillCircle(x, y, 5);
            if (label.Length > 0) Text(label, x - 25, y - 30);
        }
        switch (visual.Kind)
        {
            case "foundation-count":
                for (int i = 0; i < visual.Values[0]; i++)
                { canvas.FillColor = Series[i % 3]; canvas.FillCircle(65 + i % 5 * 55, 45 + i / 5 * 45, 10); }
                break;
            case "foundation-fraction":
            {
                int n = (int)visual.Values[0], d = (int)visual.Values[1];
                float cell = 300f / d;
                for (int i = 0; i < d; i++)
                {
                    if (i < n) { canvas.FillColor = Series[0].WithAlpha(.55f); canvas.FillRectangle(30 + i * cell, 90, cell, 70); }
                    canvas.DrawRectangle(30 + i * cell, 90, cell, 70);
                }
                break;
            }
            case "foundation-number-line":
                canvas.DrawLine(25, 120, 335, 120);
                canvas.DrawLine(335, 120, 326, 113); canvas.DrawLine(335, 120, 326, 127);
                for (int i = 0; i < visual.Values.Count; i++)
                {
                    float x = 40 + i * 55;
                    canvas.DrawLine(x, 113, x, 127);
                    Text(visual.HiddenValueIndices?.Contains(i) == true ? "?" : V(i), x - 27, 132, 54);
                }
                break;
            case "foundation-position":
            {
                (float x, float y) = (int)visual.Values[0] switch { 0 => (80, 140), 1 => (280, 140), 2 => (180, 70), _ => (180, 210) };
                Dot(180, 140, "B"); Dot(x, y, "A"); break;
            }
            case "foundation-line":
                if (visual.Values[0] <= 1)
                {
                    canvas.DrawLine(50, 130, 310, 130);
                    if (visual.Values[0] == 0) { Dot(50, 130); Dot(310, 130); }
                    else
                    {
                        canvas.DrawLine(50, 130, 61, 122); canvas.DrawLine(50, 130, 61, 138);
                        canvas.DrawLine(310, 130, 299, 122); canvas.DrawLine(310, 130, 299, 138);
                    }
                }
                else
                {
                    var path = new PathF(); path.MoveTo(50, 160);
                    if (visual.Values[0] == 2) path.CurveTo(95, 10, 235, 230, 310, 80);
                    else { path.LineTo(120, 70); path.LineTo(220, 190); path.LineTo(310, 80); }
                    canvas.DrawPath(path);
                }
                break;
            case "foundation-midpoint":
            {
                float middle = 40 + 280 * (float)(visual.Values[0] / visual.Values.Sum());
                canvas.DrawLine(40, 130, 320, 130);
                Dot(40, 130, "A"); Dot(middle, 130, "M"); Dot(320, 130, "B");
                Text(V(0) + " cm", 40, 155, middle - 40); Text(V(1) + " cm", middle, 155, 320 - middle);
                break;
            }
            case "foundation-circle":
                canvas.DrawCircle(180, 135, 80);
                canvas.DrawLine(100, 135, 260, 135); canvas.DrawLine(180, 135, 180, 55);
                Dot(180, 135, "O"); Dot(100, 135, "A"); Dot(260, 135, "B"); Dot(180, 55, "C");
                break;
            case "foundation-net":
                if (visual.Values[0] == 2)
                {
                    canvas.DrawRectangle(80, 95, 200, 70); canvas.DrawCircle(180, 60, 35); canvas.DrawCircle(180, 200, 35);
                }
                else if (visual.Values[0] == 0)
                {
                    for (int i = 0; i < 4; i++) canvas.DrawRectangle(80 + i * 50, 100, 50, 50);
                    canvas.DrawRectangle(130, 50, 50, 50); canvas.DrawRectangle(130, 150, 50, 50);
                }
                else
                {
                    float x = 90;
                    foreach (float cell in new[] { 60f, 30, 60, 30 }) { canvas.DrawRectangle(x, 100, cell, 70); x += cell; }
                    canvas.DrawRectangle(150, 40, 30, 60); canvas.DrawRectangle(150, 170, 30, 60);
                }
                break;
            case "foundation-triangle":
            {
                int index = (int)visual.Values[0];
                (float ax, float ay, float bx, float by, float cx, float cy) = index switch
                {
                    1 => (70, 200, 230, 200, 70, 40),
                    2 => (70, 200, 270, 200, 170, 142.265f),
                    3 => (90, 200, 270, 200, 180, 44.115f),
                    _ => (70, 200, 270, 200, 188.48f, 58.8f)
                };
                var triangle = new PathF(); triangle.MoveTo(ax, ay); triangle.LineTo(bx, by); triangle.LineTo(cx, cy); triangle.Close();
                canvas.DrawPath(triangle);
                string[] angles = index switch { 1 => ["90°", "45°", "45°"], 2 => ["30°", "30°", "120°"], 3 => ["60°", "60°", "60°"], _ => ["50°", "60°", "70°"] };
                Text(angles[0], ax - 25, ay + 4); Text(angles[1], bx - 25, by + 4); Text(angles[2], cx - 25, cy - 28);
                if (index == 1) { canvas.DrawLine(ax + 15, ay, ax + 15, ay - 15); canvas.DrawLine(ax + 15, ay - 15, ax, ay - 15); }
                if (index == 3)
                    foreach (var edge in new[] { (ax, ay, bx, by), (bx, by, cx, cy), (cx, cy, ax, ay) })
                    {
                        float mx = (edge.Item1 + edge.Item3) / 2, my = (edge.Item2 + edge.Item4) / 2;
                        float dx = edge.Item3 - edge.Item1, dy = edge.Item4 - edge.Item2, length = MathF.Sqrt(dx * dx + dy * dy);
                        canvas.DrawLine(mx - 5 * dy / length, my + 5 * dx / length, mx + 5 * dy / length, my - 5 * dx / length);
                    }
                break;
            }
            case "foundation-ruler":
                canvas.DrawRectangle(25, 115, 310, 60);
                for (int i = 0; i <= 10; i++)
                {
                    float x = 40 + 28 * i; canvas.DrawLine(x, 115, x, 130); Text(i.ToString(), x - 12, 140, 24);
                    if (i < 10) for (int j = 1; j < 10; j++) canvas.DrawLine(x + 2.8f * j, 115, x + 2.8f * j, j == 5 ? 125 : 121);
                }
                canvas.StrokeColor = Series[0]; canvas.StrokeSize = 6;
                canvas.DrawLine(40 + 28 * (float)visual.Values[0], 95, 40 + 28 * (float)visual.Values[1], 95);
                Text("cm", 260, 185); break;
            case "foundation-protractor":
            {
                float cx = 180, cy = 205, radius = 140;
                var arc = new PathF();
                for (int degree = 0; degree <= 180; degree++)
                {
                    double rad = degree * Math.PI / 180;
                    float x = cx + radius * (float)Math.Cos(rad), y = cy - radius * (float)Math.Sin(rad);
                    if (degree == 0) arc.MoveTo(x, y); else arc.LineTo(x, y);
                    if (degree % 10 == 0)
                    {
                        canvas.DrawLine(x, y, cx + (radius - 8) * (float)Math.Cos(rad), cy - (radius - 8) * (float)Math.Sin(rad));
                        Text(degree.ToString(), cx + (radius - 24) * (float)Math.Cos(rad) - 18, cy - (radius - 24) * (float)Math.Sin(rad) - 12, 36);
                    }
                }
                canvas.DrawPath(arc); canvas.DrawLine(cx - radius, cy, cx + radius, cy);
                canvas.StrokeColor = Series[0]; canvas.StrokeSize = 3;
                double angle = (double)visual.Values[0] * Math.PI / 180;
                canvas.DrawLine(cx, cy, cx + radius * (float)Math.Cos(angle), cy - radius * (float)Math.Sin(angle));
                canvas.DrawLine(cx, cy, cx + radius, cy); break;
            }
            case "foundation-thermometer":
                canvas.DrawRoundedRectangle(130, 25, 35, 185, 16); canvas.DrawCircle(147.5f, 220, 14);
                canvas.FillColor = Series[2]; canvas.FillCircle(147.5f, 220, 10);
                canvas.FillRectangle(143, 205 - (float)visual.Values[0] * 4, 9, (float)visual.Values[0] * 4 + 15);
                for (int i = 0; i <= 40; i += 2)
                { float y = 205 - i * 4; canvas.DrawLine(170, y, i % 10 == 0 ? 190 : 179, y); if (i % 10 == 0) Text(i.ToString(), 192, y - 12, 30); }
                Text("°C", 200, 15); break;
        }
    }
}
