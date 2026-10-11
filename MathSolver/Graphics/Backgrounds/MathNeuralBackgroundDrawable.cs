using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

/// <summary>
/// Mathematical constellations, a layered neural network feeding an AI processor,
/// rotating wireframes and flowing curves. Geometry is deterministic and buffers are reused, keeping the
/// learning area calm without images, gradients, shadows or per-frame collections.
/// </summary>
public sealed class MathNeuralBackgroundDrawable : ITimeDrivenDrawable
{
    private static readonly PointF[] Anchors =
    [
        new(.06f, .11f), new(.14f, .08f), new(.22f, .15f), new(.10f, .23f),
        new(.20f, .29f), new(.04f, .36f), new(.14f, .42f), new(.26f, .38f),
        new(.08f, .55f), new(.19f, .60f), new(.05f, .69f), new(.25f, .73f),
        new(.12f, .85f), new(.22f, .94f), new(.04f, .93f), new(.32f, .87f),
        new(.76f, .07f), new(.91f, .10f), new(.84f, .26f), new(.96f, .31f),
        new(.73f, .35f), new(.89f, .43f), new(.98f, .53f), new(.80f, .57f),
        new(.91f, .66f), new(.74f, .73f), new(.98f, .80f), new(.86f, .87f),
        new(.71f, .94f), new(.95f, .96f), new(.39f, .10f), new(.63f, .90f)
    ];

    private static readonly (int A, int B)[] Links =
    [
        (0, 1), (1, 2), (0, 3), (1, 3), (2, 4), (3, 4), (3, 5),
        (4, 6), (5, 6), (6, 7), (6, 8), (8, 9), (8, 10), (9, 11),
        (10, 12), (11, 12), (12, 13), (12, 14), (13, 15),
        (16, 17), (17, 18), (18, 19), (18, 20), (19, 21), (20, 21),
        (21, 22), (21, 23), (22, 24), (23, 24), (23, 25), (24, 26),
        (25, 27), (26, 27), (27, 28), (27, 29), (28, 31), (2, 30)
    ];

    private static readonly (int A, int B)[] CubeEdges =
    [
        (0, 1), (1, 3), (3, 2), (2, 0), (4, 5), (5, 7),
        (7, 6), (6, 4), (0, 4), (1, 5), (2, 6), (3, 7)
    ];

    private static readonly Glyph[] Glyphs =
    [
        new("π", .06f, .46f, 25), new("√", .25f, .08f, 23),
        new("x²", .06f, .79f, 19), new("Σ", .94f, .57f, 24),
        new("a² + b² = c²", .17f, .49f, 14),
        new("x² + y² = r²", .84f, .31f, 14),
        new("y = sin x", .83f, .92f, 14),
        new("(a + b)² = a² + 2ab + b²", .35f, .96f, 13)
    ];

    private readonly PointF[] _nodes = new PointF[Anchors.Length];
    private static readonly int[] NeuralLayerSizes = [3, 4, 3];
    private readonly PointF[] _neurons = new PointF[10];
    private readonly PointF[] _cube = new PointF[8];
    private readonly float[] _cubeDepth = new float[8];
    private Color? _sourceAccent;
    private Color? _sourceInk;
    private Color _accent = Colors.Purple;
    private Color _cool = Colors.Teal;
    private Color _ink = Colors.Black;

    public double TimeSeconds { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!float.IsFinite(dirtyRect.X) || !float.IsFinite(dirtyRect.Y) ||
            !float.IsFinite(dirtyRect.Width) || !float.IsFinite(dirtyRect.Height) ||
            dirtyRect.Width < 80 || dirtyRect.Height < 80)
            return;

        // Bound angles after long sessions; never pass a bad clock into native drawing.
        double time = double.IsFinite(TimeSeconds) ? Math.Max(0, TimeSeconds) : 0;
        bool compact = dirtyRect.Width < 600;
        RefreshPalette();
        canvas.SaveState();
        try
        {
            canvas.ClipRectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);
            canvas.Antialias = true;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            DrawDust(canvas, dirtyRect, time, compact);
            DrawContours(canvas, dirtyRect, time, compact);
            DrawNetwork(canvas, dirtyRect, time, compact);
            DrawNeuralProcessor(canvas, dirtyRect, time, compact);
            DrawOrbitalSphere(canvas, dirtyRect, time, compact);
            DrawCube(canvas, dirtyRect, time, compact);
            DrawGraph(canvas, dirtyRect, time, compact);
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
        _accent = dark ? Mix(accent, Colors.White, .48f) : accent;
        _cool = Mix(_accent, Color.FromArgb(dark ? "#67E8F9" : "#0891B2"), .65f);
        _ink = Mix(ink, _accent, .30f);
    }

    private void DrawDust(ICanvas canvas, RectF rect, double time, bool compact)
    {
        canvas.FillColor = _cool;
        int count = compact ? 18 : 44;
        for (int i = 0; i < count; i++)
        {
            // Irrational strides distribute stars without a changing random seed.
            float nx = Fract(i * .61803398875 + .13);
            float ny = Fract(i * .41421356237 + .07);
            float x = rect.Left + rect.Width * nx + Wave(time, .08, i) * 3;
            float y = rect.Top + rect.Height * ny + Wave(time, .06, i * 2) * 4;
            canvas.Alpha = EdgeOpacity(nx, compact) * (.09f + .05f * Wave(time, .32, i));
            canvas.FillCircle(x, y, i % 5 == 0 ? 1.4f : .8f);
        }
    }

    private void DrawContours(ICanvas canvas, RectF rect, double time, bool compact)
    {
        const int segments = 32;
        canvas.StrokeColor = _cool;
        canvas.StrokeSize = .8f;
        int bands = compact ? 2 : 3;
        for (int side = 0; side < 2; side++)
        for (int band = 0; band < bands; band++)
        {
            PointF previous = default;
            for (int step = 0; step <= segments; step++)
            {
                float nx = step / (float)segments;
                float envelope = MathF.Sin(nx * MathF.PI);
                float ny = (side == 0 ? .045f : .965f) +
                    (side == 0 ? 1 : -1) * (.017f * band + envelope * .024f *
                    Wave(time, .12, nx * 8 + band * .65));
                PointF p = new(rect.Left + nx * rect.Width, rect.Top + ny * rect.Height);
                canvas.Alpha = EdgeOpacity(nx, compact) * .12f;
                if (step > 0) Line(canvas, previous, p);
                previous = p;
            }
        }
    }

    private void DrawNetwork(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float drift = MathF.Min(10, MathF.Min(rect.Width, rect.Height) * .018f);
        for (int i = 0; i < Anchors.Length; i++)
        {
            PointF anchor = Anchors[i];
            float nx = compact ? .5f + (anchor.X - .5f) * 1.08f : anchor.X;
            _nodes[i] = new(rect.Left + nx * rect.Width + Wave(time, .13, i * .71) * drift,
                rect.Top + anchor.Y * rect.Height + Wave(time, .11, i * .93) * drift);
        }

        canvas.StrokeSize = 1;
        for (int i = 0; i < Links.Length; i++)
        {
            var (a, b) = Links[i];
            PointF start = _nodes[a], end = _nodes[b];
            float weight = EdgeOpacity((Anchors[a].X + Anchors[b].X) / 2, compact);
            canvas.StrokeColor = i % 3 == 0 ? _cool : _accent;
            canvas.Alpha = weight * (.15f + .03f * Wave(time, .24, i));
            Line(canvas, start, end);

            // Signals fade at endpoints, so wrapping the clock cannot make them jump.
            if (i % (compact ? 4 : 3) != 0) continue;
            float progress = Fract(time * (.07 + i % 4 * .008) + i * .137);
            float fade = MathF.Sin(progress * MathF.PI);
            PointF signal = Lerp(start, end, progress);
            canvas.FillColor = i % 2 == 0 ? _cool : _accent;
            canvas.Alpha = weight * fade * .07f;
            canvas.FillCircle(signal.X, signal.Y, 5);
            canvas.Alpha = weight * fade * .50f;
            canvas.FillCircle(signal.X, signal.Y, 1.6f);
        }

        for (int i = 0; i < _nodes.Length; i++)
        {
            PointF p = _nodes[i];
            float pulse = .5f + .5f * Wave(time, .45, i * .83);
            float weight = EdgeOpacity(Anchors[i].X, compact);
            canvas.FillColor = i % 3 == 0 ? _cool : _accent;
            canvas.Alpha = weight * .045f;
            canvas.FillCircle(p.X, p.Y, 6 + pulse * 2);
            canvas.Alpha = weight * (.30f + pulse * .12f);
            canvas.FillCircle(p.X, p.Y, 1.7f + pulse * .3f);
            if (i % 4 != 0) continue;
            canvas.StrokeColor = i % 3 == 0 ? _cool : _accent;
            canvas.Alpha = weight * .12f;
            canvas.DrawCircle(p.X, p.Y, 4 + pulse * 1.5f);
        }
    }

    private void DrawNeuralProcessor(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float width = MathF.Min(compact ? 120 : 218, rect.Width * .28f);
        float height = MathF.Min(compact ? 104 : 144, rect.Height * .18f);
        float left = rect.Left + MathF.Min(60, rect.Width * .045f);
        float centerY = rect.Top + rect.Height * .25f + Wave(time, .10, 2) * 3;
        float cycle = Fract(time * .10);
        float nodeRadius = compact ? 3.3f : 4.5f;
        int offset = 0;
        for (int layer = 0; layer < NeuralLayerSizes.Length; layer++)
        {
            int count = NeuralLayerSizes[layer];
            for (int row = 0; row < count; row++)
                _neurons[offset + row] = new(left + layer * width / 2,
                    centerY + (row - (count - 1) / 2f) * height / 4);
            offset += count;
        }

        // Fully connected neighbouring layers, with one coherent signal pass
        // travelling from inputs through the hidden layer to the AI processor.
        canvas.StrokeSize = .9f;
        int sourceOffset = 0;
        for (int layer = 0; layer < 2; layer++)
        {
            int sourceCount = NeuralLayerSizes[layer], targetCount = NeuralLayerSizes[layer + 1];
            int targetOffset = sourceOffset + sourceCount;
            float progress = Math.Clamp(cycle * 3 - layer, 0, 1);
            float fade = MathF.Max(0, MathF.Sin(progress * MathF.PI));
            for (int a = 0; a < sourceCount; a++)
            for (int b = 0; b < targetCount; b++)
            {
                PointF start = _neurons[sourceOffset + a], end = _neurons[targetOffset + b];
                canvas.StrokeColor = layer == 0 ? _cool : _accent;
                canvas.Alpha = compact ? .105f : .15f;
                Line(canvas, start, end);
                if ((a + b) % 3 != 0) continue;
                PointF signal = Lerp(start, end, progress);
                canvas.FillColor = layer == 0 ? _cool : _accent;
                canvas.Alpha = fade * .08f;
                canvas.FillCircle(signal.X, signal.Y, nodeRadius + 1);
                canvas.Alpha = fade * (compact ? .32f : .48f);
                canvas.FillCircle(signal.X, signal.Y, 1.5f);
            }
            sourceOffset = targetOffset;
        }

        offset = 0;
        for (int layer = 0; layer < NeuralLayerSizes.Length; layer++)
        {
            float activation = NeuralActivation(time, layer);
            for (int row = 0; row < NeuralLayerSizes[layer]; row++)
            {
                PointF node = _neurons[offset++];
                canvas.FillColor = layer == 1 ? _accent : _cool;
                canvas.Alpha = .035f + activation * .06f;
                canvas.FillCircle(node.X, node.Y, nodeRadius + 4 + activation * 2);
                canvas.Alpha = compact ? .07f : .10f;
                canvas.FillCircle(node.X, node.Y, nodeRadius);
                canvas.StrokeColor = layer == 1 ? _accent : _cool;
                canvas.StrokeSize = 1.2f;
                canvas.Alpha = (compact ? .20f : .28f) + activation * .18f;
                canvas.DrawCircle(node.X, node.Y, nodeRadius);
                canvas.FillColor = layer == 1 ? _accent : _cool;
                canvas.Alpha = activation * .50f;
                canvas.FillCircle(node.X, node.Y, 1.4f);
            }
        }

        float chipSize = MathF.Min(compact ? 36 : 52, rect.Width * .10f);
        float gap = compact ? 18 : 26;
        float chipLeft = left + width + gap;
        float chipRight = chipLeft + chipSize;
        float chipTop = centerY - chipSize / 2;
        float chipBottom = centerY + chipSize / 2;
        float chipCenterX = chipLeft + chipSize / 2;
        float pulse = NeuralActivation(time, 3);
        canvas.StrokeColor = _cool;
        canvas.StrokeSize = 1;
        for (int row = 0; row < NeuralLayerSizes[2]; row++)
        {
            PointF start = _neurons[7 + row];
            PointF end = new(chipLeft - 6, centerY + (row - 1) * chipSize / 5);
            canvas.Alpha = compact ? .14f : .20f;
            Line(canvas, start, end);
            float progress = Math.Clamp(cycle * 3 - 2, 0, 1);
            PointF signal = Lerp(start, end, progress);
            canvas.FillColor = _cool;
            canvas.Alpha = MathF.Max(0, MathF.Sin(progress * MathF.PI)) * .45f;
            canvas.FillCircle(signal.X, signal.Y, 1.5f);
        }

        // A chamfered chip with circuit pins makes AI recognizable even on phones.
        // Draw native lines and text; no font-icon or external image resource is needed.
        float corner = chipSize * .13f;
        canvas.StrokeColor = _accent;
        canvas.StrokeSize = 1.3f;
        canvas.Alpha = (compact ? .24f : .30f) + pulse * .12f;
        canvas.DrawLine(chipLeft + corner, chipTop, chipRight - corner, chipTop);
        canvas.DrawLine(chipRight - corner, chipTop, chipRight, chipTop + corner);
        canvas.DrawLine(chipRight, chipTop + corner, chipRight, chipBottom - corner);
        canvas.DrawLine(chipRight, chipBottom - corner, chipRight - corner, chipBottom);
        canvas.DrawLine(chipRight - corner, chipBottom, chipLeft + corner, chipBottom);
        canvas.DrawLine(chipLeft + corner, chipBottom, chipLeft, chipBottom - corner);
        canvas.DrawLine(chipLeft, chipBottom - corner, chipLeft, chipTop + corner);
        canvas.DrawLine(chipLeft, chipTop + corner, chipLeft + corner, chipTop);
        canvas.StrokeColor = _cool;
        canvas.Alpha = .16f + pulse * .08f;
        for (int pin = 1; pin <= 3; pin++)
        {
            float x = chipLeft + chipSize * pin / 4, y = chipTop + chipSize * pin / 4;
            canvas.DrawLine(x, chipTop - 5, x, chipTop);
            canvas.DrawLine(x, chipBottom, x, chipBottom + 5);
            canvas.DrawLine(chipLeft - 5, y, chipLeft, y);
            canvas.DrawLine(chipRight, y, chipRight + 5, y);
        }
        canvas.FontColor = _ink;
        canvas.FontSize = compact ? 14 : 20;
        canvas.Alpha = .26f + pulse * .12f;
        canvas.DrawString("AI", chipLeft, chipTop, chipSize, chipSize,
            HorizontalAlignment.Center, VerticalAlignment.Center);

        float labelWidth = MathF.Min(width + gap + chipSize + 16, rect.Width - 16);
        float labelLeft = Math.Clamp(left - 8, rect.Left + 8, rect.Right - labelWidth - 8);
        float labelTop = Math.Clamp(centerY - height / 2 - 28, rect.Top + 4, rect.Bottom - 28);
        canvas.FontSize = compact ? 11 : 13;
        canvas.FontColor = _ink;
        canvas.Alpha = compact ? .18f : .22f;
        canvas.DrawString("Neural Network", labelLeft, labelTop, labelWidth, 24,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    private static float NeuralActivation(double time, int layer)
    {
        float wave = .5f + .5f * Wave(time, Math.Tau * .10, Math.PI / 2 - layer * Math.Tau / 3);
        return wave * wave * wave;
    }

    private void DrawOrbitalSphere(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float radius = MathF.Min(compact ? 62 : 124, MathF.Min(rect.Width * .125f, rect.Height * .145f));
        PointF center = new(rect.Right - MathF.Max(rect.Width * .125f, radius * 1.35f + 12), rect.Top + rect.Height * .16f);
        float yaw = Angle(time, .055), tilt = .48f + Wave(time, .07, 0) * .12f;
        canvas.StrokeSize = 1;
        canvas.StrokeColor = _accent;
        // Three great circles and two parallels form a rotating sphere.
        for (int ring = 0; ring < 5; ring++)
        {
            PointF previous = default;
            for (int step = 0; step <= 48; step++)
            {
                float angle = step / 48f * MathF.Tau;
                float c = MathF.Cos(angle), s = MathF.Sin(angle);
                float x, y, z;
                if (ring == 0) { x = c; y = s; z = 0; }
                else if (ring == 1) { x = 0; y = c; z = s; }
                else if (ring == 2) { x = c; y = 0; z = s; }
                else { x = c * .8660254f; y = ring == 3 ? .5f : -.5f; z = s * .8660254f; }
                PointF p = Project(x, y, z, yaw, tilt, radius, center, out float depth);
                canvas.Alpha = (compact ? .18f : .22f) * (.38f + .62f * Math.Clamp((depth + 1) / 2, 0, 1));
                if (step > 0) Line(canvas, previous, p);
                previous = p;
            }
        }

        canvas.StrokeColor = _cool;
        float orbitYaw = Angle(time, -.0385);
        PointF prior = default;
        for (int step = 0; step <= 64; step++)
        {
            float angle = step / 64f * MathF.Tau;
            PointF p = Project(MathF.Cos(angle) * 1.35f, 0, MathF.Sin(angle) * 1.35f,
                orbitYaw, tilt + .65f, radius, center, out float depth);
            canvas.Alpha = .09f + .14f * Math.Clamp((depth / 1.35f + 1) / 2, 0, 1);
            if (step > 0) Line(canvas, prior, p);
            prior = p;
        }
        float orbit = Angle(time, .16);
        PointF satellite = Project(MathF.Cos(orbit) * 1.35f, 0, MathF.Sin(orbit) * 1.35f,
            orbitYaw, tilt + .65f, radius, center, out _);
        canvas.FillColor = _cool;
        canvas.Alpha = .10f;
        canvas.FillCircle(satellite.X, satellite.Y, 6);
        canvas.Alpha = .40f;
        canvas.FillCircle(satellite.X, satellite.Y, 2.3f);
    }

    private void DrawCube(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float size = MathF.Min(compact ? 34 : 62, MathF.Min(rect.Width * .09f, rect.Height * .08f));
        PointF center = new(rect.Left + rect.Width * .13f, rect.Top + rect.Height * .77f + Wave(time, .17, 2) * 6);
        float yaw = Angle(time, .075) + .5f;
        float tilt = .55f + Wave(time, .09, 1) * .2f;
        for (int i = 0; i < 8; i++)
            _cube[i] = Project((i & 1) == 0 ? -.65f : .65f,
                (i & 2) == 0 ? -.65f : .65f, (i & 4) == 0 ? -.65f : .65f,
                yaw, tilt, size, center, out _cubeDepth[i]);
        canvas.StrokeSize = 1.2f;
        foreach (var (a, b) in CubeEdges)
        {
            float depth = Math.Clamp((_cubeDepth[a] + _cubeDepth[b] + 2) / 4, 0, 1);
            canvas.StrokeColor = _accent;
            canvas.Alpha = (compact ? .22f : .28f) * (.4f + .6f * depth);
            Line(canvas, _cube[a], _cube[b]);
        }
        canvas.StrokeColor = _cool;
        canvas.Alpha = .07f;
        Line(canvas, _cube[0], _cube[7]);
    }

    private void DrawGraph(ICanvas canvas, RectF rect, double time, bool compact)
    {
        float width = MathF.Min(compact ? 110 : 230, rect.Width * .21f);
        float amplitude = MathF.Min(38, rect.Height * .05f);
        PointF center = new(rect.Left + rect.Width * .86f, rect.Top + rect.Height * .78f);
        canvas.StrokeColor = _ink;
        canvas.StrokeSize = .8f;
        canvas.Alpha = compact ? .07f : .10f;
        canvas.DrawLine(center.X - width / 2, center.Y, center.X + width / 2, center.Y);
        canvas.DrawLine(center.X, center.Y - amplitude * 1.35f, center.X, center.Y + amplitude * 1.35f);
        for (int tick = -3; tick <= 3; tick++)
        {
            float x = center.X + tick * width / 8;
            canvas.DrawLine(x, center.Y - 2, x, center.Y + 2);
        }
        float phase = Angle(time, .22);
        for (int curve = 0; curve < (compact ? 1 : 2); curve++)
        {
            canvas.StrokeColor = curve == 0 ? _accent : _cool;
            canvas.StrokeSize = curve == 0 ? 1.3f : .9f;
            canvas.Alpha = curve == 0 ? .28f : .14f;
            PointF previous = default;
            for (int step = 0; step <= 48; step++)
            {
                float u = step / 48f;
                PointF p = new(center.X + (u - .5f) * width, center.Y - MathF.Sin(u * MathF.Tau + phase + curve * 1.1f) * amplitude);
                if (step > 0) Line(canvas, previous, p);
                previous = p;
            }
        }
        float cursor = .5f + .42f * Wave(time, .12, 1);
        canvas.FillColor = _accent;
        canvas.Alpha = .34f;
        canvas.FillCircle(center.X + (cursor - .5f) * width, center.Y - MathF.Sin(cursor * MathF.Tau + phase) * amplitude, 2.2f);
    }

    private void DrawGlyphs(ICanvas canvas, RectF rect, double time, bool compact)
    {
        canvas.FontColor = _ink;
        int count = compact ? 4 : Glyphs.Length;
        for (int i = 0; i < count; i++)
        {
            Glyph glyph = Glyphs[i];
            float width = compact ? 44 : i < 4 ? 52 : i == 7 ? 240 : 160;
            float x = rect.Left + glyph.X * rect.Width + Wave(time, .10, i) * 4;
            float y = rect.Top + glyph.Y * rect.Height + Wave(time, .14, i * .8) * 5;
            x = Math.Clamp(x, rect.Left + width / 2, rect.Right - width / 2);
            y = Math.Clamp(y, rect.Top + 18, rect.Bottom - 18);
            canvas.FontSize = glyph.Size * (compact ? .85f : 1);
            canvas.Alpha = EdgeOpacity(glyph.X, compact) * (.18f + .04f * Wave(time, .23, i));
            canvas.DrawString(glyph.Text, x - width / 2, y - 18, width, 36, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static PointF Project(float x, float y, float z, float yaw, float tilt,
        float size, PointF center, out float depth)
    {
        float rx = x * MathF.Cos(yaw) + z * MathF.Sin(yaw);
        float rz = z * MathF.Cos(yaw) - x * MathF.Sin(yaw);
        float ry = y * MathF.Cos(tilt) - rz * MathF.Sin(tilt);
        depth = y * MathF.Sin(tilt) + rz * MathF.Cos(tilt);
        // Orthographic projection preserves parallel lines without perspective singularities.
        return new(center.X + rx * size, center.Y + ry * size);
    }

    private static float EdgeOpacity(float nx, bool compact)
    {
        float distance = MathF.Abs(nx - .5f) * 2;
        float t = Math.Clamp((distance - .12f) / .58f, 0, 1);
        return (.25f + .75f * t * t * (3 - 2 * t)) * (compact ? .72f : 1);
    }

    private static float Angle(double time, double speed) => (float)((time * speed) % Math.Tau);
    private static float Wave(double time, double speed, double phase) => MathF.Sin((float)(((time * speed) % Math.Tau + phase) % Math.Tau));
    private static float Fract(double value) => (float)(value - Math.Floor(value));
    private static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    private static void Line(ICanvas canvas, PointF a, PointF b) => canvas.DrawLine(a.X, a.Y, b.X, b.Y);
    private static Color Mix(Color a, Color b, float t) => new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t);
    private readonly record struct Glyph(string Text, float X, float Y, float Size);
}
