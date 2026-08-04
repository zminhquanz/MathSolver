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
            isSelected ? "PrimaryColor" : "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected ? "OnPrimaryColor" : "TextPrimaryColor");
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
