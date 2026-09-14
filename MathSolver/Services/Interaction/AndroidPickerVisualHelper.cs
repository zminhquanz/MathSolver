#if ANDROID
using Android.Views;
using Microsoft.Maui.Controls;

namespace MathSolver.Services;

/// <summary>
/// Small Android-only visual polish for the stock .NET MAUI Picker.
/// It keeps MAUI's Material 3 Picker behavior/dialog intact and only removes
/// the legacy text-field underline/background so the XAML Border can be the
/// single visible field outline.
/// </summary>
public static class AndroidPickerVisualHelper
{
    public static void Attach(Picker picker)
    {
        picker.HandlerChanged -= OnPickerHandlerChanged;
        picker.HandlerChanged += OnPickerHandlerChanged;
        Apply(picker);
    }

    private static void OnPickerHandlerChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is Picker picker)
        {
            Apply(picker);
        }
    }

    private static void Apply(Picker picker)
    {
        if (picker.Handler?.PlatformView is not Android.Widget.EditText editText)
        {
            return;
        }

        editText.Background = null;
        editText.SetIncludeFontPadding(false);
        editText.Gravity = GravityFlags.CenterVertical;

        float density =
            editText.Resources?.DisplayMetrics?.Density ?? 1f;

        int leftPadding =
            (int)Math.Round(16d * density);

        // Keep a little extra space for the XAML trailing chevron.
        int rightPadding =
            (int)Math.Round(46d * density);

        editText.SetPadding(
            leftPadding,
            0,
            rightPadding,
            0);
    }
}
#endif
