using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Graphics;

/// <summary>
/// Low-cost ambient math animation used when the user wants an animated
/// background without an external MP4. It intentionally avoids bitmaps,
/// shaders, blur passes and allocations inside Draw().
/// </summary>
public sealed class MathAnimatedBackgroundDrawable : ITimeDrivenDrawable
{
    private static readonly MathGlyph[] Glyphs =
    [
        new("P = 4a", 0.10f, 0.18f, 0.010f, 0.2f, 22f),
        new("S = a x a", 0.23f, 0.34f, 0.008f, 1.0f, 21f),
        new("S = pi x r x r", 0.80f, 0.22f, 0.009f, 1.8f, 19f),
        new("V = a x a x a", 0.74f, 0.84f, 0.006f, 2.4f, 18f),
        new("x + 7 = 19", 0.17f, 0.80f, 0.007f, 3.0f, 20f),
        new("x = (c - b) / a", 0.42f, 0.70f, 0.008f, 3.7f, 18f),
        new("ax + b = 0", 0.52f, 0.28f, 0.007f, 4.3f, 22f),
        new("ax2 + bx + c = 0", 0.63f, 0.56f, 0.006f, 4.9f, 20f),
        new("Delta = b2 - 4ac", 0.84f, 0.44f, 0.005f, 5.5f, 18f),
        new("x1,2 = (-b +/- sqrt(Delta)) / 2a", 0.48f, 0.94f, 0.004f, 6.1f, 16f)
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
            DrawFlatGeometry(canvas, dirtyRect);
            DrawSpatialGeometry(canvas, dirtyRect);
            DrawEquationGraph(canvas, dirtyRect);
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
            0.08f);
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
        Color primary = ThemeResource.GetColor("PrimaryColor", "#2563EB");
        Color secondary = ThemeResource.GetColor("TextSecondaryColor", "#64748B");

        for (int index = 0; index < Glyphs.Length; index++)
        {
            MathGlyph glyph = Glyphs[index];
            double t = TimeSeconds;

            float x =
                rect.Left +
                rect.Width * glyph.X +
                MathF.Sin((float)(t * 0.42 + glyph.Phase)) *
                MathF.Min(18f, rect.Width * 0.014f);

            float yCycle =
                (glyph.Y - (float)(t * glyph.Speed)) % 1.14f;

            if (yCycle < -0.10f)
            {
                yCycle += 1.14f;
            }

            float y = rect.Top + rect.Height * yCycle;

            float pulse =
                0.68f +
                0.32f * MathF.Sin((float)(t * 0.58 + glyph.Phase));

            canvas.FontSize = glyph.FontSize;
            canvas.FontColor = WithAlpha(
                index % 3 == 0 ? primary : secondary,
                0.13f + pulse * 0.07f);

            canvas.DrawString(
                glyph.Text,
                x - 130f,
                y - 18f,
                260f,
                36f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private void DrawFlatGeometry(ICanvas canvas, RectF rect)
    {
        Color primary = WithAlpha(
            ThemeResource.GetColor("PrimaryColor", "#2563EB"),
            0.20f);
        Color secondary = WithAlpha(
            ThemeResource.GetColor("TextSecondaryColor", "#64748B"),
            0.16f);

        float minSide = MathF.Min(rect.Width, rect.Height);
        float drift = MathF.Sin((float)(TimeSeconds * 0.34)) * 12f;

        canvas.StrokeSize = 2f;
        canvas.StrokeColor = primary;

        float squareSize = MathF.Max(58f, minSide * 0.12f);
        float squareX = rect.Left + rect.Width * 0.18f + drift;
        float squareY = rect.Top + rect.Height * 0.22f;
        canvas.DrawRectangle(
            squareX - squareSize / 2f,
            squareY - squareSize / 2f,
            squareSize,
            squareSize);
        DrawAnnotation(canvas, "a", squareX, squareY - squareSize * 0.62f, secondary, 16f);

        float triSize = MathF.Max(70f, minSide * 0.14f);
        float tx = rect.Left + rect.Width * 0.74f - drift;
        float ty = rect.Top + rect.Height * 0.28f;
        float triangleTopY = ty - triSize * 0.54f;
        float triangleBottomY = ty + triSize * 0.42f;
        float triangleLeftX = tx - triSize * 0.56f;
        float triangleRightX = tx + triSize * 0.56f;
        canvas.DrawLine(tx, triangleTopY, triangleLeftX, triangleBottomY);
        canvas.DrawLine(triangleLeftX, triangleBottomY, triangleRightX, triangleBottomY);
        canvas.DrawLine(triangleRightX, triangleBottomY, tx, triangleTopY);

        canvas.StrokeColor = secondary;
        float radius = MathF.Max(34f, minSide * 0.068f);
        float cx = rect.Left + rect.Width * 0.28f;
        float cy = rect.Top + rect.Height * 0.66f + drift * 0.25f;
        canvas.DrawCircle(cx, cy, radius);
        canvas.DrawLine(cx, cy, cx + radius, cy);
        DrawAnnotation(canvas, "r", cx + radius * 0.56f, cy - 18f, secondary, 16f);

        float rectWidth = MathF.Max(82f, minSide * 0.16f);
        float rectHeight = MathF.Max(52f, minSide * 0.10f);
        float rx = rect.Left + rect.Width * 0.56f;
        float ry = rect.Top + rect.Height * 0.77f;
        canvas.DrawRoundedRectangle(
            rx - rectWidth / 2f,
            ry - rectHeight / 2f,
            rectWidth,
            rectHeight,
            10f);
        DrawAnnotation(canvas, "a", rx, ry - rectHeight * 0.82f, secondary, 16f);
        DrawAnnotation(canvas, "b", rx + rectWidth * 0.62f, ry, secondary, 16f);
    }

    private void DrawSpatialGeometry(ICanvas canvas, RectF rect)
    {
        Color primary = WithAlpha(
            ThemeResource.GetColor("PrimaryColor", "#2563EB"),
            0.18f);
        Color secondary = WithAlpha(
            ThemeResource.GetColor("TextSecondaryColor", "#64748B"),
            0.15f);

        float minSide = MathF.Min(rect.Width, rect.Height);
        float drift = MathF.Sin((float)(TimeSeconds * 0.29 + 1.3)) * 10f;

        canvas.StrokeSize = 2f;
        canvas.StrokeColor = primary;

        float cubeSize = MathF.Max(62f, minSide * 0.11f);
        float cubeX = rect.Left + rect.Width * 0.82f + drift;
        float cubeY = rect.Top + rect.Height * 0.72f;
        DrawCube(canvas, cubeX, cubeY, cubeSize);

        canvas.StrokeColor = secondary;
        float cylinderW = MathF.Max(70f, minSide * 0.12f);
        float cylinderH = MathF.Max(98f, minSide * 0.18f);
        float cylX = rect.Left + rect.Width * 0.46f - drift * 0.6f;
        float cylY = rect.Top + rect.Height * 0.24f;
        DrawCylinder(canvas, cylX, cylY, cylinderW, cylinderH);

        canvas.StrokeColor = primary;
        float pyramidSize = MathF.Max(74f, minSide * 0.13f);
        float px = rect.Left + rect.Width * 0.08f;
        float py = rect.Top + rect.Height * 0.86f;
        DrawPyramid(canvas, px, py, pyramidSize);
    }

    private void DrawEquationGraph(ICanvas canvas, RectF rect)
    {
        Color secondary = WithAlpha(
            ThemeResource.GetColor("TextSecondaryColor", "#64748B"),
            0.16f);
        Color primary = WithAlpha(
            ThemeResource.GetColor("PrimaryColor", "#2563EB"),
            0.20f);

        float plotLeft = rect.Left + rect.Width * 0.42f;
        float plotTop = rect.Top + rect.Height * 0.46f;
        float plotWidth = MathF.Min(260f, rect.Width * 0.24f);
        float plotHeight = MathF.Min(150f, rect.Height * 0.18f);

        canvas.StrokeSize = 1.8f;
        canvas.StrokeColor = secondary;
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

        canvas.StrokeColor = primary;
        float lineOffset = MathF.Sin((float)(TimeSeconds * 0.42)) * 6f;
        canvas.DrawLine(
            plotLeft + plotWidth * 0.10f,
            plotTop + plotHeight * 0.78f - lineOffset,
            plotLeft + plotWidth * 0.88f,
            plotTop + plotHeight * 0.20f - lineOffset);

        canvas.StrokeColor = secondary;
        const int segments = 30;
        float previousX = 0f;
        float previousY = 0f;
        for (int i = 0; i <= segments; i++)
        {
            float normalized = i / (float)segments * 2f - 1f;
            float x = plotLeft + (normalized + 1f) * plotWidth / 2f;
            float y =
                plotTop +
                plotHeight * 0.84f -
                normalized * normalized * plotHeight * 0.62f +
                MathF.Sin((float)(TimeSeconds * 0.48)) * 3.5f;

            if (i > 0)
            {
                canvas.DrawLine(previousX, previousY, x, y);
            }

            previousX = x;
            previousY = y;
        }
    }

    private static void DrawCube(ICanvas canvas, float centerX, float centerY, float size)
    {
        float half = size / 2f;
        float offset = size * 0.28f;

        RectF front = new(centerX - half, centerY - half, size, size);
        RectF back = new(centerX - half + offset, centerY - half - offset, size, size);

        canvas.DrawRectangle(front);
        canvas.DrawRectangle(back);
        canvas.DrawLine(front.Left, front.Top, back.Left, back.Top);
        canvas.DrawLine(front.Right, front.Top, back.Right, back.Top);
        canvas.DrawLine(front.Left, front.Bottom, back.Left, back.Bottom);
        canvas.DrawLine(front.Right, front.Bottom, back.Right, back.Bottom);
    }

    private static void DrawCylinder(ICanvas canvas, float centerX, float centerY, float width, float height)
    {
        float halfW = width / 2f;
        float halfH = height / 2f;
        float ellipseHeight = MathF.Max(16f, width * 0.24f);

        canvas.DrawEllipse(centerX - halfW, centerY - halfH, width, ellipseHeight);
        canvas.DrawLine(centerX - halfW, centerY - halfH + ellipseHeight / 2f, centerX - halfW, centerY + halfH - ellipseHeight / 2f);
        canvas.DrawLine(centerX + halfW, centerY - halfH + ellipseHeight / 2f, centerX + halfW, centerY + halfH - ellipseHeight / 2f);
        canvas.DrawArc(
            centerX - halfW,
            centerY + halfH - ellipseHeight,
            width,
            ellipseHeight,
            0f,
            180f,
            false,
            false);
    }

    private static void DrawPyramid(ICanvas canvas, float centerX, float centerY, float size)
    {
        float half = size / 2f;
        float offset = size * 0.22f;

        PointF apex = new(centerX, centerY - size * 0.70f);
        PointF left = new(centerX - half, centerY - half * 0.08f);
        PointF right = new(centerX + half, centerY - half * 0.08f);
        PointF back = new(centerX + offset, centerY - size * 0.34f);
        PointF baseLeft = new(centerX - half + offset, centerY + half * 0.36f);
        PointF baseRight = new(centerX + half + offset, centerY + half * 0.36f);

        canvas.DrawLine(left.X, left.Y, right.X, right.Y);
        canvas.DrawLine(left.X, left.Y, baseLeft.X, baseLeft.Y);
        canvas.DrawLine(baseLeft.X, baseLeft.Y, baseRight.X, baseRight.Y);
        canvas.DrawLine(right.X, right.Y, baseRight.X, baseRight.Y);
        canvas.DrawLine(apex.X, apex.Y, left.X, left.Y);
        canvas.DrawLine(apex.X, apex.Y, right.X, right.Y);
        canvas.DrawLine(apex.X, apex.Y, back.X, back.Y);
        canvas.DrawLine(back.X, back.Y, baseLeft.X, baseLeft.Y);
        canvas.DrawLine(back.X, back.Y, baseRight.X, baseRight.Y);
    }

    private static void DrawAnnotation(
        ICanvas canvas,
        string text,
        float centerX,
        float centerY,
        Color color,
        float fontSize)
    {
        canvas.FontSize = fontSize;
        canvas.FontColor = color;
        canvas.DrawString(
            text,
            centerX - 24f,
            centerY - 12f,
            48f,
            24f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
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
