using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

internal sealed partial class QuizChoiceDrawable(string id, Color ink) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 0) return;
        canvas.SaveState();
        try
        {
            canvas.Translate((dirtyRect.Width - size) / 2, (dirtyRect.Height - size) / 2);
            canvas.Scale(size / 100, size / 100);
            canvas.StrokeColor = canvas.FillColor = canvas.FontColor = ink;
            canvas.StrokeSize = 3;
            canvas.FontSize = 22;
            void Label(string text, float y = 34) => canvas.DrawString(text, 5, y, 90, 32,
                HorizontalAlignment.Center, VerticalAlignment.Center);
            void Polygon(params PointF[] points)
            {
                var path = new PathF();
                path.MoveTo(points[0]);
                foreach (var point in points.Skip(1)) path.LineTo(point);
                path.Close();
                canvas.DrawPath(path);
            }
            void Bars()
            {
                for (int i = 0; i < 3; i++) canvas.FillRectangle(17 + i * 24, 65 - i * 17, 15, 20 + i * 17);
                canvas.DrawLine(10, 87, 91, 87);
            }
            if (DrawSkill(canvas)) return;
            switch (id)
            {
                case "fraction":
                    for (int i = 0; i < 4; i++)
                    {
                        if (i < 3) canvas.FillRectangle(10 + 20 * i, 42, 20, 24);
                        canvas.DrawRectangle(10 + 20 * i, 42, 20, 24);
                    }
                    canvas.FontSize = 18; Label("3/4", 8);
                    break;
                case "find-x":
                case "find-x-sum": Label("𝑥 + 3", 19); Label("= 8", 51); break;
                case "find-x-difference": Label("8 − 𝑥", 19); Label("= 3", 51); break;
                case "find-x-minuend": Label("𝑥 − 3", 19); Label("= 5", 51); break;
                case "find-x-product": Label("𝑥 × 3", 19); Label("= 12", 51); break;
                case "find-x-quotient": Label("12 ÷ 𝑥", 19); Label("= 4", 51); break;
                case "find-x-dividend": Label("𝑥 ÷ 3", 19); Label("= 4", 51); break;
                case "arithmetic-add": Label("2 + 3", 19); Label("= 5", 51); break;
                case "arithmetic-subtract": Label("5 − 2", 19); Label("= 3", 51); break;
                case "arithmetic-multiply": Label("2 × 3", 19); Label("= 6", 51); break;
                case "arithmetic-divide": Label("6 ÷ 2", 19); Label("= 3", 51); break;
                case "expression": Label("(2 + 3)", 19); Label("× 4", 51); break;
                case "geometry-square": canvas.DrawRectangle(18, 18, 64, 64); break;
                case "geometry-rectangle": canvas.DrawRectangle(9, 27, 82, 46); break;
                case "geometry-triangle": Polygon(new(50, 13), new(12, 85), new(88, 85)); break;
                case "geometry-right-triangle":
                    Polygon(new(18, 16), new(18, 84), new(86, 84));
                    canvas.DrawRectangle(18, 70, 14, 14); break;
                case "geometry-equilateral-triangle":
                    Polygon(new(50, 16), new(10, 85), new(90, 85));
                    canvas.DrawLine(25, 48, 35, 54); canvas.DrawLine(65, 54, 75, 48);
                    canvas.DrawLine(50, 79, 50, 91); break;
                case "geometry-trapezoid": Polygon(new(30, 22), new(70, 22), new(91, 78), new(9, 78)); break;
                case "geometry-isosceles-trapezoid":
                    Polygon(new(30, 22), new(70, 22), new(91, 78), new(9, 78));
                    canvas.DrawLine(14, 47, 26, 53); canvas.DrawLine(74, 53, 86, 47); break;
                case "geometry-right-trapezoid":
                    Polygon(new(18, 22), new(65, 22), new(88, 78), new(18, 78));
                    canvas.DrawRectangle(18, 64, 14, 14); break;
                case "geometry-rhombus": Polygon(new(50, 10), new(90, 50), new(50, 90), new(10, 50)); break;
                case "geometry-parallelogram": Polygon(new(30, 25), new(90, 25), new(70, 75), new(10, 75)); break;
                case "geometry-circle": canvas.DrawCircle(50, 50, 34); canvas.DrawLine(50, 50, 84, 50); break;
                case "geometry-cube":
                    canvas.DrawRectangle(12, 34, 53, 51);
                    Polygon(new(12, 34), new(35, 14), new(88, 14), new(65, 34));
                    Polygon(new(65, 34), new(88, 14), new(88, 65), new(65, 85));
                    break;
                case "geometry-prism":
                    canvas.DrawRectangle(8, 40, 62, 40);
                    Polygon(new(8, 40), new(28, 22), new(90, 22), new(70, 40));
                    Polygon(new(70, 40), new(90, 22), new(90, 62), new(70, 80)); break;
                case "geometry-sphere":
                    canvas.DrawCircle(50, 50, 36);
                    canvas.DrawEllipse(14, 39, 72, 22);
                    canvas.DrawEllipse(35, 14, 30, 72); break;
                case "geometry-cylinder":
                    canvas.DrawEllipse(20, 14, 60, 22);
                    canvas.DrawLine(20, 25, 20, 75); canvas.DrawLine(80, 25, 80, 75);
                    canvas.DrawEllipse(20, 64, 60, 22); break;
                case "geometry-cone":
                    canvas.DrawLine(50, 13, 16, 76); canvas.DrawLine(50, 13, 84, 76);
                    canvas.DrawEllipse(16, 65, 68, 22); break;
                case "geometry":
                    canvas.DrawRectangle(10, 16, 32, 32);
                    Polygon(new(68, 16), new(49, 48), new(87, 48));
                    canvas.DrawCircle(50, 76, 15); break;
                case "visual-angle":
                    canvas.DrawLine(15, 80, 85, 80); canvas.DrawLine(15, 80, 15, 15);
                    canvas.DrawRectangle(15, 64, 16, 16); break;
                case "visual-parallel":
                    canvas.DrawLine(10, 30, 90, 30); canvas.DrawLine(10, 70, 90, 70); break;
                case "visual-perpendicular":
                    canvas.DrawLine(12, 60, 88, 60); canvas.DrawLine(45, 12, 45, 88);
                    canvas.DrawRectangle(45, 44, 16, 16); break;
                case "two-numbers":
                    canvas.DrawRectangle(10, 22, 48, 19); canvas.DrawRectangle(10, 58, 80, 19);
                    canvas.DrawLine(34, 22, 34, 41);
                    canvas.DrawLine(34, 58, 34, 77); canvas.DrawLine(58, 58, 58, 77); break;
                case "measurement":
                    canvas.DrawRectangle(10, 32, 80, 32);
                    for (int i = 0; i < 8; i++) canvas.DrawLine(15 + i * 10, 33, 15 + i * 10, i % 2 == 0 ? 50 : 43);
                    break;
                case "clock":
                    canvas.DrawCircle(50, 50, 36);
                    for (int i = 0; i < 12; i++)
                    {
                        double a = i * Math.PI / 6;
                        canvas.FillCircle(50 + 30 * (float)Math.Sin(a), 50 - 30 * (float)Math.Cos(a), 2);
                    }
                    canvas.DrawLine(50, 50, 50, 23); canvas.DrawLine(50, 50, 69, 50); break;
                case "motion":
                    canvas.DrawLine(12, 60, 87, 60); canvas.DrawLine(87, 60, 74, 48); canvas.DrawLine(87, 60, 74, 72);
                    canvas.FillCircle(24, 60, 6); canvas.FontSize = 18; Label("s = v × t", 15); break;
                case "percentage":
                    for (int row = 0; row < 5; row++) for (int col = 0; col < 5; col++)
                    {
                        if (row < 3) canvas.FillRectangle(14 + col * 15, 14 + row * 15, 12, 12);
                        else canvas.DrawRectangle(14 + col * 15, 14 + row * 15, 12, 12);
                    }
                    break;
                case "probability":
                    canvas.DrawCircle(50, 50, 36);
                    for (int i = 0; i < 5; i++) canvas.FillCircle(30 + (i % 3) * 20, 39 + (i / 3) * 23, 6);
                    break;
                case "number-place-value":
                    canvas.StrokeSize = 1;
                    canvas.DrawRectangle(6, 24, 50, 50);
                    for (int i = 1; i < 10; i++)
                    {
                        canvas.DrawLine(6, 24 + i * 5, 56, 24 + i * 5);
                        canvas.DrawLine(6 + i * 5, 24, 6 + i * 5, 74);
                    }
                    canvas.DrawRectangle(65, 24, 5, 50);
                    for (int i = 1; i < 10; i++) canvas.DrawLine(65, 24 + i * 5, 70, 24 + i * 5);
                    canvas.FillRectangle(83, 53, 5, 5); canvas.FillRectangle(83, 66, 5, 5); break;
                case "number-counting":
                case "arithmetic":
                    for (int i = 0; i < 5; i++) canvas.FillCircle(22 + (i % 3) * 26, 34 + (i / 3) * 30, 8);
                    break;
                case "remainder":
                    canvas.DrawRectangle(9, 22, 52, 55);
                    for (int i = 0; i < 6; i++) canvas.FillCircle(23 + (i % 2) * 22, 34 + (i / 2) * 16, 5);
                    canvas.FillCircle(80, 49, 5); break;
                case "decimal": Label("0,5", 18); canvas.DrawLine(14, 69, 86, 69);
                    canvas.FillCircle(50, 69, 5); break;
                case "proportion":
                case "proportion-direct": canvas.DrawLine(18, 80, 84, 20);
                    canvas.DrawLine(18, 80, 18, 16); canvas.DrawLine(18, 80, 89, 80); break;
                case "proportion-inverse":
                    canvas.DrawLine(18, 80, 18, 16); canvas.DrawLine(18, 80, 89, 80);
                    var inverseCurve = new PathF();
                    inverseCurve.MoveTo(26, 20);
                    for (int x = 27; x <= 84; x++)
                        inverseCurve.LineTo(x, 80 - 480f / (x - 18));
                    canvas.DrawPath(inverseCurve); break;
                case "data": Bars(); break;
                case "multi-step": Label("① → ②", 18); Label("→ ③", 52); break;
                case "mixed": canvas.FontSize = 32; Label("+ −", 17); Label("× ÷", 53); break;
                default: throw new InvalidOperationException("Unknown quiz illustration: " + id);
            }
        }
        finally { canvas.RestoreState(); }
    }
}
