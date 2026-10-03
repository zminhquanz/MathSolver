using MathSolver.Models;
using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

public sealed partial class ElementaryQuizDrawable
{
    private static void DrawPolygons(ICanvas canvas, QuizVisualData data, float width, float height)
    {
        if (width < 120 || height < 100) return;
        var polygons = data.Polygons!;
        var annotations = data.Annotations ?? [];
        double angle = (double)data.RotationDegrees * Math.PI / 180;
        float cosine = (float)Math.Cos(angle), sine = (float)Math.Sin(angle);
        PointF Rotate(decimal x, decimal y) => new((float)x * cosine - (float)y * sine,
            (float)x * sine + (float)y * cosine);
        PointF[] bounds = polygons.SelectMany(part => part.Vertices).Select(point => Rotate(point.X, point.Y))
            .Concat(annotations.Select(label => Rotate(label.X, label.Y))).ToArray();
        float minX = bounds.Min(point => point.X), maxX = bounds.Max(point => point.X);
        float minY = bounds.Min(point => point.Y), maxY = bounds.Max(point => point.Y);
        // Reserve room for upright dimension text and outside vertex names on narrow phones.
        float horizontalMargin = Math.Min(72, width * .22f), verticalMargin = 30;
        float scale = Math.Min((width - 2 * horizontalMargin) / Math.Max(.01f, maxX - minX),
            (height - 2 * verticalMargin) / Math.Max(.01f, maxY - minY));
        float offsetX = (width - (maxX - minX) * scale) / 2 - minX * scale;
        float offsetY = (height - (maxY - minY) * scale) / 2 - minY * scale;
        PointF Map(decimal x, decimal y)
        {
            PointF rotated = Rotate(x, y);
            return new(offsetX + rotated.X * scale, offsetY + rotated.Y * scale);
        }
        Color text = ThemeResource.GetColor("WallpaperTextPrimaryColor", "#1E293B");
        Color outline = ThemeResource.GetColor("WallpaperTextSecondaryColor", "#64748B");
        void Label(string value, PointF point, float textWidth = 100)
        {
            // Coordinates are given independently of the answer; dimensions never expose x.
            float left = Math.Clamp(point.X - textWidth / 2, 2, Math.Max(2, width - textWidth - 2));
            float top = Math.Clamp(point.Y - 12, 2, height - 26);
            canvas.DrawString(value, left, top, Math.Min(textWidth, width - 4), 24,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        for (int index = 0; index < polygons.Count; index++)
        {
            var part = polygons[index];
            PointF[] points = part.Vertices.Select(point => Map(point.X, point.Y)).ToArray();
            if (points.Length < 3) continue;
            var path = new PathF();
            path.MoveTo(points[0]);
            foreach (PointF point in points.Skip(1)) path.LineTo(point);
            path.Close();
            canvas.FillColor = part.IsCutout ? ThemeResource.GetColor("SurfaceColor", "#FFFFFF")
                : Series[index % Series.Length].WithAlpha(.16f);
            canvas.FillPath(path);
            canvas.StrokeColor = part.IsCutout || data.Kind == "polygon" ? outline : Series[index % Series.Length];
            canvas.StrokeDashPattern = part.IsCutout ? [5, 4] : null;
            canvas.DrawPath(path);
            canvas.StrokeDashPattern = null;
            // Use the vertex average only to place short region names, never measurements.
            float cx = points.Average(point => point.X), cy = points.Average(point => point.Y);
            canvas.FontColor = text;
            if (part.Label.Length > 0) Label(part.Label, new(cx, cy), 80);
            for (int vertex = 0; vertex < points.Length; vertex++)
            {
                string name = part.Vertices[vertex].Label;
                if (name.Length == 0) continue;
                PointF point = points[vertex];
                float dx = point.X - cx, dy = point.Y - cy;
                float length = Math.Max(1, MathF.Sqrt(dx * dx + dy * dy));
                canvas.FillColor = outline;
                canvas.FillCircle(point.X, point.Y, 2.5f);
                Label(name, new(point.X + 16 * dx / length, point.Y + 16 * dy / length), 28);
            }
        }
        canvas.FontColor = text;
        foreach (var annotation in annotations)
            Label(annotation.Text, Map(annotation.X, annotation.Y), Math.Clamp(annotation.Text.Length * 8, 50, 130));
    }
}
