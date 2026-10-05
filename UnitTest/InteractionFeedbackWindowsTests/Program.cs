using System.Reflection;
using MathSolver.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls.Xaml;
using NativeBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using NativeButton = Microsoft.UI.Xaml.Controls.Button;
using NativeApplication = Microsoft.UI.Xaml.Application;

// Exercise the actual WinUI ResourceDictionary on its UI thread. No app database,
// visible window or production application services are involved in this check.
internal static class Program
{
    internal static string ReportPath = "";
    private static bool _reproducePrevious;

    [STAThread]
    private static int Main(string[] args)
    {
        ReportPath = args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "result.txt");
        _reproducePrevious = args.Contains("--reproduce-previous");
        WinRT.ComWrappersSupport.InitializeComWrappers();
        NativeApplication.Start(_ => new InteractionFeedbackWindowsTests.SmokeApplication());
        return Environment.ExitCode;
    }

    internal static void RunTests()
        {
            try
            {
                using var mauiApp = MauiApp.CreateBuilder().UseMauiApp<SmokeMauiApplication>().Build();
                var context = new MauiContext(mauiApp.Services);
                if (_reproducePrevious)
                {
                    ReproducePrevious(context);
                    File.WriteAllText(ReportPath, "Previous resource ownership failure was not reproduced.");
                    return;
                }
                InteractiveColorFeedback.Configure();
                TestInitialBorders(context);
                Test(new Button { BackgroundColor = Colors.Green, TextColor = Colors.White, BorderColor = Colors.Red }, context);
                Test(new ImageButton { BackgroundColor = Colors.Transparent }, context);
                TestResponsiveSizing(context);
                File.WriteAllText(ReportPath, "PASS: native Button/ImageButton resources, borders, hover/press, keyboard focus, wrapped large-text sizing in light/dark themes and centered viewport width bindings.");
            }
            catch (Exception exception)
            {
                Environment.ExitCode = 1;
                File.WriteAllText(ReportPath, exception.ToString());
            }
            finally { NativeApplication.Current.Exit(); }
    }

    public sealed class SmokeMauiApplication : Microsoft.Maui.Controls.Application { }

    private static void TestResponsiveSizing(MauiContext context)
    {
        var resources = new ResourceDictionary().LoadFromXaml(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SolverStyles.source.xaml")));
        var style = (Style)resources["SolverChoiceButtonStyle"];
        foreach (var theme in new[] { Microsoft.UI.Xaml.ElementTheme.Light, Microsoft.UI.Xaml.ElementTheme.Dark })
        foreach (double font in new[] { 16d, 24d, 32d })
        {
            var button = new Button { Style = style, Text = "Find the original number when a fraction is known",
                FontSize = font, LineBreakMode = LineBreakMode.WordWrap };
            var control = (NativeButton)button.ToPlatform(context);
            control.RequestedTheme = theme;
            control.Measure(new global::Windows.Foundation.Size(180, double.PositiveInfinity));
            Assert(button.HeightRequest == -1 && button.MinimumHeightRequest >= 48,
                "Text choices must have a minimum height without a fixed height.");
            Assert(control.DesiredSize.Height > 48,
                "A wrapped long choice must grow beyond its minimum height with larger text.");
        }
        var content = new VerticalStackLayout { Style = (Style)resources["SolverContentStyle"] };
        var scroll = new ScrollView { Content = content };
        foreach (double width in new[] { 360d, 800d, 1920d })
        {
            scroll.Arrange(new Rect(0, 0, width, 600));
            Assert(content.WidthRequest == width, "Content width must follow the logical viewport when resizing.");
            Assert(content.MaximumWidthRequest == 1320 && content.HorizontalOptions.Alignment == LayoutAlignment.Center,
                "Reading pages must remain centered and limited on wide screens.");
        }
    }

    private static void TestInitialBorders(MauiContext context)
    {
        foreach (var theme in new[] { Microsoft.UI.Xaml.ElementTheme.Light, Microsoft.UI.Xaml.ElementTheme.Dark })
        {
            Color color = Color.FromArgb(theme == Microsoft.UI.Xaml.ElementTheme.Light ? "#DCE7F5" : "#475569");
            var button = new Button { BackgroundColor = Colors.Transparent, BorderColor = color, BorderWidth = 1.5 };
            var control = (NativeButton)button.ToPlatform(context);
            control.RequestedTheme = theme;
            Assert(control.BorderBrush is NativeBrush brush && Math.Abs(brush.Color.R / 255f - color.Red) < 0.005,
                "The initial native border must use the MAUI color before any click.");
            Assert(control.BorderThickness.Left == 1.5 && control.BorderThickness.Bottom == 1.5,
                "The initial native border must use the MAUI width before any click.");
            button.BorderWidth = 2;
            Assert(control.BorderThickness.Left == 2, "Border width changes must refresh without changing color.");
            button.BorderWidth = 0;
            Assert(control.BorderThickness.Left == 0, "Borderless actions must remain borderless.");
        }
    }

    private static void ReproducePrevious(MauiContext context)
    {
        var control = (NativeButton)new ImageButton { BackgroundColor = Colors.Transparent }.ToPlatform(context);
        var brush = new NativeBrush(Microsoft.UI.Colors.Green);
        for (int i = 0; i < 5; i++)
        {
            control.Background = brush;
            control.Resources["ButtonBackgroundPointerOver"] = brush;
            control.Resources["ButtonBackgroundPressed"] = brush;
        }
    }

    private static void Test(View view, MauiContext context)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(InteractiveColorFeedback).GetNestedType("FeedbackState", BindingFlags.NonPublic)!;
        var control = (NativeButton)view.ToPlatform(context);
        Assert(control.UseSystemFocusVisuals && control.FocusVisualPrimaryThickness.Left == 2 &&
            control.FocusVisualSecondaryThickness.Left == 1, "Keyboard focus must have a visible independent outline.");
        object states = typeof(InteractiveColorFeedback).GetField("States", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] lookup = [view, null];
        Assert((bool)states.GetType().GetMethod("TryGetValue")!.Invoke(states, lookup)!, "Handler must attach feedback during initialization.");
        object state = lookup[1]!;
        MethodInfo update = type.GetMethod("UpdateWindowsColors", flags)!;
        update.Invoke(state, null);
        var normal = (NativeBrush)control.Background;
        var hover = (NativeBrush)control.Resources["ButtonBackgroundPointerOver"];
        var pressed = (NativeBrush)control.Resources["ButtonBackgroundPressed"];
        Assert(!ReferenceEquals(normal, hover) && !ReferenceEquals(normal, pressed) && !ReferenceEquals(hover, pressed),
            $"Background and each resource must own separate brushes ({view.GetType().Name}).");

        for (int i = 0; i < 5; i++) update.Invoke(state, null);
        Assert(ReferenceEquals(hover, control.Resources["ButtonBackgroundPointerOver"]), "Refresh must update existing resources.");
        view.BackgroundColor = Colors.Blue;
        update.Invoke(state, null);
        normal = (NativeBrush)control.Background;
        hover = (NativeBrush)control.Resources["ButtonBackgroundPointerOver"];
        pressed = (NativeBrush)control.Resources["ButtonBackgroundPressed"];
        Assert(normal.Color.B == 255 && normal.Color.R == 0, "Theme/selection background must refresh.");
        Assert(normal.Color.Equals(hover.Color) && normal.Color.Equals(pressed.Color), "All states must refresh together.");
        if (view is Button button)
        {
            button.TextColor = Colors.Yellow;
            button.BorderColor = Colors.Blue;
            update.Invoke(state, null);
            Assert(((NativeBrush)control.Resources["ButtonForegroundPointerOver"]).Color.Equals(Microsoft.UI.Colors.Yellow),
                "Text color mapping must retain the new foreground.");
            Assert(((NativeBrush)control.Resources["ButtonBorderBrushPressed"]).Color.Equals(Microsoft.UI.Colors.Blue),
                "Border color mapping must retain the new stroke.");
        }

        type.GetField("_hovered", flags)!.SetValue(state, true);
        update.Invoke(state, null);
        Assert(!normal.Color.Equals(Microsoft.UI.Colors.Blue), "Hover must change background.");
        type.GetField("_pressed", flags)!.SetValue(state, true);
        update.Invoke(state, null);
        Assert(normal.Color.Equals(hover.Color) && normal.Color.Equals(pressed.Color), "Native state brushes must stay synchronized.");

        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        type.GetField("_animation", flags)!.SetValue(state, storyboard);
        MethodInfo addAnimation = type.GetMethod("AddColorAnimation", flags)!;
        foreach (NativeBrush brush in new[] { normal, hover, pressed })
            addAnimation.Invoke(state, [brush, brush.Color, Microsoft.UI.Colors.Blue]);
        Assert(storyboard.Children.Count == 3, "Animate every native state brush.");
        storyboard.Begin();
        storyboard.Stop();

        view.IsEnabled = false;
        type.GetMethod("Refresh", flags)!.Invoke(state, null);
        Assert(((NativeBrush)control.Background).Color.Equals(Microsoft.UI.Colors.Blue), "Disabling must restore the base color.");
        type.GetMethod("Detach", flags)!.Invoke(state, null);
        type.GetMethod("Refresh", flags)!.Invoke(state, null);
        Assert(((NativeBrush)control.Background).Color.Equals(Microsoft.UI.Colors.Blue), "Reattaching must initialize resources safely.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

namespace MathSolver.Services
{
    // Only theme lookup is replaced; the production feedback and palette are linked above.
    internal static class AppThemeManager
    {
        internal static bool IsDarkThemeEffective => false;
        internal static event EventHandler ThemeChanged { add { } remove { } }
    }
}
