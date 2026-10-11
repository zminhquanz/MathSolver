using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

/// <summary>
/// An ambient mathematical sketchbook: geometric constructions, a unit-circle
/// tracer, rotating solids and a quadratic graph. Bounded native primitives and
/// reusable projection buffers keep the centre quiet and drawing work predictable.
/// </summary>
public sealed class MathAnimatedBackgroundDrawable : ITimeDrivenDrawable
{
    private static readonly (int A, int B)[] CubeEdges =
    [
        (0, 1), (1, 3), (3, 2), (2, 0), (4, 5), (5, 7),
        (7, 6), (6, 4), (0, 4), (1, 5), (2, 6), (3, 7)
    ];
    private static readonly Glyph[] Glyphs =
    [
        new("a² + b² = c²", .15f, .38f, 17),
        new("πr²", .86f, .40f, 17),
        new("S = a × b", .12f, .53f, 16),
        new("V = a³", .15f, .92f, 17),
        new("ax + b = 0", .44f, .08f, 16),
        new("x = −b / a", .58f, .93f, 16),
        new("Δ = b² − 4ac", .85f, .60f, 17),
        new("(a + b)² = a² + 2ab + b²", .46f, .97f, 15)
    ];

    private readonly PointF[] _cube = new PointF[8];
    private readonly float[] _depth = new float[8];
    private Color? _sourceAccent;
    private Color? _sourceInk;
    private Color _accent = Colors.Purple;
    private Color _construction = Colors.SlateGray;
    private Color _highlight = Colors.Teal;
    private float _visibility = 1;

    public double TimeSeconds { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!float.IsFinite(dirtyRect.X) || !float.IsFinite(dirtyRect.Y) ||
            !float.IsFinite(dirtyRect.Width) || !float.IsFinite(dirtyRect.Height) ||
            dirtyRect.Width < 80 || dirtyRect.Height < 80)
            return;

        double time = double.IsFinite(TimeSeconds) ? Math.Max(0, TimeSeconds) : 0;
        bool compact = dirtyRect.Width < 600 || dirtyRect.Height < 480;
        RefreshPalette();
        canvas.SaveState();
        try
        {
            canvas.ClipRectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
            canvas.Antialias = true;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            DrawDraftingGrid(canvas, dirtyRect, time, compact);
            DrawTriangle(canvas, dirtyRect, time, compact);
            DrawCircleConstruction(canvas, dirtyRect, time, compact);
            DrawSolids(canvas, dirtyRect, time, compact);
            DrawQuadraticGraph(canvas, dirtyRect, time, compact);
            if (!compact) DrawRectangleConstruction(canvas, dirtyRect, time);
            DrawGlyphs(canvas, dirtyRect, time, compact);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void RefreshPalette()
    {
        Color accent = ThemeResource.GetColor("PrimaryColor", "#6D28D9");
        Color ink = ThemeResource.GetColor("TextPrimaryColor", "#1E293B");
        if (accent.Equals(_sourceAccent) && ink.Equals(_sourceInk)) return;
        _sourceAccent = accent;
        _sourceInk = ink;
        bool dark = ink.Red * .299f + ink.Green * .587f + ink.Blue * .114f > .65f;
        _accent = dark ? Mix(accent, Colors.White, .45f) : accent;
        _construction = Mix(ink, _accent, .35f);
        _highlight = Mix(_accent, Color.FromArgb(dark ? "#7DD3FC" : "#0284C7"), .65f);
        _visibility = dark ? 1.18f : 1;
    }

    private void DrawDraftingGrid(ICanvas canvas, RectF rect, double time, bool compact)
    {
        // A stationary grid avoids wrap jumps; its brightness breathes slowly.
        float spacing = MathF.Max(compact ? 44 : 64, MathF.Max(rect.Width, rect.Height) / 48);
        canvas.StrokeColor = _accent;
        canvas.StrokeSize = .65f;
        float breath = .92f + .08f * Wave(time, .16);
        for (int column = 0; column * spacing <= rect.Width; column++)
        {
            float x = rect.Left + column * spacing;
            canvas.Alpha = .055f * breath * _visibility * EdgeWeight(column * spacing / rect.Width);
            canvas.DrawLine(x, rect.Top, x, rect.Bottom);
        }
        for (int row = 0; row * spacing <= rect.Height; row++)
        {
            float y = rect.Top + row * spacing;
            for (int segment = 0; segment < 8; segment++)
            {
                float start = segment / 8f;
                canvas.Alpha = .055f * breath * _visibility * EdgeWeight(start + 1 / 16f);
                canvas.DrawLine(rect.Left + start * rect.Width, y,
                    rect.Left + (start + 1 / 8f) * rect.Width, y);
            }
        }
        canvas.StrokeColor = _construction;
        canvas.Alpha = .10f * _visibility;
        for (int side = 0; side < 2; side++)
        for (int i = 0; i < (compact ? 3 : 5); i++)
        {
            float x = rect.Left + rect.Width * (side == 0 ? .045f : .955f);
            float y = rect.Top + rect.Height * (.12f + i * .18f);
            canvas.DrawLine(x - 3, y, x + 3, y);
            canvas.DrawLine(x, y - 3, x, y + 3);
        }
    }

    private void DrawTriangle(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float size = MotifSize(rect, compact, 170, .31f);
        PointF centre = Anchor(rect, compact ? .25f : .15f, .21f, size * .68f);
        PointF left = new(centre.X - size * .52f, centre.Y + size * .35f);
        PointF right = new(centre.X + size * .52f, left.Y);
        PointF apex = new(centre.X + size * .16f * Wave(time, .20), centre.Y - size * .45f);
        PointF foot = new(apex.X, left.Y);
        Style(canvas, _accent, 1.5f, .25f);
        Line(canvas, left, right); Line(canvas, left, apex); Line(canvas, apex, right);
        Style(canvas, _construction, .9f, .15f);
        DashedLine(canvas, apex, foot, 10);
        float corner = size * .055f;
        canvas.DrawLine(foot.X, foot.Y - corner, foot.X + corner, foot.Y - corner);
        canvas.DrawLine(foot.X + corner, foot.Y - corner, foot.X + corner, foot.Y);
        Arc(canvas, left, size * .17f, MathF.Atan2(apex.Y - left.Y, apex.X - left.X), 0, 12);
        Style(canvas, _highlight, 1, .30f);
        Node(canvas, apex, compact ? 2.4f : 3.2f);
        Label(canvas, rect, "A", apex.X, apex.Y - 13, compact ? 11 : 13, .22f);
        Label(canvas, rect, "B", left.X - 8, left.Y + 12, compact ? 11 : 13, .18f);
        Label(canvas, rect, "C", right.X + 8, right.Y + 12, compact ? 11 : 13, .18f);
        Label(canvas, rect, "h", foot.X + 12, (apex.Y + foot.Y) / 2, 12, .19f);
        Label(canvas, rect, "S = ½bh", centre.X, left.Y + 32, compact ? 12 : 15, .22f, size + 28);
    }

    private void DrawCircleConstruction(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float radius = MotifSize(rect, compact, 170, .31f) * .45f;
        PointF centre = Anchor(rect, compact ? .77f : .85f, .23f, radius + 26);
        Style(canvas, _construction, .8f, .12f);
        canvas.DrawLine(centre.X - radius * 1.2f, centre.Y, centre.X + radius * 1.2f, centre.Y);
        canvas.DrawLine(centre.X, centre.Y - radius * 1.2f, centre.X, centre.Y + radius * 1.2f);
        Style(canvas, _accent, 1.4f, .24f);
        canvas.DrawCircle(centre.X, centre.Y, radius);
        float angle = Angle(time, .19);
        PointF tracer = new(centre.X + MathF.Cos(angle) * radius, centre.Y - MathF.Sin(angle) * radius);
        PointF projection = new(tracer.X, centre.Y);
        Line(canvas, centre, tracer);
        Style(canvas, _construction, .8f, .16f);
        DashedLine(canvas, tracer, projection, 8);
        Style(canvas, _highlight, 1.6f, .26f);
        Line(canvas, centre, projection);
        Arc(canvas, centre, radius, -angle, -angle + .46f, 12);
        Node(canvas, tracer, compact ? 2.8f : 3.8f);
        Style(canvas, _construction, 1, .18f);
        Node(canvas, centre, 1.5f);
        Label(canvas, rect, "O", centre.X - 10, centre.Y + 12, 11, .18f);
        Label(canvas, rect, "r", (centre.X + tracer.X) / 2, (centre.Y + tracer.Y) / 2 - 10, 12, .22f);
        Label(canvas, rect, "x² + y² = r²", centre.X, centre.Y + radius + 25, compact ? 11 : 15, .21f, radius * 2 + 40);
    }

    private void DrawSolids(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float size = MotifSize(rect, compact, 150, .27f);
        PointF centre = Anchor(rect, compact ? .25f : .15f, .78f, size * .85f);
        float yaw = Angle(time, .105);
        float tilt = .37f + .09f * Wave(time, .13);
        float cy = MathF.Cos(yaw), sy = MathF.Sin(yaw), ct = MathF.Cos(tilt), st = MathF.Sin(tilt);
        for (int i = 0; i < 8; i++)
        {
            float x = (i & 1) == 0 ? -.5f : .5f;
            float y = (i & 2) == 0 ? -.5f : .5f;
            float z = (i & 4) == 0 ? -.5f : .5f;
            float rx = x * cy + z * sy;
            float rz = z * cy - x * sy;
            float ry = y * ct - rz * st;
            _depth[i] = y * st + rz * ct;
            _cube[i] = new(centre.X + rx * size, centre.Y + ry * size);
        }
        foreach (var (a, b) in CubeEdges)
        {
            float near = (_depth[a] + _depth[b] + 1.5f) / 3;
            Style(canvas, _accent, 1.25f, .10f + .16f * near);
            Line(canvas, _cube[a], _cube[b]);
        }
        Style(canvas, _highlight, 1, .22f);
        for (int i = 0; i < 8; i++) Node(canvas, _cube[i], 1.6f);
        Label(canvas, rect, "V = a³", centre.X, centre.Y + size * .78f, compact ? 12 : 15, .22f, size + 30);
        if (compact) return;

        float pyramidSize = size * .57f;
        PointF pc = Anchor(rect, .12f, .60f, pyramidSize);
        PointF top = new(pc.X, pc.Y - pyramidSize * .65f);
        PointF previous = default, first = default;
        Style(canvas, _construction, 1, .17f);
        for (int i = 0; i < 4; i++)
        {
            float theta = Angle(time, -.08) + i * MathF.PI / 2;
            PointF p = new(pc.X + MathF.Cos(theta) * pyramidSize * .60f,
                pc.Y + pyramidSize * .20f + MathF.Sin(theta) * pyramidSize * .21f);
            if (i == 0) first = p;
            else Line(canvas, previous, p);
            Line(canvas, top, p);
            previous = p;
        }
        Line(canvas, previous, first);
    }

    private void DrawQuadraticGraph(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float width = MotifSize(rect, compact, 225, .40f);
        float height = width * .72f;
        PointF centre = Anchor(rect, compact ? .76f : .85f, .77f, width * .58f);
        float bottom = centre.Y + height * .36f;
        Style(canvas, _construction, .85f, .15f);
        canvas.DrawLine(centre.X - width / 2, bottom, centre.X + width / 2, bottom);
        canvas.DrawLine(centre.X, bottom + height * .12f, centre.X, centre.Y - height * .58f);
        for (int i = -2; i <= 2; i++)
            canvas.DrawLine(centre.X + i * width * .20f, bottom - 3, centre.X + i * width * .20f, bottom + 3);
        const int segments = 40;
        Style(canvas, _accent, 1.5f, .25f);
        PointF previous = default;
        for (int i = 0; i <= segments; i++)
        {
            PointF point = GraphPoint(centre.X, bottom, width, height, i / (float)segments * 2 - 1);
            if (i > 0) Line(canvas, previous, point);
            previous = point;
        }
        float probeX = Wave(time, .24);
        PointF probe = GraphPoint(centre.X, bottom, width, height, probeX);
        Style(canvas, _construction, .85f, .16f);
        DashedLine(canvas, probe, new(probe.X, bottom), 8);
        Style(canvas, _highlight, 1, .32f);
        Node(canvas, probe, compact ? 2.8f : 3.6f);
        Label(canvas, rect, "x", centre.X + width / 2 + 8, bottom, 11, .20f);
        Label(canvas, rect, "y", centre.X + 10, centre.Y - height * .58f, 11, .20f);
        Label(canvas, rect, "y = x²", centre.X, bottom + 26, compact ? 12 : 15, .22f, width);
    }

    private void DrawRectangleConstruction(ICanvas canvas, RectF rect, double time)
    {
        float width = MathF.Min(145, rect.Width * .12f);
        float height = width * (.55f + .045f * Wave(time, .17));
        PointF c = Anchor(rect, .87f, .50f, width * .7f);
        PointF a = new(c.X - width / 2, c.Y - height / 2);
        PointF b = new(c.X + width / 2, a.Y);
        PointF d = new(a.X, c.Y + height / 2);
        PointF e = new(b.X, d.Y);
        Style(canvas, _accent, 1.2f, .20f);
        Line(canvas, a, b); Line(canvas, b, e); Line(canvas, e, d); Line(canvas, d, a);
        Style(canvas, _construction, .8f, .12f);
        DashedLine(canvas, a, e, 12); DashedLine(canvas, b, d, 12);
        Label(canvas, rect, "a", c.X, a.Y - 13, 12, .18f);
        Label(canvas, rect, "b", b.X + 12, c.Y, 12, .18f);
    }

    private void DrawGlyphs(ICanvas canvas, RectF rect, double time, bool compact)
    {
        for (int i = 0; i < Glyphs.Length; i++)
        {
            if (compact && i is not (4 or 5)) continue;
            Glyph glyph = Glyphs[i];
            float x = rect.Left + rect.Width * glyph.X + 6 * Wave(time, .12, i);
            float y = rect.Top + rect.Height * (compact ? (i == 4 ? .46f : .54f) : glyph.Y) + 4 * Wave(time, .15, i * 1.4);
            float fontSize = compact ? 12 : glyph.Size;
            float alpha = .105f + .035f * Wave(time, .25, i);
            Label(canvas, rect, glyph.Text, x, y, fontSize, alpha, glyph.Text.Length * fontSize * .62f + 14);
        }
    }

    private void Style(ICanvas canvas, Color color, float stroke, float alpha)
    {
        canvas.StrokeColor = color;
        canvas.FillColor = color;
        canvas.StrokeSize = stroke;
        canvas.Alpha = alpha * _visibility;
    }

    private void Label(ICanvas canvas, RectF rect, string text, float x, float y,
        float size, float alpha, float width = 28)
    {
        width = MathF.Min(width, rect.Width - 8);
        const float height = 24;
        float left = Math.Clamp(x - width / 2, rect.Left + 4, rect.Right - width - 4);
        float top = Math.Clamp(y - height / 2, rect.Top + 4, rect.Bottom - height - 4);
        canvas.FontColor = _accent;
        canvas.FontSize = size;
        canvas.Alpha = alpha * _visibility;
        canvas.DrawString(text, left, top, width, height, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static PointF GraphPoint(float x, float bottom, float width, float height, float value) =>
        new(x + value * width * .46f, bottom - value * value * height * .84f);
    private static float MotifSize(RectF rect, bool compact, float maximum, float fraction) =>
        MathF.Min(maximum, MathF.Min(rect.Width * (compact ? fraction : .19f), rect.Height * .22f));
    private static PointF Anchor(RectF rect, float x, float y, float margin) =>
        new(rect.Left + Math.Clamp(rect.Width * x, margin, rect.Width - margin),
            rect.Top + Math.Clamp(rect.Height * y, margin, rect.Height - margin));
    private static float EdgeWeight(float x) => .25f + .75f * MathF.Pow(MathF.Abs(2 * x - 1), 1.6f);
    private static float Angle(double time, double speed) => (float)((time * speed) % Math.Tau);
    private static float Wave(double time, double speed, double phase = 0) =>
        MathF.Sin((float)(((time * speed) % Math.Tau + phase) % Math.Tau));
    private static Color Mix(Color a, Color b, float t) =>
        new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t);
    private static void Line(ICanvas canvas, PointF a, PointF b) => canvas.DrawLine(a.X, a.Y, b.X, b.Y);
    private static void Node(ICanvas canvas, PointF point, float radius) => canvas.FillCircle(point.X, point.Y, radius);
    private static void DashedLine(ICanvas canvas, PointF a, PointF b, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments, end = (i + .48f) / segments;
            canvas.DrawLine(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t,
                a.X + (b.X - a.X) * end, a.Y + (b.Y - a.Y) * end);
        }
    }
    private static void Arc(ICanvas canvas, PointF centre, float radius, float start, float end, int segments)
    {
        PointF previous = new(centre.X + MathF.Cos(start) * radius, centre.Y + MathF.Sin(start) * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = start + (end - start) * i / segments;
            PointF p = new(centre.X + MathF.Cos(angle) * radius, centre.Y + MathF.Sin(angle) * radius);
            Line(canvas, previous, p);
            previous = p;
        }
    }
    private readonly record struct Glyph(string Text, float X, float Y, float Size);
}
