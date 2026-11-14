using MathSolver.Graphics;
using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

/// <summary>Small mathematical diagrams, drawn at any DPI using stable catalogue IDs.</summary>
public sealed class QuizChoiceIllustration : GraphicsView
{
    public static readonly BindableProperty IllustrationIdProperty = BindableProperty.Create(
        nameof(IllustrationId), typeof(string), typeof(QuizChoiceIllustration), "mixed", propertyChanged: Refresh);
    public static readonly BindableProperty InkColorProperty = BindableProperty.Create(
        nameof(InkColor), typeof(Color), typeof(QuizChoiceIllustration), Colors.Green, propertyChanged: Refresh);
    public static readonly BindableProperty IsThemeGroupProperty = BindableProperty.Create(
        nameof(IsThemeGroup), typeof(bool), typeof(QuizChoiceIllustration), false, propertyChanged: Refresh);
    public static readonly BindableProperty InkSurfaceColorProperty = BindableProperty.Create(
        nameof(InkSurfaceColor), typeof(Color), typeof(QuizChoiceIllustration), Colors.White, propertyChanged: Refresh);
    public bool IsThemeGroup { get => (bool)GetValue(IsThemeGroupProperty); set => SetValue(IsThemeGroupProperty, value); }
    public Color InkSurfaceColor { get => (Color)GetValue(InkSurfaceColorProperty); set => SetValue(InkSurfaceColorProperty, value); }
    public string IllustrationId { get => (string)GetValue(IllustrationIdProperty); set => SetValue(IllustrationIdProperty, value); }
    public Color InkColor { get => (Color)GetValue(InkColorProperty); set => SetValue(InkColorProperty, value); }

    public QuizChoiceIllustration()
    {
        InputTransparent = true;
        AutomationProperties.SetExcludedWithChildren(this, true);
        SetDynamicResource(InkColorProperty, "PrimaryColor");
        Drawable = new QuizChoiceDrawable(IllustrationId, InkColor);
    }

    private static void Refresh(BindableObject target, object oldValue, object newValue)
    {
        var view = (QuizChoiceIllustration)target;
        view.Drawable = new QuizChoiceDrawable(view.IllustrationId,
            view.IsThemeGroup ? ReadableInk(view.InkColor, view.InkSurfaceColor) : view.InkColor, view.IsThemeGroup);
        view.Invalidate();
    }

    internal static Color ReadableInk(Color? candidate, Color? background)
    {
        // A recycled template can resolve its surface binding after its group flag.
        // Use the current local card surface until the real binding arrives.
        candidate ??= ThemeResource.GetColor("PrimaryColor", "#2563EB");
        background ??= ThemeResource.GetColor("SurfaceAltColor", "#FFFFFF");
        var rgb = QuizThemeIconPolicy.InkOn(new(candidate.Red, candidate.Green, candidate.Blue),
            new(background.Red, background.Green, background.Blue));
        return new Color((float)rgb.Red, (float)rgb.Green, (float)rgb.Blue, 1);
    }

}
