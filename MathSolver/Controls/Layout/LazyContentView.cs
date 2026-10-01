namespace MathSolver.Controls;

/// <summary>A tab body created on first selection and retained with its input state.</summary>
public sealed class LazyContentView : ContentView
{
    public static readonly BindableProperty ContentTemplateProperty = BindableProperty.Create(
        nameof(ContentTemplate), typeof(DataTemplate), typeof(LazyContentView));

    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public View EnsureContent()
    {
        if (Content is null)
        {
            Content = ContentTemplate?.CreateContent() as View ??
                throw new InvalidOperationException("A lazy tab requires a View template.");
        }
        return Content;
    }
}
