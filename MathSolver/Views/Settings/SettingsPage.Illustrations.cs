using MathSolver.Services;
namespace MathSolver.Views;

public partial class SettingsPage
{
    private bool _updatingIllustrationMotion;
    private void RefreshIllustrationMotionSwitch()
    {
        _updatingIllustrationMotion = true;
        try { IllustrationMotionSwitch.IsToggled = IllustrationMotionSettings.IsEnabled; }
        finally { _updatingIllustrationMotion = false; }
    }
    private void OnIllustrationMotionToggled(object? sender, ToggledEventArgs e)
    {
        if (!_updatingIllustrationMotion) IllustrationMotionSettings.SetEnabled(e.Value);
    }
}
