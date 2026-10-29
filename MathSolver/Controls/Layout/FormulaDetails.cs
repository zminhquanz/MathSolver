using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Optional reference content with an accessible, density-aware action.</summary>
[ContentProperty(nameof(Details))]
public sealed class FormulaDetails : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(FormulaDetails), "", propertyChanged: Refresh);
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded), typeof(bool), typeof(FormulaDetails), false, propertyChanged: Refresh);
    public static readonly BindableProperty DetailsProperty = BindableProperty.Create(
        nameof(Details), typeof(View), typeof(FormulaDetails), null,
        propertyChanged: (owner, _, value) => ((FormulaDetails)owner)._body.Content = (View?)value);

    private readonly Button _toggle;
    private readonly ContentView _body = new() { IsVisible = false };

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public View? Details { get => (View?)GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }

    public FormulaDetails()
    {
        _toggle = new Button
        {
            Style = (Style)Application.Current!.Resources["SolverOutlinedActionStyle"],
            LineBreakMode = LineBreakMode.WordWrap, AutomationId = "FormulaDetailsToggle"
        };
        LocalizationService.ExcludeSubtreeFromLegacyTracking(_toggle);
        DisplayDensityManager.BindControl(_toggle);
        _toggle.Clicked += (_, _) => IsExpanded = !IsExpanded;
        var layout = new VerticalStackLayout { Spacing = 8 };
        layout.Add(_toggle); layout.Add(_body); Content = layout;
        Loaded += (_, _) =>
        {
            LocalizationService.CultureChanged -= OnCultureChanged;
            LocalizationService.CultureChanged += OnCultureChanged;
            UpdatePresentation();
        };
        Unloaded += (_, _) => LocalizationService.CultureChanged -= OnCultureChanged;
        UpdatePresentation();
    }

    private static void Refresh(BindableObject owner, object oldValue, object newValue) =>
        ((FormulaDetails)owner).UpdatePresentation();

    private void UpdatePresentation()
    {
        if (_toggle is null) return;
        _toggle.Text = Title + (IsExpanded ? " ▴" : " ▾");
        SemanticProperties.SetDescription(_toggle, Title + ". " + LocalizationService.TranslateKey(
            IsExpanded ? "Formula.Workspace.Expanded" : "Formula.Workspace.Collapsed"));
        SemanticProperties.SetHint(_toggle, LocalizationService.TranslateKey(
            IsExpanded ? "Formula.Workspace.Collapse" : "Formula.Workspace.Expand"));
        _body.IsVisible = IsExpanded;
    }

    private void OnCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    {
        if (IsLoaded && Handler?.MauiContext is not null && !WindowUiLifetime.IsClosing(Window))
            UpdatePresentation();
    });
}
