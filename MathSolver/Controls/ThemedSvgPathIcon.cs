using MathSolver.Services;
using MauiGeometry = Microsoft.Maui.Controls.Shapes.Geometry;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;
using MauiPathGeometryConverter = Microsoft.Maui.Controls.Shapes.PathGeometryConverter;
using MauiStretch = Microsoft.Maui.Controls.Stretch;

namespace MathSolver.Controls;

/// <summary>
/// Hiển thị path vector lấy từ SVG và đổi màu hoàn toàn bằng code theo theme.
/// Không kế thừa Shapes.Path vì Path là sealed; thay vào đó control bọc một
/// MauiPath thật ở bên trong ContentView.
/// </summary>
public sealed class ThemedSvgPathIcon : ContentView
{
    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(
            nameof(Data),
            typeof(string),
            typeof(ThemedSvgPathIcon),
            string.Empty,
            propertyChanged: OnDataChanged);

    private readonly MauiPath _path;
    private bool _isThemeSubscribed;

    public ThemedSvgPathIcon()
    {
        InputTransparent = true;

        _path =
            new MauiPath
            {
                Aspect =
                    MauiStretch.Uniform,

                StrokeThickness =
                    0d,

                InputTransparent =
                    true,

                HorizontalOptions =
                    LayoutOptions.Fill,

                VerticalOptions =
                    LayoutOptions.Fill
            };

        Content =
            _path;

        Loaded +=
            OnLoaded;

        Unloaded +=
            OnUnloaded;

        ApplyThemeColor();
    }

    /// <summary>
    /// SVG-compatible path markup, ví dụ: M10,10 L20,20 Z.
    /// Để kiểu string nhằm tránh phụ thuộc XAML converter của custom control;
    /// geometry được parse rõ ràng trong code bằng PathGeometryConverter.
    /// </summary>
    public string Data
    {
        get =>
            (string)GetValue(
                DataProperty);

        set =>
            SetValue(
                DataProperty,
                value);
    }

    private static void OnDataChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        if (bindable is not
            ThemedSvgPathIcon icon)
        {
            return;
        }

        icon.ApplyPathData(
            newValue as string);
    }

    private void ApplyPathData(
        string? pathMarkup)
    {
        if (string.IsNullOrWhiteSpace(
                pathMarkup))
        {
            _path.Data =
                null;

            return;
        }

        // .NET MAUI hỗ trợ SVG-compatible path markup qua
        // PathGeometryConverter.ConvertFromInvariantString().
        _path.Data =
            (MauiGeometry)new MauiPathGeometryConverter()
                .ConvertFromInvariantString(
                    pathMarkup);
    }

    private void OnLoaded(
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

        // Luôn áp lại màu khi control được attach/re-attach vào native tree.
        ApplyThemeColor();
    }

    private void OnUnloaded(
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
        // AppThemeManager luôn phát ThemeChanged trên UI thread, nên không cần
        // Dispatcher ở control này. Việc bỏ Dispatcher cũng tránh phụ thuộc API
        // platform/dispatcher không cần thiết.
        ApplyThemeColor();
    }

    private void ApplyThemeColor()
    {
        Color iconColor =
            AppThemeManager.MonochromeIconColor;

        // Gán Brush mới để native Shape chắc chắn invalidates rendering ngay cả
        // khi Popup/TitleView vừa được attach lại.
        _path.Fill =
            new SolidColorBrush(
                iconColor);

        _path.Stroke =
            null;
    }
}
