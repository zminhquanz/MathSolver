using global::Windows.Graphics;
using MathSolver.Services;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using WinRT.Interop;
using MauiWindow = Microsoft.Maui.Controls.Window;

namespace MathSolver.Platforms.Windows;

/// <summary>
/// Persists and restores the main Windows window state.
///
/// Supported states:
/// - Restored
/// - Maximized
/// - Minimized
/// - True full-screen presenter
///
/// The last restored position and size are also retained so returning from
/// maximize/full-screen uses the same normal window bounds.
/// </summary>
public static class WindowStateManager
{

    private const string StatePreferenceKey =
        "Window.State";

    private const string PositionXPreferenceKey =
        "Window.Restored.X";

    private const string PositionYPreferenceKey =
        "Window.Restored.Y";

    private const string WidthPreferenceKey =
        "Window.Restored.Width";

    private const string HeightPreferenceKey =
        "Window.Restored.Height";

    private const int MinimumWindowWidth =
        640;

    private const int MinimumWindowHeight =
        480;

    private static MauiWindow? _mauiWindow;
    private static AppWindow? _appWindow;
    private static nint _nativeWindowHandle;
    private static RectInt32? _lastRestoredBounds;
    private static PersistedWindowState _lastObservedState =
        PersistedWindowState.Restored;

    private static bool _isRestoring;

    // The active page can install one asynchronous close guard.
    // This manager owns the native AppWindow.Closing subscription from
    // application startup, so X and Alt+F4 are intercepted reliably.
    private static object? _closeGuardOwner;
    private static Func<Task<bool>>? _closeGuardAsync;
    private static bool _isCloseGuardRunning;
    private static bool _allowCloseOnce;

    public static void Attach(
        MauiWindow window)
    {
        ArgumentNullException.ThrowIfNull(
            window);

        if (ReferenceEquals(
                _mauiWindow,
                window))
        {
            TryAttachNativeWindow(
                window);

            return;
        }

        Detach();

        _mauiWindow =
            window;

        _lastRestoredBounds =
            LoadRestoredBounds();

        window.HandlerChanged +=
            OnWindowHandlerChanged;

        window.Destroying +=
            OnWindowDestroying;

        TryAttachNativeWindow(
            window);
    }

    /// <summary>
    /// Installs an asynchronous guard for native Windows close requests.
    /// The callback returns true to continue closing, or false to keep the
    /// application open.
    /// </summary>
    public static void SetCloseGuard(
        object owner,
        Func<Task<bool>> closeGuardAsync)
    {
        ArgumentNullException.ThrowIfNull(
            owner);

        ArgumentNullException.ThrowIfNull(
            closeGuardAsync);

        _closeGuardOwner =
            owner;

        _closeGuardAsync =
            closeGuardAsync;
    }

    /// <summary>
    /// Removes the close guard only when it belongs to the supplied owner.
    /// </summary>
    public static void ClearCloseGuard(
        object owner)
    {
        ArgumentNullException.ThrowIfNull(
            owner);

        if (!ReferenceEquals(
                _closeGuardOwner,
                owner))
        {
            return;
        }

        _closeGuardOwner =
            null;

        _closeGuardAsync =
            null;
    }

    private static void OnWindowHandlerChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is MauiWindow window)
        {
            TryAttachNativeWindow(
                window);
        }
    }

    private static void TryAttachNativeWindow(
        MauiWindow window)
    {
        if (window.Handler?.PlatformView
            is not MauiWinUIWindow nativeWindow)
        {
            return;
        }

        AppWindow appWindow =
            nativeWindow.AppWindow;

        nint nativeWindowHandle =
            WindowNative.GetWindowHandle(
                nativeWindow);

        if (ReferenceEquals(
                _appWindow,
                appWindow))
        {
            _nativeWindowHandle =
                nativeWindowHandle;

            ApplyNativeTitleBarTheme();
            return;
        }

        DetachAppWindow();

        _appWindow =
            appWindow;

        _nativeWindowHandle =
            nativeWindowHandle;

        _lastObservedState =
            ReadSavedState();

        appWindow.Changed +=
            OnAppWindowChanged;

        appWindow.Closing +=
            OnAppWindowClosing;

        AppThemeManager.ThemeChanged +=
            OnAppThemeChanged;

        ApplyNativeTitleBarTheme();

        nativeWindow.DispatcherQueue.TryEnqueue(
            RestoreWindowState);
    }

    private static void RestoreWindowState()
    {
        AppWindow? appWindow =
            _appWindow;

        if (appWindow is null)
        {
            return;
        }

        _isRestoring =
            true;

        try
        {
            PersistedWindowState savedState =
                ReadSavedState();

            // Reapply the normal presenter first. This is required when the
            // previous session ended in true full-screen mode.
            appWindow.SetPresenter(
                AppWindowPresenterKind.Default);

            if (appWindow.Presenter
                is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.Restore();
            }

            RestoreNormalBounds(
                appWindow);

            switch (savedState)
            {
                case PersistedWindowState.FullScreen:
                    appWindow.SetPresenter(
                        AppWindowPresenterKind.FullScreen);
                    break;

                case PersistedWindowState.Maximized:
                    if (appWindow.Presenter
                        is OverlappedPresenter maximizedPresenter)
                    {
                        maximizedPresenter.Maximize();
                    }
                    break;

                case PersistedWindowState.Minimized:
                    if (appWindow.Presenter
                        is OverlappedPresenter minimizedPresenter)
                    {
                        minimizedPresenter.Minimize();
                    }
                    break;

                default:
                    if (appWindow.Presenter
                        is OverlappedPresenter restoredPresenter)
                    {
                        restoredPresenter.Restore();
                    }
                    break;
            }

            _lastObservedState =
                savedState;
        }
        finally
        {
            _isRestoring =
                false;
        }
    }

    private static void OnAppWindowChanged(
        AppWindow sender,
        AppWindowChangedEventArgs args)
    {
        if (_isRestoring)
        {
            return;
        }

        PersistedWindowState previousState =
            _lastObservedState;

        PersistedWindowState currentState =
            GetCurrentState(
                sender);

        if (currentState == PersistedWindowState.Minimized &&
            previousState != PersistedWindowState.Minimized)
        {
            LiveWallpaperManager.NotifyHostSuspended();
        }
        else if (previousState == PersistedWindowState.Minimized &&
                 currentState != PersistedWindowState.Minimized)
        {
            LiveWallpaperManager.NotifyHostResumed();
        }

        if (currentState ==
            PersistedWindowState.Restored)
        {
            CaptureRestoredBounds(
                sender);
        }

        // State changes are infrequent, so save them immediately. This also
        // preserves maximize/minimize/full-screen if Windows ends the process
        // without a normal close path.
        if (currentState !=
            _lastObservedState)
        {
            _lastObservedState =
                currentState;

            Preferences.Default.Set(
                StatePreferenceKey,
                currentState.ToString());
        }
    }

    private static async void OnAppWindowClosing(
        AppWindow sender,
        AppWindowClosingEventArgs args)
    {
        // This is the second close request issued after the user confirmed.
        // Let it continue without displaying the dialog again.
        if (_allowCloseOnce)
        {
            _allowCloseOnce =
                false;

            SaveCurrentWindowState(
                sender);

            return;
        }

        Func<Task<bool>>? closeGuard =
            _closeGuardAsync;

        if (closeGuard is null)
        {
            SaveCurrentWindowState(
                sender);

            return;
        }

        // AppWindow.Closing is synchronous. Cancel the current X / Alt+F4
        // request before awaiting the confirmation dialog.
        args.Cancel =
            true;

        // Repeated X / Alt+F4 presses while the dialog is visible remain
        // cancelled and cannot open duplicate dialogs.
        if (_isCloseGuardRunning)
        {
            return;
        }

        _isCloseGuardRunning =
            true;

        try
        {
            bool allowClose =
                await closeGuard();

            if (!allowClose)
            {
                return;
            }

            // Reissue the close request only after the page has stopped and
            // awaited every benchmark worker.
            _allowCloseOnce =
                true;

            MauiWindow? windowToClose =
                _mauiWindow;

            if (Application.Current is Application application &&
                windowToClose is not null)
            {
                application.CloseWindow(
                    windowToClose);
            }
            else
            {
                sender.Destroy();
            }
        }
        catch
        {
            // A failed confirmation must never close the application.
            _allowCloseOnce =
                false;
        }
        finally
        {
            _isCloseGuardRunning =
                false;
        }
    }

    private static void OnWindowDestroying(
        object? sender,
        EventArgs e)
    {
        // Closing normally saves from AppWindow.Closing. At this later stage
        // the native presenter may already be changing, so preserve the last
        // observed state instead of querying the window again.
        Preferences.Default.Set(
            StatePreferenceKey,
            _lastObservedState.ToString());

        SaveRestoredBounds();
        Detach();
    }

    private static void SaveCurrentWindowState(
        AppWindow appWindow)
    {
        PersistedWindowState currentState =
            GetCurrentState(
                appWindow);

        if (currentState ==
            PersistedWindowState.Restored)
        {
            CaptureRestoredBounds(
                appWindow);
        }

        Preferences.Default.Set(
            StatePreferenceKey,
            currentState.ToString());

        SaveRestoredBounds();

        _lastObservedState =
            currentState;
    }

    private static PersistedWindowState GetCurrentState(
        AppWindow appWindow)
    {
        if (appWindow.Presenter.Kind ==
            AppWindowPresenterKind.FullScreen)
        {
            return PersistedWindowState.FullScreen;
        }

        if (appWindow.Presenter
            is not OverlappedPresenter presenter)
        {
            return PersistedWindowState.Restored;
        }

        return presenter.State switch
        {
            OverlappedPresenterState.Maximized =>
                PersistedWindowState.Maximized,

            OverlappedPresenterState.Minimized =>
                PersistedWindowState.Minimized,

            _ =>
                PersistedWindowState.Restored
        };
    }

    private static PersistedWindowState ReadSavedState()
    {
        string storedValue =
            Preferences.Default.Get(
                StatePreferenceKey,
                PersistedWindowState.Restored.ToString());

        return Enum.TryParse(
                storedValue,
                ignoreCase: true,
                out PersistedWindowState state)
            ? state
            : PersistedWindowState.Restored;
    }

    private static void CaptureRestoredBounds(
        AppWindow appWindow)
    {
        if (appWindow.Size.Width <= 0 ||
            appWindow.Size.Height <= 0)
        {
            return;
        }

        _lastRestoredBounds =
            new RectInt32(
                appWindow.Position.X,
                appWindow.Position.Y,
                appWindow.Size.Width,
                appWindow.Size.Height);
    }

    private static void SaveRestoredBounds()
    {
        if (_lastRestoredBounds
            is not RectInt32 bounds)
        {
            return;
        }

        Preferences.Default.Set(
            PositionXPreferenceKey,
            bounds.X);

        Preferences.Default.Set(
            PositionYPreferenceKey,
            bounds.Y);

        Preferences.Default.Set(
            WidthPreferenceKey,
            bounds.Width);

        Preferences.Default.Set(
            HeightPreferenceKey,
            bounds.Height);
    }

    private static RectInt32? LoadRestoredBounds()
    {
        if (!Preferences.Default.ContainsKey(
                WidthPreferenceKey) ||
            !Preferences.Default.ContainsKey(
                HeightPreferenceKey))
        {
            return null;
        }

        int width =
            Preferences.Default.Get(
                WidthPreferenceKey,
                0);

        int height =
            Preferences.Default.Get(
                HeightPreferenceKey,
                0);

        if (width <= 0 ||
            height <= 0)
        {
            return null;
        }

        return new RectInt32(
            Preferences.Default.Get(
                PositionXPreferenceKey,
                0),
            Preferences.Default.Get(
                PositionYPreferenceKey,
                0),
            width,
            height);
    }

    private static void RestoreNormalBounds(
        AppWindow appWindow)
    {
        if (_lastRestoredBounds
            is not RectInt32 savedBounds)
        {
            return;
        }

        DisplayArea displayArea =
            DisplayArea.GetFromRect(
                savedBounds,
                DisplayAreaFallback.Nearest) ??
            DisplayArea.Primary;

        RectInt32 outerBounds =
            displayArea.OuterBounds;

        RectInt32 workArea =
            displayArea.WorkArea;

        int maximumWidth =
            Math.Max(
                1,
                workArea.Width);

        int maximumHeight =
            Math.Max(
                1,
                workArea.Height);

        int minimumWidth =
            Math.Min(
                MinimumWindowWidth,
                maximumWidth);

        int minimumHeight =
            Math.Min(
                MinimumWindowHeight,
                maximumHeight);

        int width =
            Math.Clamp(
                savedBounds.Width,
                minimumWidth,
                maximumWidth);

        int height =
            Math.Clamp(
                savedBounds.Height,
                minimumHeight,
                maximumHeight);

        // MoveAndResize(rect, displayArea) expects coordinates relative to
        // the selected display area. Saved AppWindow coordinates are global.
        int relativeX =
            savedBounds.X -
            outerBounds.X;

        int relativeY =
            savedBounds.Y -
            outerBounds.Y;

        int maximumX =
            workArea.X +
            workArea.Width -
            width;

        int maximumY =
            workArea.Y +
            workArea.Height -
            height;

        relativeX =
            Math.Clamp(
                relativeX,
                workArea.X,
                Math.Max(
                    workArea.X,
                    maximumX));

        relativeY =
            Math.Clamp(
                relativeY,
                workArea.Y,
                Math.Max(
                    workArea.Y,
                    maximumY));

        appWindow.MoveAndResize(
            new RectInt32(
                relativeX,
                relativeY,
                width,
                height),
            displayArea);
    }

    private static void OnAppThemeChanged(
        object? sender,
        EventArgs e)
    {
        ApplyNativeTitleBarTheme();
    }

    private static void ApplyNativeTitleBarTheme()
    {
        AppWindow? appWindow =
            _appWindow;

        if (appWindow is null)
        {
            return;
        }

        bool isDark =
            AppThemeManager.IsDarkThemeEffective;

        global::Windows.UI.Color foreground =
            isDark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindowTitleBar titleBar =
                appWindow.TitleBar;

            // Keep Windows App SDK caption buttons and foreground in sync.
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveForegroundColor = foreground;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = foreground;
        }
    }

    [DllImport(
        "dwmapi.dll",
        PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);


    private static void Detach()
    {
        if (_mauiWindow is not null)
        {
            _mauiWindow.HandlerChanged -=
                OnWindowHandlerChanged;

            _mauiWindow.Destroying -=
                OnWindowDestroying;
        }

        DetachAppWindow();

        _mauiWindow =
            null;
    }

    private static void DetachAppWindow()
    {
        if (_appWindow is null)
        {
            return;
        }

        _appWindow.Changed -=
            OnAppWindowChanged;

        _appWindow.Closing -=
            OnAppWindowClosing;

        AppThemeManager.ThemeChanged -=
            OnAppThemeChanged;

        _appWindow =
            null;

        _nativeWindowHandle =
            0;

        _allowCloseOnce =
            false;

        _isCloseGuardRunning =
            false;
    }

    private enum PersistedWindowState
    {
        Restored,
        Maximized,
        Minimized,
        FullScreen
    }
}
