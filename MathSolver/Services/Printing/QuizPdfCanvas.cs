using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Text;
using PdfSharp.Drawing;
using System.Numerics;

namespace MathSolver.Services.Printing;

/// <summary>Vector adapter for the existing quiz drawings, with a fixed light print palette.</summary>
internal sealed class QuizPdfCanvas(XGraphics graphics) : ICanvas
{
    private sealed record State(float StrokeSize, Color Stroke, Color Fill, float FontSize, IFont Font, float Alpha, float[]? Dash);
    private readonly Stack<(XGraphicsState Transform, State Style)> _states = new();
    public float DisplayScale { get; set; } = 1;
    public float StrokeSize { get; set; } = 1;
    public float MiterLimit { get; set; } = 10;
    public Color StrokeColor { get; set; } = Colors.Black;
    public Color FillColor { get; set; } = Colors.White;
    public Color FontColor { get; set; } = Colors.Black;
    public IFont Font { get; set; } = Microsoft.Maui.Graphics.Font.Default;
    public float FontSize { get; set; } = 12;
    public float Alpha { get; set; } = 1;
    public LineCap StrokeLineCap { get; set; }
    public LineJoin StrokeLineJoin { get; set; }
    public float[]? StrokeDashPattern { get; set; }
    public float StrokeDashOffset { get; set; }
    public bool Antialias { get; set; } = true;
    public BlendMode BlendMode { get; set; }
    private static XColor Gray(Color value, float alpha = 1)
    {
        double gray = .299 * value.Red + .587 * value.Green + .114 * value.Blue;
        double opacity = value.Alpha * alpha;
        int channel = (int)Math.Clamp((gray * opacity + 1 - opacity) * 255, 0, 255);
        return XColor.FromArgb(channel, channel, channel);
    }
    private XPen Pen()
    {
        var pen = new XPen(XColors.Black, StrokeSize);
        if (StrokeDashPattern is { Length: > 0 }) pen.DashPattern = StrokeDashPattern.Select(v => Math.Max(.1, (double)v)).ToArray();
        return pen;
    }
    private XBrush Brush() => new XSolidBrush(Gray(FillColor, Alpha));
    private XFont TextFont(float? size = null, IFont? font = null) => new("QuizPrint", size ?? FontSize,
        (font ?? Font).Weight >= 600 ? XFontStyleEx.Bold : XFontStyleEx.Regular);
    public void DrawLine(float x1, float y1, float x2, float y2) => graphics.DrawLine(Pen(), x1, y1, x2, y2);
    public void DrawRectangle(float x, float y, float w, float h) => graphics.DrawRectangle(Pen(), x, y, w, h);
    public void FillRectangle(float x, float y, float w, float h) => graphics.DrawRectangle(Brush(), x, y, w, h);
    public void DrawRoundedRectangle(float x, float y, float w, float h, float radius) => graphics.DrawRoundedRectangle(Pen(), x, y, w, h, radius * 2, radius * 2);
    public void FillRoundedRectangle(float x, float y, float w, float h, float radius) => graphics.DrawRoundedRectangle(Brush(), x, y, w, h, radius * 2, radius * 2);
    public void DrawEllipse(float x, float y, float w, float h) => graphics.DrawEllipse(Pen(), x, y, w, h);
    public void FillEllipse(float x, float y, float w, float h) => graphics.DrawEllipse(Brush(), x, y, w, h);
    private static XGraphicsPath Path(PathF source)
    {
        using var flattened = source.GetFlattenedPath(.2f, true);
        var result = new XGraphicsPath(); PointF previous = default;
        for (int i = 0; i < flattened.OperationCount; i++)
        {
            var operation = flattened.GetSegmentType(i);
            var points = flattened.GetPointsForSegment(i);
            if (operation == PathOperation.Move) { result.StartFigure(); previous = points[0]; }
            else if (operation == PathOperation.Line)
            { result.AddLine(previous.X, previous.Y, points[0].X, points[0].Y); previous = points[0]; }
            else if (operation == PathOperation.Close) result.CloseFigure();
        }
        return result;
    }
    public void DrawPath(PathF path) => graphics.DrawPath(Pen(), Path(path));
    public void FillPath(PathF path, WindingMode windingMode = WindingMode.NonZero) => graphics.DrawPath(Brush(), Path(path));
    public void ClipPath(PathF path, WindingMode windingMode = WindingMode.NonZero) => graphics.IntersectClip(Path(path));
    public void ClipRectangle(float x, float y, float w, float h) => graphics.IntersectClip(new XRect(x, y, w, h));
    public void SubtractFromClip(float x, float y, float w, float h) => throw new NotSupportedException("Print drawings do not subtract clips.");
    private static PathF Arc(float x, float y, float w, float h, float start, float end, bool clockwise, bool closed)
    {
        var path = new PathF();
        double span = end - start;
        if (clockwise && span > 0) span -= 360;
        if (!clockwise && span < 0) span += 360;
        int steps = Math.Max(2, (int)Math.Abs(span));
        for (int step = 0; step <= steps; step++)
        {
            double angle = (start + span * step / steps) * Math.PI / 180;
            float px = x + w / 2 + w / 2 * (float)Math.Cos(angle), py = y + h / 2 - h / 2 * (float)Math.Sin(angle);
            if (step == 0) path.MoveTo(px, py); else path.LineTo(px, py);
        }
        if (closed) path.Close(); return path;
    }
    public void DrawArc(float x, float y, float w, float h, float start, float end, bool clockwise, bool closed) { using var p = Arc(x, y, w, h, start, end, clockwise, closed); DrawPath(p); }
    public void FillArc(float x, float y, float w, float h, float start, float end, bool clockwise) { using var p = Arc(x, y, w, h, start, end, clockwise, true); FillPath(p); }
    public void DrawString(string value, float x, float y, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var size = graphics.MeasureString(value, TextFont());
        double left = alignment == HorizontalAlignment.Center ? x - size.Width / 2 : alignment == HorizontalAlignment.Right ? x - size.Width : x;
        graphics.DrawString(value, TextFont(), XBrushes.Black, left, y);
    }
    public void DrawString(string value, float x, float y, float w, float h, HorizontalAlignment horizontal,
        VerticalAlignment vertical, TextFlow flow = TextFlow.ClipBounds, float spacing = 0)
    {
        var font = TextFont();
        var lines = QuizPdfText.Wrap(graphics, value, font, w);
        double height = lines.Sum(line => line.Height);
        double top = y + (vertical == VerticalAlignment.Center ? Math.Max(0, (h - height) / 2) : vertical == VerticalAlignment.Bottom ? Math.Max(0, h - height) : 0);
        foreach (var line in lines)
        {
            double left = x + (horizontal == HorizontalAlignment.Center ? (w - line.Width) / 2 : horizontal == HorizontalAlignment.Right ? w - line.Width : 0);
            QuizPdfText.DrawLine(graphics, line, left, top); top += line.Height;
        }
    }
    public SizeF GetStringSize(string value, IFont font, float size) { var measured = graphics.MeasureString(value, TextFont(size, font)); return new((float)measured.Width, (float)measured.Height); }
    public SizeF GetStringSize(string value, IFont font, float size, HorizontalAlignment h, VerticalAlignment v) => GetStringSize(value, font, size);
    public void Translate(float x, float y) => graphics.TranslateTransform(x, y);
    public void Scale(float x, float y) => graphics.ScaleTransform(x, y);
    public void Rotate(float angle) => graphics.RotateTransform(angle);
    public void Rotate(float angle, float x, float y) => graphics.RotateAtTransform(angle, new XPoint(x, y));
    public void ConcatenateTransform(Matrix3x2 m) => graphics.MultiplyTransform(new XMatrix(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32));
    public void SaveState() => _states.Push((graphics.Save(), new(StrokeSize, StrokeColor, FillColor, FontSize, Font, Alpha, StrokeDashPattern)));
    public bool RestoreState()
    {
        if (!_states.TryPop(out var saved)) return false;
        graphics.Restore(saved.Transform); var s = saved.Style;
        StrokeSize = s.StrokeSize; StrokeColor = s.Stroke; FillColor = s.Fill; FontSize = s.FontSize; Font = s.Font; Alpha = s.Alpha; StrokeDashPattern = s.Dash;
        return true;
    }
    public void ResetState() { while (RestoreState()) { } }
    public void SetShadow(SizeF offset, float blur, Color color) { /* Omit decorative shadows on paper. */ }
    public void SetFillPaint(Paint paint, RectF rectangle) => throw new NotSupportedException("Print quiz drawings use solid fills.");
    public void DrawImage(Microsoft.Maui.Graphics.IImage image, float x, float y, float w, float h) => throw new NotSupportedException("Print quiz drawings use vector data.");
    public void DrawText(IAttributedText text, float x, float y, float w, float h) => throw new NotSupportedException("Print quiz drawings use plain or mathematical text.");
}
