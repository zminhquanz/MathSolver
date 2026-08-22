using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

/// <summary>
/// Low-cost ambient math animation used when the user wants an animated
/// background without an external MP4. It intentionally avoids bitmaps,
/// shaders, blur passes and allocations inside Draw().
/// </summary>
public sealed class MathAnimatedBackgroundDrawable : IDrawable
{
    private static readonly MathGlyph[] Glyphs =
    [
        new("πr²", 0.08f, 0.18f, 0.008f, 0.2f, 25f),
        new("√x", 0.28f, 0.68f, 0.010f, 1.1f, 29f),
        new("∑", 0.47f, 0.25f, 0.006f, 2.3f, 34f),
        new("x² + y²", 0.67f, 0.73f, 0.008f, 3.2f, 22f),
        new("Δ = b² − 4ac", 0.79f, 0.16f, 0.005f, 4.0f, 20f),
        new("v = s / t", 0.14f, 0.86f, 0.007f, 4.8f, 21f),
        new("a² + b² = c²", 0.55f, 0.90f, 0.006f, 5.7f, 20f),
        new("1/2 + 1/3 = 5/6", 0.82f, 0.48f, 0.005f, 0.8f, 18f)
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
            DrawMovingGrid(canvas, dirtyRect);
            DrawGeometry(canvas, dirtyRect);
            DrawGlyphs(canvas, dirtyRect);
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawMovingGrid(ICanvas canvas, RectF rect)
    {
        const float spacing = 72f;
        float offsetX = (float)((TimeSeconds * 5.5) % spacing);
        float offsetY = (float)((TimeSeconds * 3.0) % spacing);

        canvas.StrokeColor = WithAlpha(
            ThemeResource.GetColor("PrimaryColor", "#2563EB"),
            0.10f);
        canvas.StrokeSize = 1f;

        for (float x = rect.Left - spacing + offsetX;
             x <= rect.Right + spacing;
             x += spacing)
        {
            canvas.DrawLine(x, rect.Top, x, rect.Bottom);
        }

        for (float y = rect.Top - spacing + offsetY;
             y <= rect.Bottom + spacing;
             y += spacing)
        {
            canvas.DrawLine(rect.Left, y, rect.Right, y);
        }
    }

    private void DrawGlyphs(ICanvas canvas, RectF rect)
    {
        Color primary =
            ThemeResource.GetColor("PrimaryColor", "#2563EB");
        Color secondary =
            ThemeResource.GetColor("TextSecondaryColor", "#64748B");

        for (int index = 0; index < Glyphs.Length; index++)
        {
            MathGlyph glyph = Glyphs[index];
            double t = TimeSeconds;

            float x =
                rect.Left +
                rect.Width * glyph.X +
                MathF.Sin((float)(t * 0.35 + glyph.Phase)) *
                MathF.Min(22f, rect.Width * 0.018f);

            float yCycle =
                (glyph.Y -
                 (float)(t * glyph.Speed)) % 1.12f;

            if (yCycle < -0.08f)
            {
                yCycle += 1.12f;
            }

            float y =
                rect.Top + rect.Height * yCycle;

            float pulse =
                0.72f +
                0.28f * MathF.Sin(
                    (float)(t * 0.55 + glyph.Phase));

            canvas.FontSize = glyph.FontSize;
            canvas.FontColor = WithAlpha(
                index % 3 == 0
                    ? primary
                    : secondary,
                0.16f + pulse * 0.07f);

            canvas.DrawString(
                glyph.Text,
                x - 90f,
                y - 20f,
                180f,
                44f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private void DrawGeometry(ICanvas canvas, RectF rect)
    {
        Color primary =
            WithAlpha(
                ThemeResource.GetColor("PrimaryColor", "#2563EB"),
                0.18f);
        Color secondary =
            WithAlpha(
                ThemeResource.GetColor("TextSecondaryColor", "#64748B"),
                0.13f);

        float minSide = MathF.Min(rect.Width, rect.Height);
        float drift = MathF.Sin((float)(TimeSeconds * 0.32)) * 14f;

        canvas.StrokeSize = 2f;
        canvas.StrokeColor = primary;

        float circleRadius = MathF.Max(36f, minSide * 0.075f);
        canvas.DrawCircle(
            rect.Left + rect.Width * 0.20f + drift,
            rect.Top + rect.Height * 0.42f,
            circleRadius);

        float triangleSize = MathF.Max(70f, minSide * 0.14f);
        float tx = rect.Left + rect.Width * 0.73f - drift;
        float ty = rect.Top + rect.Height * 0.69f;
        float triangleTopY = ty - triangleSize * 0.55f;
        float triangleBottomY = ty + triangleSize * 0.45f;
        float triangleLeftX = tx - triangleSize * 0.58f;
        float triangleRightX = tx + triangleSize * 0.58f;
        canvas.DrawLine(tx, triangleTopY, triangleLeftX, triangleBottomY);
        canvas.DrawLine(triangleLeftX, triangleBottomY, triangleRightX, triangleBottomY);
        canvas.DrawLine(triangleRightX, triangleBottomY, tx, triangleTopY);

        canvas.StrokeColor = secondary;
        float plotLeft = rect.Left + rect.Width * 0.38f;
        float plotTop = rect.Top + rect.Height * 0.49f;
        float plotWidth = MathF.Min(250f, rect.Width * 0.20f);
        float plotHeight = MathF.Min(130f, rect.Height * 0.16f);

        canvas.DrawLine(
            plotLeft,
            plotTop + plotHeight / 2f,
            plotLeft + plotWidth,
            plotTop + plotHeight / 2f);
        canvas.DrawLine(
            plotLeft + plotWidth / 2f,
            plotTop,
            plotLeft + plotWidth / 2f,
            plotTop + plotHeight);

        const int segments = 28;
        float previousX = 0f;
        float previousY = 0f;
        for (int i = 0; i <= segments; i++)
        {
            float normalized =
                i / (float)segments * 2f - 1f;
            float x =
                plotLeft +
                (normalized + 1f) * plotWidth / 2f;
            float y =
                plotTop +
                plotHeight * 0.82f -
                normalized * normalized * plotHeight * 0.62f;

            y += MathF.Sin(
                (float)(TimeSeconds * 0.45)) * 4f;

            if (i > 0)
            {
                canvas.DrawLine(previousX, previousY, x, y);
            }

            previousX = x;
            previousY = y;
        }
    }

    private static Color WithAlpha(Color color, float alpha) =>
        new(
            color.Red,
            color.Green,
            color.Blue,
            Math.Clamp(alpha, 0f, 1f));

    private readonly record struct MathGlyph(
        string Text,
        float X,
        float Y,
        float Speed,
        float Phase,
        float FontSize);
}
