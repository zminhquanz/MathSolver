using MathSolver.Services;
using Microsoft.Maui.Graphics;
using System.Globalization;

namespace MathSolver.Graphics;

/// <summary>Mathematical illustration, drawn from immutable quiz values, not a decorative bitmap.</summary>
internal sealed class LearningGuideDrawable(LearningGuideScene scene) : IDrawable
{
    public LearningGuideFrame Frame { get; set; } = scene.At(1);
    public void Draw(ICanvas canvas, RectF bounds)
    {
        if (bounds.Width < 48 || bounds.Height < 48) return;
        canvas.SaveState();
        try
        {
            var ink = ThemeResource.GetColor("WallpaperTextPrimaryColor", "#1E293B");
            var secondary = ThemeResource.GetColor("WallpaperTextSecondaryColor", "#64748B");
            var accent = ThemeResource.GetColor("ContentAccentTextColor", "#2563EB");
            canvas.StrokeColor = secondary; canvas.FontColor = ink; canvas.StrokeSize = 1.5f;
            if (scene.Kind == LearningGuideKind.Fraction)
            {
                float w = Math.Min(bounds.Width - 16, 480), cell = w / scene.Second;
                float x = (bounds.Width - w) / 2, y = (bounds.Height - 64) / 2;
                for (int i = 0; i < scene.Second; i++)
                {
                    // Each equal part fades as a whole; never suggest unequal fractional widths.
                    canvas.FillColor = accent.WithAlpha((float)Math.Clamp(Frame.ShadedParts - i, 0, 1) * .45f);
                    canvas.FillRectangle(x + cell * i, y, cell, 64);
                    canvas.DrawRectangle(x + cell * i, y, cell, 64);
                }
                return;
            }
            float cx = bounds.Width / 2, cy = bounds.Height / 2;
            float radius = Math.Min(bounds.Width / 2 - 16, bounds.Height / 2 - 10);
            canvas.FontSize = 16 * (float)Math.Min(ResponsiveLayoutPolicy.TextScale, 1.5);
            canvas.DrawCircle(cx, cy, radius);
            for (int tick = 0; tick < 60; tick++)
            {
                double a = tick * Math.PI / 30 - Math.PI / 2;
                float outer = radius - 4, inner = radius - (tick % 5 == 0 ? 11 : 7);
                canvas.DrawLine(cx + inner * (float)Math.Cos(a), cy + inner * (float)Math.Sin(a),
                    cx + outer * (float)Math.Cos(a), cy + outer * (float)Math.Sin(a));
            }
            for (int number = 1; number <= 12; number++)
            {
                double a = number * Math.PI / 6 - Math.PI / 2;
                float r = radius - 28;
                canvas.DrawString(number.ToString(CultureInfo.CurrentCulture),
                    cx + r * (float)Math.Cos(a) - 18, cy + r * (float)Math.Sin(a) - 16,
                    36, 32, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            void Hand(double degrees, float length, float thickness, Color color)
            {
                double a = degrees * Math.PI / 180 - Math.PI / 2;
                canvas.StrokeColor = color; canvas.StrokeSize = thickness; canvas.StrokeLineCap = LineCap.Round;
                canvas.DrawLine(cx, cy, cx + length * (float)Math.Cos(a), cy + length * (float)Math.Sin(a));
            }
            Hand(Frame.HourDegrees, radius * .44f, 5, ink);
            Hand(Frame.MinuteDegrees, radius * .68f, 3, accent);
            canvas.FillColor = ink; canvas.FillCircle(cx, cy, 4);
        }
        finally { canvas.RestoreState(); }
    }
}
