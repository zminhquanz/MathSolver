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
            isSelected ? "PrimaryColor" : "WallpaperSurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected ? "OnPrimaryColor" : "WallpaperTextPrimaryColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            isSelected ? "PrimaryColor" : "WallpaperBorderColor");

        button.BorderWidth = isSelected ? 0d : 1d;
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
