namespace MathSolver.Controls;

/// <summary>A regular button with a decorative emoji and a separately localized caption.</summary>
public sealed class EmojiActionButton : Button
{
    public static readonly BindableProperty VectorIconKeyProperty = BindableProperty.Create(
        nameof(VectorIconKey), typeof(string), typeof(EmojiActionButton), null,
        propertyChanged: static (button, _, _) => ((EmojiActionButton)button).UpdateVectorIcon());
    public string? VectorIconKey { get => (string?)GetValue(VectorIconKeyProperty); set => SetValue(VectorIconKeyProperty, value); }

    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
        nameof(Caption), typeof(string), typeof(EmojiActionButton), string.Empty,
        propertyChanged: static (button, _, _) => ((EmojiActionButton)button).UpdateCaption());

    public static readonly BindableProperty EmojiProperty = BindableProperty.Create(
        nameof(Emoji), typeof(string), typeof(EmojiActionButton), string.Empty,
        propertyChanged: static (button, _, _) => ((EmojiActionButton)button).UpdateCaption());

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Emoji
    {
        get => (string)GetValue(EmojiProperty);
        set => SetValue(EmojiProperty, value);
    }

    private void UpdateCaption()
    {
        string caption = Caption ?? string.Empty;
        string emoji = Emoji ?? string.Empty;
        // Some existing translations already include this decorative icon.
        if (emoji.Length > 0 && caption.StartsWith(emoji, StringComparison.Ordinal))
            caption = caption[emoji.Length..].TrimStart();

        if (MathSolver.Services.ActionIconCatalog.GlyphFor(VectorIconKey) is not null)
        {
            emoji = string.Empty;
            caption = MathSolver.Services.ActionIconCatalog.LabelFor(VectorIconKey, caption);
        }

        Text = emoji.Length == 0 ? caption : caption.Length == 0 ? emoji : $"{emoji}  {caption}";
        // Screen readers announce the action rather than the emoji's Unicode name.
        SemanticProperties.SetDescription(this, caption);
    }

    private void UpdateVectorIcon()
    {
        ActionIcon.SetKey(this, VectorIconKey);
        UpdateCaption();
    }
}
