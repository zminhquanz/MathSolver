using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

internal sealed partial class QuizChoiceDrawable
{
    // Each skill has a mathematical thumbnail, independent of translated captions.
    // Coordinates are shared with the 100 × 100 logical canvas in Draw.
    private bool DrawSkill(ICanvas c)
    {
        void Text(string value, float x = 5, float y = 34, float width = 90, float size = 20)
        {
            c.FontSize = size;
            c.DrawString(value, x, Math.Min(y, 68), width, 28, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        void Equation(string top, string bottom)
        {
            float size = Math.Min(20, 180f / Math.Max(top.Length, bottom.Length));
            Text(top, y: 16, size: size);
            Text(bottom, y: 52, size: size);
        }
        void Arrow(float x, float y, float endX, float endY)
        {
            c.DrawLine(x, y, endX, endY);
            float angle = MathF.Atan2(endY - y, endX - x);
            c.DrawLine(endX, endY, endX - 9 * MathF.Cos(angle - 0.55f), endY - 9 * MathF.Sin(angle - 0.55f));
            c.DrawLine(endX, endY, endX - 9 * MathF.Cos(angle + 0.55f), endY - 9 * MathF.Sin(angle + 0.55f));
        }
        void Shape(bool fill, params PointF[] points)
        {
            var path = new PathF();
            path.MoveTo(points[0]);
            foreach (var point in points.Skip(1)) path.LineTo(point);
            path.Close();
            if (fill)
            {
                c.FillColor = ink.WithAlpha(0.22f);
                c.FillPath(path);
                c.FillColor = ink;
            }
            c.DrawPath(path);
        }
        void Cells(int columns, int rows, int shaded, float x = 12, float y = 20, float cell = 16)
        {
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                if (row * columns + col < shaded)
                {
                    c.FillColor = ink.WithAlpha(0.25f);
                    c.FillRectangle(x + col * cell, y + row * cell, cell, cell);
                    c.FillColor = ink;
                }
                c.DrawRectangle(x + col * cell, y + row * cell, cell, cell);
            }
        }
        void Fraction(string numerator, string denominator, float x, float y, float width = 26)
        {
            Text(numerator, x, y, width, 18);
            c.DrawLine(x, y + 27, x + width, y + 27);
            Text(denominator, x, y + 28, width, 18);
        }
        void Bars(bool missing = false)
        {
            c.DrawLine(10, 85, 92, 85);
            c.DrawLine(12, 85, 12, 12);
            c.FillRectangle(24, 57, 14, 28);
            if (missing)
            {
                c.DrawRectangle(47, 32, 14, 53);
                Text("?", 43, 40, 22);
            }
            else c.FillRectangle(47, 32, 14, 53);
            c.FillRectangle(70, 16, 14, 69);
        }
        void GroupBar(float y, int parts)
        {
            for (int i = 0; i < parts; i++) c.DrawRectangle(12 + i * 21, y, 21, 16);
        }
        void Ruler()
        {
            c.DrawRectangle(10, 40, 80, 28);
            for (int i = 0; i < 9; i++)
                c.DrawLine(10 + i * 10, 41, 10 + i * 10, i % 2 == 0 ? 58 : 50);
        }
        void Cube(bool sides, bool top)
        {
            Shape(sides, new(17, 36), new(62, 36), new(62, 83), new(17, 83));
            Shape(top, new(17, 36), new(40, 16), new(85, 16), new(62, 36));
            Shape(sides, new(62, 36), new(85, 16), new(85, 63), new(62, 83));
        }
        void NumberLine()
        {
            Arrow(10, 58, 92, 58);
            for (int i = 0; i < 4; i++)
            {
                c.DrawLine(18 + i * 20, 51, 18 + i * 20, 65);
                Text(i.ToString(), 8 + i * 20, 68, 20, 14);
            }
        }
        switch (id)
        {
            case "data-table":
                Cells(3, 3, 3, 14, 14, 24);
                Text("2", 15, 39, 22, 16); Text("5", 39, 63, 22, 16);
                break;
            case "data-bar": Bars(); break;
            case "data-pie":
                Shape(true, new(50, 50), new(50, 14), new(68, 19), new(81, 32), new(86, 50));
                c.DrawCircle(50, 50, 36);
                c.DrawLine(50, 50, 50, 14); c.DrawLine(50, 50, 86, 50); c.DrawLine(50, 50, 24, 76);
                break;
            case "data-total":
                c.FillRectangle(14, 35, 16, 28); c.FillRectangle(42, 18, 16, 45); c.FillRectangle(70, 8, 16, 55);
                c.DrawLine(12, 72, 88, 72); c.DrawLine(12, 66, 12, 72); c.DrawLine(88, 66, 88, 72);
                Text("+", y: 72); break;
            case "data-difference":
                c.FillRectangle(18, 49, 20, 36); c.FillRectangle(50, 18, 20, 67);
                c.DrawLine(76, 18, 88, 18); c.DrawLine(76, 49, 88, 49); c.DrawLine(84, 18, 84, 49);
                Text("−", 72, 52, 24); break;
            case "data-pictograph":
                for (int i = 0; i < 5; i++) c.FillCircle(24 + (i % 3) * 24, 24 + (i / 3) * 28, 8);
                Text("● = 2", y: 70, size: 16); break;
            case "data-sort":
                c.DrawRectangle(8, 40, 36, 46); c.DrawRectangle(56, 40, 36, 46);
                c.FillCircle(19, 55, 5); c.FillCircle(33, 72, 5);
                c.FillRectangle(65, 50, 10, 10); c.FillRectangle(76, 70, 10, 10);
                Arrow(40, 17, 26, 33); Arrow(60, 17, 74, 33); break;
            case "data-complete": Bars(missing: true); break;
            case "time-elapsed":
                c.DrawCircle(23, 32, 17); c.DrawLine(23, 32, 23, 18); c.DrawLine(23, 32, 34, 32);
                c.DrawCircle(77, 32, 17); c.DrawLine(77, 32, 77, 18); c.DrawLine(77, 32, 77, 45);
                Arrow(18, 68, 82, 68); Text("?", y: 70); break;
            case "time-conversion": Equation("1 h", "= 60 min"); break;
            case "time-calendar":
                c.DrawRoundedRectangle(14, 20, 72, 68, 5);
                c.DrawLine(14, 39, 86, 39); c.DrawLine(30, 12, 30, 29); c.DrawLine(70, 12, 70, 29);
                Cells(3, 2, 1, 23, 47, 18); break;
            case "measure-length": Ruler(); Text("cm → m", y: 7, size: 17); break;
            case "measure-mass":
                c.DrawLine(50, 16, 50, 80); c.DrawLine(20, 28, 80, 28); c.DrawLine(30, 82, 70, 82);
                Shape(true, new(12, 55), new(32, 55), new(27, 65), new(17, 65));
                Shape(true, new(68, 55), new(88, 55), new(83, 65), new(73, 65));
                c.DrawLine(22, 28, 12, 55); c.DrawLine(22, 28, 32, 55);
                c.DrawLine(78, 28, 68, 55); c.DrawLine(78, 28, 88, 55); break;
            case "measure-capacity":
                c.DrawRectangle(24, 15, 44, 70); c.DrawRoundedRectangle(68, 29, 16, 34, 6);
                c.FillColor = ink.WithAlpha(0.25f); c.FillRectangle(26, 49, 40, 34); c.FillColor = ink;
                for (int i = 0; i < 4; i++) c.DrawLine(25, 27 + i * 14, 37, 27 + i * 14);
                Text("L", 45, 56, 18, 16); break;
            case "measure-area": Cells(4, 3, 12, 14, 12, 18); Text("m² → cm²", y: 72, size: 15); break;
            case "measure-volume": Cube(sides: false, top: false); Text("m³", 24, 48, 30, 16); break;
            case "measure-mixed":
                c.DrawLine(12, 38, 50, 38); c.DrawLine(50, 38, 88, 38);
                c.DrawLine(12, 30, 12, 46); c.DrawLine(50, 30, 50, 46); c.DrawLine(88, 30, 88, 46);
                Text("m", 12, 49, 36); Text("cm", 52, 49, 36, 18); Text("+", y: 6); break;
            case "measure-map":
                Shape(false, new(10, 27), new(35, 17), new(65, 27), new(90, 17), new(90, 76), new(65, 86), new(35, 76), new(10, 86));
                c.DrawLine(35, 17, 35, 76); c.DrawLine(65, 27, 65, 86);
                c.FillCircle(23, 52, 4); Arrow(23, 52, 78, 52); Text("1:100", y: 0, size: 14); break;
            case "measure-ruler": Ruler(); Arrow(10, 24, 60, 24); Text("?", 49, 70, 24); break;
            case "measure-protractor":
                for (int i = 0; i < 18; i++)
                {
                    float a = i * MathF.PI / 18, b = (i + 1) * MathF.PI / 18;
                    c.DrawLine(50 + 38 * MathF.Cos(a), 75 - 38 * MathF.Sin(a), 50 + 38 * MathF.Cos(b), 75 - 38 * MathF.Sin(b));
                }
                c.DrawLine(12, 75, 88, 75); c.DrawLine(50, 75, 70, 29); Text("°", 36, 40, 22); break;
            case "measure-temperature":
                c.DrawRoundedRectangle(36, 10, 16, 65, 8); c.DrawCircle(44, 79, 12);
                c.FillCircle(44, 79, 7); c.DrawLine(44, 72, 44, 40);
                for (int i = 0; i < 4; i++) c.DrawLine(58, 23 + i * 14, 68, 23 + i * 14);
                Text("°C", 64, 7, 30, 17); break;
            case "decimal-add": Equation("1,2 + 0,3", "= 1,5"); break;
            case "decimal-subtract": Equation("1,5 − 0,3", "= 1,2"); break;
            case "decimal-multiply": Equation("1,2 × 3", "= 3,6"); break;
            case "decimal-divide": Equation("3,6 ÷ 3", "= 1,2"); break;
            case "decimal-round": Equation("1,26", "→ 1,3"); break;
            case "decimal-compare": Equation("1,2 < 1,3", "1,3 > 1,2"); break;
            case "fraction-reduce":
                Fraction("6", "8", 9, 20); Text("=", 39, 33, 22); Fraction("3", "4", 66, 20); break;
            case "fraction-mixed":
                Text("1", 5, 34, 22, 26); Fraction("1", "2", 35, 20);
                c.DrawCircle(81, 33, 12); Shape(true, new(81, 33), new(81, 21), new(93, 33), new(81, 45)); break;
            case "fraction-common":
                Fraction("1", "2", 6, 4); Fraction("1", "3", 68, 4); Text(";", 37, 17, 26);
                Cells(6, 1, 3, 8, 76, 14); break;
            case "fraction-part":
                Fraction("1", "3", 5, 20);
                for (int i = 0; i < 9; i++)
                    if (i < 3) c.FillCircle(45 + (i % 3) * 20, 25 + (i / 3) * 22, 5);
                    else c.DrawCircle(45 + (i % 3) * 20, 25 + (i / 3) * 22, 5);
                break;
            case "fraction-whole":
                Cells(3, 1, 1, 11, 43, 26);
                Text("3", 10, 9, 28); Text("?", y: 73); break;
            case "fraction-terms": Fraction("a", "b", 35, 18); Arrow(9, 20, 31, 28); Arrow(87, 80, 65, 68); break;
            case "fraction-equivalent":
                Cells(2, 1, 1, 10, 14, 37); Text("=", y: 39);
                Cells(4, 1, 2, 10, 72, 18.5f); break;
            case "fraction-order":
                Fraction("1", "4", 7, 20); Text("<", 39, 34, 22); Fraction("1", "2", 67, 20); break;
            case "expression-integer": Equation("2 + 3 × 4", "= ?"); break;
            case "expression-brackets": Equation("(2 + 3)", "× 4 = ?"); break;
            case "expression-fraction":
                Fraction("1", "2", 7, 15); Text("+", 39, 28, 20); Fraction("1", "3", 65, 15);
                Text("× 2", y: 76, size: 16); break;
            case "expression-fraction-brackets":
                Fraction("1", "2", 16, 14, 22); Text("+", 41, 27, 18); Fraction("1", "3", 62, 14, 22);
                Text("(", 1, 30, 13, 30); Text(")", 85, 30, 13, 30); Text("× 2", y: 76, size: 16); break;
            case "average":
            case "average-direct":
                c.DrawRectangle(13, 55, 18, 30); c.DrawRectangle(41, 35, 18, 50); c.DrawRectangle(69, 15, 18, 70);
                c.StrokeSize = 4; c.DrawLine(7, 35, 94, 35); Text("=", y: 0); break;
            case "average-from-total": Equation("12 ÷ 3", "= 4"); break;
            case "average-to-total": Equation("4 × 3", "= 12"); break;
            case "average-missing":
                c.DrawRectangle(14, 52, 18, 33); c.DrawRectangle(42, 32, 18, 53); c.DrawRectangle(70, 14, 18, 71);
                Text("?", 69, 35, 20); c.DrawLine(8, 32, 94, 32); break;
            case "average-indirect": Equation("a, a + 2", "(a + b) ÷ 2"); break;
            case "average-two-groups":
                Cells(2, 1, 2, 10, 19, 22); Cells(3, 1, 0, 10, 57, 22);
                Arrow(75, 20, 90, 42); Arrow(75, 78, 90, 56); break;
            case "percentage-ratio":
                Fraction("3", "4", 9, 16); Text("× 100", 40, 27, 57, 16); Text("= 75%", y: 77, size: 16); break;
            case "percentage-part": Cells(5, 3, 3, 12, 10, 15); Text("20% → ?", y: 70, size: 17); break;
            case "percentage-whole":
                Cells(4, 1, 1, 10, 38, 20); Text("25% = 5", y: 2, size: 17); Text("100% = ?", y: 74, size: 17); break;
            case "motion-chasing":
                c.FillCircle(17, 32, 6); Arrow(24, 32, 65, 32);
                c.FillCircle(45, 67, 6); Arrow(52, 67, 88, 67); break;
            case "motion-meeting":
                c.FillCircle(13, 50, 6); Arrow(22, 50, 44, 50);
                c.FillCircle(87, 50, 6); Arrow(78, 50, 56, 50); break;
            case "motion-river":
                Shape(true, new(22, 35), new(78, 35), new(66, 52), new(34, 52));
                c.DrawLine(50, 12, 50, 35); Shape(false, new(50, 12), new(70, 29), new(50, 29));
                for (int i = 0; i < 4; i++) c.DrawLine(10 + i * 22, 63, 24 + i * 22, 58);
                Arrow(18, 82, 85, 82); break;
            case "two-sum-difference":
                c.DrawRectangle(10, 23, 48, 18); c.DrawRectangle(10, 60, 78, 18);
                c.DrawLine(58, 52, 88, 52); Text("−", 58, 20, 30); Text("+", y: 79); break;
            case "two-sum-ratio":
                GroupBar(23, 2); GroupBar(61, 3);
                c.DrawLine(82, 23, 90, 23); c.DrawLine(90, 23, 90, 77); c.DrawLine(82, 77, 90, 77);
                Text("+", y: 77); break;
            case "two-difference-ratio":
                GroupBar(23, 2); GroupBar(61, 3);
                c.FillColor = ink.WithAlpha(0.25f); c.FillRectangle(54, 61, 21, 16); c.FillColor = ink;
                c.DrawLine(54, 51, 75, 51); c.DrawLine(54, 47, 54, 55); c.DrawLine(75, 47, 75, 55);
                Text("−", 52, 14, 25); break;
            case "remainder-minimum":
                Cells(2, 2, 4, 10, 28, 16); Cells(2, 2, 1, 57, 28, 16); Text("+ 1", y: 72); break;
            case "remainder-leftovers":
                c.DrawRectangle(8, 15, 48, 68);
                for (int i = 0; i < 6; i++) c.FillCircle(20 + (i % 2) * 22, 29 + (i / 2) * 20, 5);
                c.DrawCircle(77, 48, 15); c.FillCircle(77, 48, 5); break;
            case "geometry-perimeter":
                c.StrokeSize = 5; c.DrawRectangle(18, 22, 64, 55); Arrow(35, 11, 70, 11); break;
            case "geometry-area": Cells(4, 3, 12, 14, 22, 18); break;
            case "geometry-volume": Cube(sides: true, top: true); c.DrawLine(39, 36, 39, 83); c.DrawLine(17, 59, 62, 59); break;
            case "geometry-lateral-area": Cube(sides: true, top: false); break;
            case "geometry-total-area": Cube(sides: true, top: true); break;
            case "visual-count-sides":
                Shape(false, new(50, 14), new(86, 39), new(73, 82), new(27, 82), new(14, 39));
                Text("1", 60, 12, 24, 13); Text("2", 74, 52, 24, 13); Text("3", 38, 75, 24, 13);
                Text("4", 4, 52, 24, 13); Text("5", 15, 12, 24, 13); break;
            case "visual-missing-side": c.DrawRectangle(13, 28, 74, 48); Text("?", y: 0); Text("6", y: 76, size: 16); break;
            case "visual-composite-area":
                Shape(true, new(15, 15), new(49, 15), new(49, 49), new(85, 49), new(85, 85), new(15, 85));
                c.DrawLine(15, 49, 49, 49); break;
            case "visual-position":
                c.DrawRectangle(35, 38, 30, 30); c.FillCircle(50, 15, 6); c.FillCircle(15, 52, 6);
                Arrow(82, 85, 82, 20); break;
            case "visual-line": Arrow(12, 45, 90, 45); c.FillCircle(12, 45, 5); Text("A", 5, 60, 18, 16); break;
            case "visual-midpoint":
                c.DrawLine(10, 50, 90, 50);
                for (int i = 0; i < 3; i++) c.FillCircle(10 + 40 * i, 50, 4);
                c.DrawLine(30, 43, 30, 57); c.DrawLine(70, 43, 70, 57);
                Text("M", 39, 14, 22, 18); break;
            case "visual-circle-parts":
                c.DrawCircle(50, 50, 35); c.DrawLine(15, 50, 85, 50); c.DrawLine(50, 50, 50, 15);
                c.FillCircle(50, 50, 4); Text("r", 54, 16, 20, 15); Text("d", y: 59, size: 15); break;
            case "visual-net":
                for (int i = 0; i < 4; i++) c.DrawRectangle(41, 9 + i * 20, 20, 20);
                c.DrawRectangle(21, 29, 20, 20); c.DrawRectangle(61, 29, 20, 20); break;
            case "visual-triangle-kind":
                Shape(false, new(12, 20), new(12, 82), new(88, 82)); c.DrawRectangle(12, 67, 15, 15);
                Text("?", 38, 42, 22); break;
            case "multi-add-subtract": Equation("8 + 4 → 12", "12 − 3 → ?"); break;
            case "multi-equal-groups": Equation("3 × 4 → 12", "12 + 2 → ?"); break;
            case "multi-remaining": Equation("2 × 3 → 6", "12 − 6 → ?"); break;
            case "multi-share": Equation("12 + 8 → 20", "20 ÷ 4 → ?"); break;
            case "number-read":
                Text("34", 4, 33, 44, 25);
                Shape(true, new(54, 40), new(65, 40), new(76, 28), new(76, 72), new(65, 60), new(54, 60));
                c.DrawLine(85, 37, 92, 45); c.DrawLine(92, 45, 92, 55); c.DrawLine(92, 55, 85, 63); break;
            case "number-write":
                Text("34", 3, 24, 44, 25); c.DrawLine(9, 75, 48, 75);
                Shape(true, new(59, 65), new(80, 18), new(90, 24), new(69, 71), new(54, 82)); break;
            case "number-adjacent":
                Text("4", 6, 35, 22); Text("?", 39, 35, 22); Text("6", 72, 35, 22);
                c.DrawRoundedRectangle(34, 26, 32, 45, 5); break;
            case "number-line": NumberLine(); c.FillCircle(58, 58, 5); Text("?", 46, 17, 24); break;
            case "number-parity":
                for (int i = 0; i < 8; i++) c.FillCircle(32 + (i % 2) * 36, 18 + (i / 2) * 21, 6);
                for (int i = 0; i < 4; i++) c.DrawLine(39, 18 + i * 21, 61, 18 + i * 21); break;
            case "number-roman": Equation("XIV", "= 14"); break;
            case "number-order": Equation("2 < 5 < 8", "→"); break;
            case "number-round": Equation("47", "→ 50"); break;
            case "number-estimate": Equation("19 + 21", "≈ 40"); break;
            case "number-letter": Equation("2 × a + 1", "a = 3"); break;
            case "probability-experiment":
                c.DrawRectangle(12, 12, 34, 34); c.FillCircle(22, 22, 3); c.FillCircle(36, 36, 3);
                c.DrawLine(57, 21, 89, 21); c.DrawLine(57, 32, 89, 32); c.DrawLine(57, 43, 80, 43);
                Text("3 / 10", y: 65); break;
            case "find-x-auto":
                Text("x = ?", y: 13, size: 28); Text("+ − × ÷", y: 58, size: 22); break;
            default: return false;
        }
        return true;
    }
}
