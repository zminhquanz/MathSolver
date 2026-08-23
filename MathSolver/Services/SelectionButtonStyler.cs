namespace MathSolver.Services;

/// <summary>
/// Applies the shared selected/unselected appearance used by solver tabs.
/// </summary>
internal static class SelectionButtonStyler
{
    public static void Apply(Button button, bool isSelected)
    {
        ArgumentNullException.ThrowIfNull(button);

        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            isSelected ? "WallpaperSelectionBackgroundColor" : "WallpaperSurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected ? "WallpaperSelectionTextColor" : "WallpaperTextPrimaryColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            isSelected ? "WallpaperSelectionBorderColor" : "WallpaperBorderColor");

        button.BorderWidth = 1d;
    }

    public static void Select(Button selectedButton, params Button[] buttons)
    {
        ArgumentNullException.ThrowIfNull(selectedButton);
        ArgumentNullException.ThrowIfNull(buttons);

        foreach (Button button in buttons)
        {
            Apply(button, ReferenceEquals(button, selectedButton));
        }
    }
}
