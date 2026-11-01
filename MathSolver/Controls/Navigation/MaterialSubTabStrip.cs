using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Android's native, scrollable Material tabs; desktop uses its existing strip.</summary>
public sealed class MaterialSubTabStrip : ContentView
{
    public static readonly BindableProperty TabKeysProperty = BindableProperty.Create(
        nameof(TabKeys), typeof(string[]), typeof(MaterialSubTabStrip), Array.Empty<string>());
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(MaterialSubTabStrip), 0);
    public static readonly BindableProperty AccentColorProperty = BindableProperty.Create(
        nameof(AccentColor), typeof(Color), typeof(MaterialSubTabStrip), Colors.Purple);
    public static readonly BindableProperty LabelColorProperty = BindableProperty.Create(
        nameof(LabelColor), typeof(Color), typeof(MaterialSubTabStrip), Colors.DimGray);
    public static readonly BindableProperty SelectedLabelColorProperty = BindableProperty.Create(
        nameof(SelectedLabelColor), typeof(Color), typeof(MaterialSubTabStrip), Colors.Black);
    public static readonly BindableProperty SurfaceColorProperty = BindableProperty.Create(
        nameof(SurfaceColor), typeof(Color), typeof(MaterialSubTabStrip), Colors.White);

    public string[] TabKeys
    {
        get => (string[])GetValue(TabKeysProperty);
        set => SetValue(TabKeysProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public Color LabelColor
    {
        get => (Color)GetValue(LabelColorProperty);
        set => SetValue(LabelColorProperty, value);
    }

    public Color SelectedLabelColor
    {
        get => (Color)GetValue(SelectedLabelColorProperty);
        set => SetValue(SelectedLabelColorProperty, value);
    }

    public Color SurfaceColor
    {
        get => (Color)GetValue(SurfaceColorProperty);
        set => SetValue(SurfaceColorProperty, value);
    }

    public event EventHandler<int>? SelectionRequested;

    public MaterialSubTabStrip()
    {
        SetDynamicResource(AccentColorProperty, "PrimaryColor");
        SetDynamicResource(LabelColorProperty, "TextSecondaryColor");
        SetDynamicResource(SelectedLabelColorProperty, "TextPrimaryColor");
        SetDynamicResource(SurfaceColorProperty, "SurfaceColor");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        LocalizationService.CultureChanged -= OnCultureChanged;
        LocalizationService.CultureChanged += OnCultureChanged;
        RefreshLayout();
        // Language may have changed on Settings while this page was unloaded.
        Handler?.UpdateValue(nameof(TabKeys));
    }

    private void OnUnloaded(object? sender, EventArgs e) =>
        LocalizationService.CultureChanged -= OnCultureChanged;

    private void OnCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    {
        if (IsLoaded) Handler?.UpdateValue(nameof(TabKeys));
    });

    public void RefreshLayout() => HeightRequest = Math.Max(
        DisplayDensityManager.Metrics.ControlHeight, 48 * ResponsiveLayoutPolicy.TextScale);

    public void RevealSelected() => Handler?.Invoke(nameof(RevealSelected));

    // Reapply even when unchanged: a navigation request can be refused while
    // a calculation or another page transition is in progress.
    public void SetSelection(int index)
    {
        SelectedIndex = index;
        Handler?.UpdateValue(nameof(SelectedIndex));
    }

    internal void RequestSelection(int index) => SelectionRequested?.Invoke(this, index);
}
