using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

/// <summary>
/// Ambient "neural network + mathematical space" animation for the second
/// built-in wallpaper mode. It is intentionally subtle and soft, mirroring the
/// low-contrast feel of the original math wallpaper while introducing an AI
/// brain, mathematical space, and an open-book motif.
/// </summary>
public sealed class MathNeuralBackgroundDrawable : ITimeDrivenDrawable
{
    private static readonly NodeSpec[] Nodes =
    [
        new(0.10f, 0.12f, 0.8f, 0.0f, 2.5f),
        new(0.18f, 0.26f, 0.9f, 0.6f, 2.2f),
        new(0.30f, 0.12f, 0.7f, 1.2f, 2.3f),
        new(0.24f, 0.42f, 1.1f, 1.7f, 2.7f),
        new(0.42f, 0.13f, 0.8f, 2.1f, 2.2f),
        new(0.55f, 0.26f, 0.9f, 2.7f, 2.5f),
        new(0.34f, 0.62f, 0.7f, 3.2f, 2.4f),
        new(0.62f, 0.62f, 0.8f, 3.8f, 2.6f),
        new(0.48f, 0.86f, 1.0f, 4.3f, 2.3f),
        new(0.12f, 0.86f, 0.9f, 4.9f, 2.5f),
        new(0.78f, 0.16f, 0.7f, 5.2f, 2.2f),
        new(0.84f, 0.32f, 0.8f, 5.8f, 2.4f)
    ];

    private static readonly (int Start, int End)[] Connections =
    [
        (0, 2),
        (1, 3),
        (2, 4),
        (3, 6),
        (4, 5),
        (5, 10),
        (10, 11),
        (6, 7),
        (6, 8),
        (8, 9)
    ];

    private static readonly SymbolSpec[] Symbols =
    [
        new("√", 0.09f, 0.39f, 0.5f, 0.2f, 28f),
        new("π", 0.31f, 0.20f, 0.6f, 1.1f, 20f),
        new("x²", 0.21f, 0.60f, 0.5f, 2.1f, 22f),
        new("Σ", 0.12f, 0.92f, 0.4f, 3.2f, 24f),
        new("Δ", 0.30f, 0.92f, 0.4f, 4.2f, 24f),
        new("ax+b=0", 0.78f, 0.24f, 0.35f, 5.0f, 14f),
        new("x=(-b±√Δ)/2a", 0.69f, 0.56f, 0.32f, 5.8f, 13f)
    ];

    public double TimeSeconds { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width < 80f || dirtyRect.Height < 80f)
        {
            return;
        }

        canvas.SaveState();

        try
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            DrawSpaceField(canvas, dirtyRect);
            DrawConnectionField(canvas, dirtyRect);
            DrawCentralBrainPolygon(canvas, dirtyRect);
            DrawOrbitEquation(canvas, dirtyRect);
            DrawOpenBook(canvas, dirtyRect);
            DrawSymbols(canvas, dirtyRect);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawSpaceField(ICanvas canvas, RectF rect)
    {
        Color primary = WithAlpha(GetForegroundColor(), 0.07f);
        Color secondary = WithAlpha(GetForegroundColor(), 0.05f);

        float ringX = rect.Left + rect.Width * 0.62f;
        float ringY = rect.Top + rect.Height * 0.36f;
        float width = MathF.Min(360f, rect.Width * 0.32f);
        float height = MathF.Min(170f, rect.Height * 0.20f);
        float pulse = MathF.Sin((float)(TimeSeconds * 0.22)) * 5f;

        canvas.StrokeSize = 1.2f;
        canvas.StrokeColor = primary;
        canvas.DrawEllipse(
            ringX - width / 2f,
            ringY - height / 2f,
            width,
            height);
        canvas.DrawEllipse(
            ringX - width * 0.38f,
            ringY - height * 0.26f,
            width * 0.76f,
            height * 0.52f);

        canvas.StrokeColor = secondary;
        canvas.DrawLine(
            rect.Left + rect.Width * 0.50f,
            ringY + pulse,
            rect.Left + rect.Width * 0.84f,
            ringY + pulse);
        canvas.DrawLine(
            rect.Left + rect.Width * 0.67f,
            rect.Top + rect.Height * 0.10f,
            rect.Left + rect.Width * 0.67f,
            rect.Top + rect.Height * 0.58f);
    }

    private void DrawConnectionField(ICanvas canvas, RectF rect)
    {
        Color lineColor = WithAlpha(GetForegroundColor(), 0.13f);
        Color dotColor = WithAlpha(GetForegroundColor(), 0.22f);

        PointF[] positions = new PointF[Nodes.Length];

        for (int i = 0; i < Nodes.Length; i++)
        {
            NodeSpec node = Nodes[i];
            float driftX = MathF.Sin((float)(TimeSeconds * 0.30 * node.Speed + node.Phase)) * 8f;
            float driftY = MathF.Cos((float)(TimeSeconds * 0.24 * node.Speed + node.Phase)) * 5f;
            positions[i] = new PointF(
                rect.Left + rect.Width * node.X + driftX,
                rect.Top + rect.Height * node.Y + driftY);
        }

        canvas.StrokeSize = 1.35f;
        canvas.StrokeColor = lineColor;

        foreach ((int start, int end) in Connections)
        {
            PointF a = positions[start];
            PointF b = positions[end];
            canvas.DrawLine(a.X, a.Y, b.X, b.Y);
        }

        canvas.FillColor = dotColor;
        for (int i = 0; i < positions.Length; i++)
        {
            float radius = Nodes[i].Radius;
            PointF p = positions[i];
            canvas.FillCircle(p.X, p.Y, radius);
        }
    }

    private void DrawCentralBrainPolygon(ICanvas canvas, RectF rect)
    {
        Color stroke = WithAlpha(GetForegroundColor(), 0.20f);
        canvas.StrokeColor = stroke;
        canvas.StrokeSize = 2f;

        float cx = rect.Left + rect.Width * 0.20f;
        float cy = rect.Top + rect.Height * 0.28f;
        float halfW = MathF.Min(rect.Width, rect.Height) * 0.055f;
        float halfH = halfW * 1.25f;
        float pulse = MathF.Sin((float)(TimeSeconds * 0.45)) * 2.4f;

        PointF[] points =
        [
            new(cx - halfW * 0.45f, cy - halfH),
            new(cx + halfW * 0.45f, cy - halfH),
            new(cx + halfW, cy - halfH * 0.35f),
            new(cx + halfW, cy + halfH * 0.35f),
            new(cx + halfW * 0.45f, cy + halfH),
            new(cx - halfW * 0.45f, cy + halfH),
            new(cx - halfW, cy + halfH * 0.35f),
            new(cx - halfW, cy - halfH * 0.35f)
        ];

        for (int i = 0; i < points.Length; i++)
        {
            PointF a = points[i];
            PointF b = points[(i + 1) % points.Length];

            float t0 = 0.14f;
            float t1 = 0.86f;
            float startX = a.X + (b.X - a.X) * t0;
            float startY = a.Y + (b.Y - a.Y) * t0;
            float endX = a.X + (b.X - a.X) * t1;
            float endY = a.Y + (b.Y - a.Y) * t1;

            if (Math.Abs(a.Y - b.Y) < 0.001f)
            {
                startY += pulse * 0.03f;
                endY += pulse * 0.03f;
            }

            canvas.DrawLine(startX, startY, endX, endY);
        }
    }

    private void DrawOrbitEquation(ICanvas canvas, RectF rect)
    {
        Color stroke = WithAlpha(GetForegroundColor(), 0.18f);
        canvas.StrokeColor = stroke;
        canvas.StrokeSize = 1.7f;

        float startX = rect.Left + rect.Width * 0.06f;
        float startY = rect.Top + rect.Height * 0.72f;
        float midX = rect.Left + rect.Width * 0.22f;
        float endX = rect.Left + rect.Width * 0.42f;
        float stepY = startY + MathF.Sin((float)(TimeSeconds * 0.36)) * 4f;
        float lowerY = startY + rect.Height * 0.07f;

        canvas.DrawLine(startX, startY, midX, startY);
        DrawArcCorner(canvas, midX, startY, 12f, true);
        canvas.DrawLine(midX, stepY + 12f, midX, lowerY - 12f);
        DrawArcCorner(canvas, midX, lowerY, 12f, false);
        canvas.DrawLine(midX + 12f, lowerY, endX, lowerY);
    }

    private void DrawOpenBook(ICanvas canvas, RectF rect)
    {
        Color stroke = WithAlpha(GetForegroundColor(), 0.18f);
        Color secondary = WithAlpha(GetForegroundColor(), 0.12f);
        canvas.StrokeColor = stroke;
        canvas.StrokeSize = 1.8f;

        float centerX = rect.Left + rect.Width * 0.79f;
        float centerY = rect.Top + rect.Height * 0.82f;
        float bookWidth = MathF.Min(170f, rect.Width * 0.16f);
        float bookHeight = MathF.Min(68f, rect.Height * 0.10f);
        float halfW = bookWidth / 2f;
        float halfH = bookHeight / 2f;
        float curve = MathF.Sin((float)(TimeSeconds * 0.30)) * 2.5f;

        // Spine
        canvas.DrawLine(centerX, centerY - halfH, centerX, centerY + halfH);

        // Left page
        canvas.DrawLine(centerX, centerY - halfH, centerX - halfW, centerY - halfH * 0.72f - curve);
        canvas.DrawLine(centerX - halfW, centerY - halfH * 0.72f - curve, centerX - halfW, centerY + halfH * 0.82f);
        canvas.DrawLine(centerX - halfW, centerY + halfH * 0.82f, centerX, centerY + halfH * 0.36f);

        // Right page
        canvas.DrawLine(centerX, centerY - halfH, centerX + halfW, centerY - halfH * 0.72f + curve);
        canvas.DrawLine(centerX + halfW, centerY - halfH * 0.72f + curve, centerX + halfW, centerY + halfH * 0.82f);
        canvas.DrawLine(centerX + halfW, centerY + halfH * 0.82f, centerX, centerY + halfH * 0.36f);

        // Page lines
        canvas.StrokeColor = secondary;
        canvas.StrokeSize = 1.2f;
        canvas.DrawLine(centerX - halfW * 0.72f, centerY - halfH * 0.20f, centerX - halfW * 0.18f, centerY - halfH * 0.08f);
        canvas.DrawLine(centerX + halfW * 0.18f, centerY - halfH * 0.08f, centerX + halfW * 0.72f, centerY - halfH * 0.20f);
        canvas.DrawLine(centerX - halfW * 0.72f, centerY + halfH * 0.12f, centerX - halfW * 0.18f, centerY + halfH * 0.24f);
        canvas.DrawLine(centerX + halfW * 0.18f, centerY + halfH * 0.24f, centerX + halfW * 0.72f, centerY + halfH * 0.12f);
    }

    private void DrawSymbols(ICanvas canvas, RectF rect)
    {
        Color foreground = WithAlpha(GetForegroundColor(), 0.22f);
        canvas.FontColor = foreground;

        foreach (SymbolSpec symbol in Symbols)
        {
            float driftX = MathF.Sin((float)(TimeSeconds * 0.24 * symbol.Speed + symbol.Phase)) * 6f;
            float driftY = MathF.Cos((float)(TimeSeconds * 0.20 * symbol.Speed + symbol.Phase)) * 4f;

            float x = rect.Left + rect.Width * symbol.X + driftX;
            float y = rect.Top + rect.Height * symbol.Y + driftY;

            canvas.FontSize = symbol.FontSize;
            canvas.DrawString(
                symbol.Text,
                x - 60f,
                y - 16f,
                120f,
                32f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private Color GetForegroundColor()
    {
        Color baseColor = ThemeResource.GetColor("TextPrimaryColor", "#F8FAFC");
        if (GetPerceivedBrightness(baseColor) > 0.70f)
        {
            return baseColor;
        }

        Color accent = ThemeResource.GetColor("PrimaryColor", "#2563EB");
        return Mix(baseColor, accent, 0.18f);
    }

    private static float GetPerceivedBrightness(Color color) =>
        (color.Red * 0.299f) +
        (color.Green * 0.587f) +
        (color.Blue * 0.114f);

    private static Color Mix(Color a, Color b, float ratio)
    {
        float clamped = Math.Clamp(ratio, 0f, 1f);
        float inverse = 1f - clamped;
        return new Color(
            a.Red * inverse + b.Red * clamped,
            a.Green * inverse + b.Green * clamped,
            a.Blue * inverse + b.Blue * clamped,
            a.Alpha * inverse + b.Alpha * clamped);
    }

    private static void DrawArcCorner(
        ICanvas canvas,
        float x,
        float y,
        float radius,
        bool stepDown)
    {
        if (stepDown)
        {
            canvas.DrawArc(
                x - radius,
                y,
                radius * 2f,
                radius * 2f,
                270f,
                90f,
                false,
                false);
        }
        else
        {
            canvas.DrawArc(
                x,
                y - radius * 2f,
                radius * 2f,
                radius * 2f,
                180f,
                90f,
                false,
                false);
        }
    }

    private static Color WithAlpha(Color color, float alpha) =>
        new(
            color.Red,
            color.Green,
            color.Blue,
            Math.Clamp(alpha, 0f, 1f));

    private readonly record struct NodeSpec(
        float X,
        float Y,
        float Speed,
        float Phase,
        float Radius);

    private readonly record struct SymbolSpec(
        string Text,
        float X,
        float Y,
        float Speed,
        float Phase,
        float FontSize);
}
