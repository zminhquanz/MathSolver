using MathSolver.Services;

namespace MathSolver.Views;

public partial class SettingsPage
{
    private bool _densityLayoutQueued;

    private void LoadDensitySettings()
    {
        if (_isClosing || WindowUiLifetime.IsClosing(Window)) return;
        foreach (var (button, mode) in new[]
        {
            (AutomaticDensityButton, DisplayDensityMode.Automatic),
            (CompactDensityButton, DisplayDensityMode.Compact),
            (TouchDensityButton, DisplayDensityMode.Touch)
        })
        {
            bool selected = DisplayDensityManager.RequestedMode == mode;
            string caption = LocalizationService.TranslateKey("Settings.Density." + mode);
            button.Text = selected ? "✓  " + caption : caption;
            UpdateThemeModeButton(button, selected);
            button.BorderWidth = selected ? 2 : 1;
            SemanticProperties.SetDescription(button, selected
                ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    LocalizationService.TranslateKey("Settings.Density.Selected"), caption) : caption);
        }
        string effective = LocalizationService.TranslateKey("Settings.Density." + DisplayDensityManager.EffectiveMode);
        DensityEffectiveLabel.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey("Settings.Density.Effective"), effective);
        OnDensityChoicesSizeChanged(this, EventArgs.Empty);
    }

    private void OnDensityChoiceClicked(object? sender, EventArgs e)
    {
        if (_isClosing || sender is not Button { CommandParameter: DisplayDensityMode mode }) return;
        if (mode == DisplayDensityManager.RequestedMode) return;
        DisplayDensityManager.SetMode(mode);
        LoadDensitySettings();
    }

    private void OnDensityChoicesSizeChanged(object? sender, EventArgs e)
    {
        if (_densityLayoutQueued || _isClosing || !IsLoaded || DensityChoices.Width <= 0) return;
        _densityLayoutQueued = true;
        Dispatcher.Dispatch(() =>
        {
            _densityLayoutQueued = false;
            if (_isClosing || !IsLoaded || DensityChoices.Handler?.MauiContext is null
                || Window is not { } window || WindowUiLifetime.IsClosing(window)) return;
            int columns = ResponsiveLayoutPolicy.Columns(DensityChoices.Width, 130, 3, DensityChoices.ColumnSpacing) == 3 ? 3 : 1;
            int rows = columns == 3 ? 1 : 3;
            if (DensityChoices.ColumnDefinitions.Count == columns && DensityChoices.RowDefinitions.Count == rows) return;
            DensityChoices.ColumnDefinitions.Clear();
            DensityChoices.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) DensityChoices.ColumnDefinitions.Add(new(GridLength.Star));
            for (int i = 0; i < rows; i++) DensityChoices.RowDefinitions.Add(new(GridLength.Auto));
            var buttons = new[] { AutomaticDensityButton, CompactDensityButton, TouchDensityButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                Grid.SetColumn(buttons[i], columns == 3 ? i : 0);
                Grid.SetRow(buttons[i], columns == 3 ? 0 : i);
            }
        });
    }

    private void OnDensityChanged(object? sender, EventArgs e)
    {
        if (!_isClosing && IsLoaded && Handler?.MauiContext is not null
            && Window is { } window && !WindowUiLifetime.IsClosing(window)) LoadDensitySettings();
    }
}
