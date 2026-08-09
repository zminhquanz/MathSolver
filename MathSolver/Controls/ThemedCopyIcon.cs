using CommunityToolkit.Maui.Behaviors;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>
/// Icon sao chép dùng chung cho các nút kết quả. Màu icon luôn tương phản
/// với nền nút: đen ở chủ đề sáng và trắng ở chủ đề tối.
/// </summary>
public sealed class ThemedCopyIcon : Image
{
    private readonly IconTintColorBehavior _tintBehavior = new();
    private bool _isThemeSubscribed;

    public ThemedCopyIcon()
    {
        Source =
            "content_copy.png";

        Aspect =
            Microsoft.Maui.Aspect.AspectFit;

        InputTransparent =
            true;

        Behaviors.Add(
            _tintBehavior);

        Loaded +=
            OnIconLoaded;

        Unloaded +=
            OnIconUnloaded;
    }

    private void OnIconLoaded(
        object? sender,
        EventArgs e)
    {
        if (!_isThemeSubscribed)
        {
            AppThemeManager.ThemeChanged +=
                OnThemeChanged;

            _isThemeSubscribed =
                true;
        }

        UpdateTintColor();
    }

    private void OnIconUnloaded(
        object? sender,
        EventArgs e)
    {
        if (!_isThemeSubscribed)
        {
            return;
        }

        AppThemeManager.ThemeChanged -=
            OnThemeChanged;

        _isThemeSubscribed =
            false;
    }

    private void OnThemeChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            UpdateTintColor);
    }

    private void UpdateTintColor()
    {
        AppTheme effectiveTheme =
            AppThemeManager.CurrentMode switch
            {
                AppThemeMode.Light =>
                    AppTheme.Light,

                AppThemeMode.Dark =>
                    AppTheme.Dark,

                _ =>
                    Application.Current?.RequestedTheme == AppTheme.Dark
                        ? AppTheme.Dark
                        : AppTheme.Light
            };

        _tintBehavior.TintColor =
            effectiveTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;
    }
}
