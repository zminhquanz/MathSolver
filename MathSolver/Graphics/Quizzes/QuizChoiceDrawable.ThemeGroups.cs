using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

internal sealed partial class QuizChoiceDrawable
{
    // Category signposts, isolated from the mathematical thumbnails sharing these IDs.
    private bool DrawThemeGroup(ICanvas c, float size)
    {
        c.Translate(5, 5);
        c.Scale(.9f, .9f);
        c.StrokeSize = QuizThemeIconPolicy.StrokeForSize(size) * 100 / (size * .9f);
        c.StrokeLineCap = LineCap.Round;
        c.StrokeLineJoin = LineJoin.Round;

        void Line(float x, float y, float x2, float y2) => c.DrawLine(x, y, x2, y2);
        void Path(params PointF[] points)
        {
            var p = new PathF(); p.MoveTo(points[0]);
            foreach (var pt in points.Skip(1)) p.LineTo(pt);
            c.DrawPath(p);
        }
        switch (id)
        {
            case "knowledge-objects":
            case "knowledge-animals":
            case "knowledge-school-supplies":
            case "knowledge-money":
            case "knowledge-packaging":
                return DrawPresentation(c);
            case "mixed":
                var rising = new PathF(); rising.MoveTo(12, 78); rising.LineTo(27, 78);
                rising.CurveTo(45, 78, 55, 22, 73, 22); rising.LineTo(88, 22); c.DrawPath(rising);
                var falling = new PathF(); falling.MoveTo(12, 22); falling.LineTo(27, 22);
                falling.CurveTo(37, 22, 41, 36, 45, 43); c.DrawPath(falling);
                var end = new PathF(); end.MoveTo(55, 57); end.CurveTo(61, 69, 65, 78, 73, 78); end.LineTo(88, 78); c.DrawPath(end);
                Path(new(78, 12), new(88, 22), new(78, 32));
                Path(new(78, 68), new(88, 78), new(78, 88));
                break;
            case "motion":
                var route = new PathF(); route.MoveTo(12, 80);
                route.CurveTo(12, 53, 50, 70, 49, 47);
                route.CurveTo(48, 23, 73, 33, 88, 18); c.DrawPath(route);
                c.FillCircle(12, 80, 4);
                Path(new(75, 18), new(88, 18), new(88, 31));
                break;
            case "food":
                var bowl = new PathF(); bowl.MoveTo(12, 47); bowl.LineTo(88, 47);
                bowl.CurveTo(83, 70, 69, 81, 50, 81); bowl.CurveTo(31, 81, 17, 70, 12, 47); bowl.Close(); c.DrawPath(bowl);
                Line(34, 85, 66, 85);
                Line(27, 18, 27, 33); Line(50, 12, 50, 29); Line(73, 18, 73, 33);
                break;
            case "drinks":
                c.DrawRoundedRectangle(18, 14, 54, 68, 7);
                var handle = new PathF(); handle.MoveTo(72, 30); handle.CurveTo(99, 30, 99, 64, 72, 64); c.DrawPath(handle);
                break;
            case "fruit":
                var apple = new PathF(); apple.MoveTo(50, 36);
                apple.CurveTo(31, 23, 13, 36, 16, 57); apple.CurveTo(17, 78, 31, 92, 42, 89);
                apple.CurveTo(48, 86, 52, 86, 58, 89); apple.CurveTo(72, 93, 85, 76, 85, 55);
                apple.CurveTo(85, 36, 68, 26, 50, 36); apple.Close(); c.DrawPath(apple);
                Line(50, 36, 54, 10);
                var leaf = new PathF(); leaf.MoveTo(54, 24); leaf.CurveTo(61, 9, 76, 9, 84, 14);
                leaf.CurveTo(78, 28, 64, 31, 54, 24); leaf.Close(); c.DrawPath(leaf);
                break;
            case "clock":
                c.DrawCircle(50, 50, 40);
                Line(50, 16, 50, 21); Line(84, 50, 79, 50); Line(50, 84, 50, 79); Line(16, 50, 21, 50);
                Path(new(50, 29), new(50, 50), new(68, 50));
                break;
            case "measurement":
                var ruler = new PathF(); ruler.MoveTo(12, 65); ruler.LineTo(72, 12); ruler.LineTo(89, 32); ruler.LineTo(29, 85); ruler.Close(); c.DrawPath(ruler);
                for (int n = 0; n < 6; n++)
                {
                    float x = 24 + n * 9, y = 55 - n * 8;
                    float length = n % 2 == 0 ? 10 : 6;
                    Line(x, y, x + length * .66f, y + length * .75f);
                }
                break;
            case "geometry":
                c.DrawRectangle(10, 16, 32, 32);
                Path(new(68, 16), new(49, 48), new(87, 48), new(68, 16));
                c.DrawCircle(50, 76, 15);
                break;
            case "knowledge-production":
                var factory = new PathF(); factory.MoveTo(12, 82); factory.LineTo(12, 40);
                factory.LineTo(34, 25); factory.LineTo(34, 40); factory.LineTo(56, 25); factory.LineTo(56, 40);
                factory.LineTo(75, 40); factory.LineTo(75, 14); factory.LineTo(87, 14); factory.LineTo(87, 82); factory.Close(); c.DrawPath(factory);
                for (int n = 0; n < 3; n++) c.DrawRoundedRectangle(22 + n * 22, 53, 10, 13, 1);
                break;
            case "data-table":
                c.FillColor = ink.WithAlpha(.18f); c.FillRectangle(10, 10, 80, 26); c.FillColor = ink;
                c.DrawRectangle(10, 10, 80, 80);
                Line(10, 36, 90, 36); Line(10, 63, 90, 63); Line(36, 10, 36, 90); Line(63, 10, 63, 90);
                break;
            default: throw new InvalidOperationException("Unknown theme group illustration: " + id);
        }
        return true;
    }
}
