using MathSolver.Controls;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private int _configurationThemeGeneration;

    private void OnConfigurationThemeChanged(object? sender, EventArgs e)
        => QueueConfigurationThemeRefresh();

    private void QueueConfigurationThemeRefresh()
    {
        int generation = ++_configurationThemeGeneration;
        void Refresh()
        {
            if (_appeared && generation == _configurationThemeGeneration)
                RefreshConfigurationTheme();
        }
        Dispatcher.Dispatch(Refresh);
#if WINDOWS
        // Native peers can still be reconnecting on the first dispatcher turn.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(180), Refresh);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(420), Refresh);
#endif
    }

    private void RefreshConfigurationTheme()
    {
        var resources = Application.Current?.Resources;
        if (ConfigurationPanel is null || resources is null) return;
        if (resources.TryGetValue("WallpaperTextPrimaryColor", out var foreground) != true
            || foreground is not Color primary
            || resources.TryGetValue("TextSecondaryColor", out var muted) != true
            || muted is not Color secondary) return;

        void Apply(Action update)
        {
            try { update(); }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ObjectDisposedException)
            { System.Diagnostics.Debug.WriteLine($"AI configuration theme text deferred: {error}"); }
        }

        void RefreshLabels(Microsoft.Maui.IVisualTreeElement element)
        {
            // Illustrated fields own their opaque input foreground. Only the
            // configuration's neutral captions use the wallpaper contrast color;
            // action/status colors elsewhere on the page retain their bindings.
            if (element is IllustratedQuizPicker) return;
            if (element is Label label)
                Apply(() => label.TextColor = label.IsEnabled ? primary : secondary);
            foreach (var child in element.GetVisualChildren()) RefreshLabels(child);
        }

        RefreshLabels(ConfigurationPanel);
        Apply(() => AiBankHeaderSubtitleLabel.TextColor = secondary);
        if (resources.TryGetValue("TextPrimaryColor", out var inputForeground) == true
            && inputForeground is Color inputPrimary)
        {
            Apply(() => AiBankHeaderTitleLabel.TextColor = inputPrimary);
            Apply(() => CountEntry.TextColor = CountEntry.IsEnabled ? inputPrimary : secondary);
        }
        Apply(() => CountEntry.PlaceholderColor = secondary);
    }
}
