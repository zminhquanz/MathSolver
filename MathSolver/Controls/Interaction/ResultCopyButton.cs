using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>
/// Keeps the native button text empty so its disabled visual state cannot draw
/// over the icon and label that form the visible copy action.
/// </summary>
public sealed class ResultCopyButton : Button
{
    public static readonly BindableProperty NormalTextKeyProperty =
        BindableProperty.Create(
            nameof(NormalTextKey),
            typeof(string),
            typeof(ResultCopyButton),
            "PowerRoot.CopyResult",
            propertyChanged: OnDisplayStateChanged);

    public static readonly BindableProperty IsCopiedProperty =
        BindableProperty.Create(
            nameof(IsCopied),
            typeof(bool),
            typeof(ResultCopyButton),
            false,
            propertyChanged: OnDisplayStateChanged);

    public string NormalTextKey
    {
        get => (string)GetValue(NormalTextKeyProperty);
        set => SetValue(NormalTextKeyProperty, value);
    }

    public bool IsCopied
    {
        get => (bool)GetValue(IsCopiedProperty);
        set => SetValue(IsCopiedProperty, value);
    }

    public string DisplayText =>
        LocalizationService.TranslateKey(
            IsCopied ? "PowerRoot.Copied" : NormalTextKey);

    public ResultCopyButton()
    {
        Text = string.Empty;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnDisplayStateChanged(
        BindableObject bindable,
        object oldValue,
        object newValue) =>
        ((ResultCopyButton)bindable).RefreshDisplayText();

    private void OnLoaded(object? sender, EventArgs e)
    {
        LocalizationService.CultureChanged -= OnCultureChanged;
        LocalizationService.CultureChanged += OnCultureChanged;
        RefreshDisplayText();
    }

    private void OnUnloaded(object? sender, EventArgs e) =>
        LocalizationService.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(RefreshDisplayText);

    private void RefreshDisplayText()
    {
        OnPropertyChanged(nameof(DisplayText));
        SemanticProperties.SetDescription(this, DisplayText);
    }
}
