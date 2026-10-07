using MathSolver.Graphics;
using Microsoft.Maui.Graphics;

namespace MathSolver.Controls;

/// <summary>Small mathematical diagrams, drawn at any DPI using stable catalogue IDs.</summary>
public sealed class QuizChoiceIllustration : GraphicsView
{
    public static readonly BindableProperty IllustrationIdProperty = BindableProperty.Create(
        nameof(IllustrationId), typeof(string), typeof(QuizChoiceIllustration), "mixed", propertyChanged: Refresh);
    public static readonly BindableProperty InkColorProperty = BindableProperty.Create(
        nameof(InkColor), typeof(Color), typeof(QuizChoiceIllustration), Colors.Green, propertyChanged: Refresh);
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
        view.Drawable = new QuizChoiceDrawable(view.IllustrationId, view.InkColor);
        view.Invalidate();
    }

}
