using MathSolver.Models;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public sealed partial class ElementaryQuizDrawable
{
    private static void DrawPictograph(ICanvas canvas, QuizVisualData data, float width, float height)
    {
        float left = Math.Min(105, width * .28f), rowHeight = Math.Min(65, (height - 55) / Math.Max(1, data.Labels.Count));
        decimal key = data.PictographKey ?? 1;
        int largest = Math.Max(1, (int)(data.Values.Max() / key));
        float step = Math.Min(30, (width - left - 16) / largest), radius = Math.Min(10, step * .35f);
        for (int row = 0; row < data.Labels.Count; row++)
        {
            float y = 14 + row * rowHeight;
            canvas.DrawRectangle(6, y, width - 12, rowHeight);
            canvas.DrawLine(left, y, left, y + rowHeight);
            canvas.DrawString(data.Labels[row], 8, y, left - 12, rowHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.FillColor = Series[row % Series.Length];
            int count = (int)(data.Values[row] / key);
            for (int icon = 0; icon < count; icon++)
                canvas.FillCircle(left + step * (icon + .5f), y + rowHeight / 2, radius);
        }
        float legend = 25 + data.Labels.Count * rowHeight;
        canvas.FillColor = Series[0];
        canvas.FillCircle(20, legend + 12, 8);
        canvas.DrawString($"= {key} {data.Unit}", 34, legend, width - 44, 26, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private static void DrawRecognitionShape(ICanvas canvas, QuizVisualData data, float width, float height)
    {
        float cx = width / 2, cy = height / 2, size = Math.Min(width - 65, height - 65) * .38f;
        float angle = (float)data.RotationDegrees * MathF.PI / 180;
        PointF Transform(float x, float y) => new(cx + size * (x * MathF.Cos(angle) - y * MathF.Sin(angle)),
            cy + size * (x * MathF.Sin(angle) + y * MathF.Cos(angle)));
        void Polygon(params (float X, float Y)[] vertices)
        {
            var path = new PathF();
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = Transform(vertices[i].X, vertices[i].Y);
                if (i == 0) path.MoveTo(p); else path.LineTo(p);
            }
            path.Close(); canvas.DrawPath(path);
        }
        void Segment(float x1, float y1, float x2, float y2)
        {
            var a = Transform(x1, y1); var b = Transform(x2, y2);
            canvas.DrawLine(a.X, a.Y, b.X, b.Y);
        }
        switch (data.ScenarioId)
        {
            case "circle": canvas.DrawCircle(cx, cy, size); break;
            case "triangle": Polygon((0, -1), (1, .8f), (-1, .8f)); break;
            case "square": Polygon((-.8f, -.8f), (.8f, -.8f), (.8f, .8f), (-.8f, .8f)); break;
            case "rectangle": Polygon((-1.2f, -.65f), (1.2f, -.65f), (1.2f, .65f), (-1.2f, .65f)); break;
            case "trapezoid": Polygon((-.55f, -.7f), (.55f, -.7f), (1.15f, .7f), (-1.15f, .7f)); break;
            case "parallelogram": Polygon((-.65f, -.7f), (1.15f, -.7f), (.65f, .7f), (-1.15f, .7f)); break;
            case "rhombus": Polygon((0, -.65f), (1.2f, 0), (0, .65f), (-1.2f, 0)); break;
            case "cube":
            case "cuboid":
            {
                float halfWidth = data.ScenarioId == "cube" ? .65f : 1.1f, halfHeight = .65f;
                Polygon((-halfWidth, -halfHeight), (halfWidth, -halfHeight), (halfWidth, halfHeight), (-halfWidth, halfHeight));
                // Oblique projection: visible top and right faces, hidden rear edges dashed.
                Segment(-halfWidth, -halfHeight, -halfWidth + .45f, -halfHeight - .4f);
                Segment(-halfWidth + .45f, -halfHeight - .4f, halfWidth + .45f, -halfHeight - .4f);
                Segment(halfWidth + .45f, -halfHeight - .4f, halfWidth, -halfHeight);
                Segment(halfWidth + .45f, -halfHeight - .4f, halfWidth + .45f, halfHeight - .4f);
                Segment(halfWidth + .45f, halfHeight - .4f, halfWidth, halfHeight);
                canvas.StrokeDashPattern = [4, 4];
                Segment(-halfWidth + .45f, -halfHeight - .4f, -halfWidth + .45f, halfHeight - .4f);
                Segment(-halfWidth + .45f, halfHeight - .4f, halfWidth + .45f, halfHeight - .4f);
                Segment(-halfWidth + .45f, halfHeight - .4f, -halfWidth, halfHeight);
                canvas.StrokeDashPattern = null;
                break;
            }
            case "cylinder":
                canvas.DrawEllipse(cx - size * .65f, cy - size, size * 1.3f, size * .4f);
                canvas.DrawLine(cx - size * .65f, cy - size * .8f, cx - size * .65f, cy + size * .8f);
                canvas.DrawLine(cx + size * .65f, cy - size * .8f, cx + size * .65f, cy + size * .8f);
                canvas.DrawEllipse(cx - size * .65f, cy + size * .6f, size * 1.3f, size * .4f);
                break;
            case "sphere":
                canvas.DrawCircle(cx, cy, size);
                canvas.StrokeDashPattern = [4, 4];
                canvas.DrawEllipse(cx - size, cy - size * .25f, size * 2, size * .5f);
                canvas.DrawEllipse(cx - size * .3f, cy - size, size * .6f, size * 2);
                canvas.StrokeDashPattern = null;
                break;
        }
    }
}
