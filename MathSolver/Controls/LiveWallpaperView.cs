using CommunityToolkit.Maui.Views;
using MathSolver.Graphics;
using MathSolver.Services;
using Microsoft.Maui.Dispatching;

namespace MathSolver.Controls;

/// <summary>
/// Shared animated-background host for the four learning tabs. Both the built-in
/// GraphicsView animation and validated hardware-decoded H.264 MP4 keep running
/// during local AI inference. All native/drawing resources are allocated lazily
/// and are released when the background or owning tab is inactive.
/// </summary>
public sealed class LiveWallpaperView : Grid
{
    private static readonly TimeSpan AnimationInterval =
        TimeSpan.FromMilliseconds(1000d / 24d);

    private static readonly TimeSpan ContrastUpdateInterval =
        TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan MediaElementReleaseDelay =
        TimeSpan.FromMilliseconds(750);

    private MathAnimatedBackgroundDrawable? _mathDrawable;
    private GraphicsView? _mathAnimationView;
    private BoxView? _readabilityScrim;
    private MediaElement? _mediaElement;
    private IDispatcherTimer? _animationTimer;
    private IDispatcherTimer? _contrastTimer;
    private LiveWallpaperFrameProfile? _frameProfile;

    private bool _isPageActive;
    private bool _isSubscribed;
    private string? _loadedPath;
    private int _refreshGeneration;
    private int _mediaReleaseGeneration;

    public LiveWallpaperView()
    {
        InputTransparent = true;
        ZIndex = -100;

        // No GraphicsView, drawable, timer, MediaElement, or scrim is created
        // here. With wallpaper disabled the control stays almost allocation-free.
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        RefreshVisibility();
    }

    public void Resume()
    {
        _isPageActive = true;
        RefreshPlayback();
    }

    public void Pause()
    {
        _isPageActive = false;
        _refreshGeneration++;

        ReleaseMathAnimationResources();
        StopAdaptiveContrast();
        ReleaseScrim();

        // Detaching Source releases decoder buffers/video textures/file handles.
        ReleaseSource();
        ScheduleMediaElementRelease();
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (!_isSubscribed)
        {
            LiveWallpaperManager.SettingsChanged +=
                OnWallpaperSettingsChanged;
            _isSubscribed = true;
        }

        if (LiveWallpaperManager.HasWallpaper &&
            LiveWallpaperManager.Mode == LiveWallpaperMode.Mp4)
        {
            await LiveWallpaperManager.EnsureOptimizedWallpaperAsync();
        }

        RefreshPlayback();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_isSubscribed)
        {
            LiveWallpaperManager.SettingsChanged -=
                OnWallpaperSettingsChanged;
            _isSubscribed = false;
        }

        _isPageActive = false;
        _refreshGeneration++;
        _mediaReleaseGeneration++;

        ReleaseMathAnimationResources();
        StopAdaptiveContrast();
        ReleaseScrim();
        ReleaseMediaElement();
    }

    private void OnWallpaperSettingsChanged(
        object? sender,
        EventArgs e)
    {
        QueuePlaybackRefresh();
    }

    private void QueuePlaybackRefresh()
    {
        int generation = ++_refreshGeneration;

        // Picker mode changes are coalesced to the next frame so WinUI does not
        // tear down/create native media while ComboBox is closing its popup.
        Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(16),
            () =>
            {
                if (generation != _refreshGeneration)
                {
                    return;
                }

                RefreshPlayback();
            });
    }

    private void RefreshPlayback()
    {
        RefreshVisibility();

        if (!LiveWallpaperManager.IsEnabled || !_isPageActive)
        {
            ReleaseMathAnimationResources();
            StopAdaptiveContrast();
            ReleaseScrim();
            PauseMediaPlayback();
            return;
        }

        EnsureScrim();

        LiveWallpaperMode mode =
            LiveWallpaperManager.Mode;

        if (mode == LiveWallpaperMode.MathAnimation)
        {
            // Math Animation is intentionally allowed to continue during AI/LLM
            // inference. At 24 FPS its ambient drawing overhead is very small.
            StopAdaptiveContrast();
            AppThemeManager.ResetLiveWallpaperAdaptiveContrast();

            PauseMediaPlayback();
            ScheduleMediaElementRelease();
            StartMathAnimation();
            return;
        }

        // MP4 mode was accepted only after H.264/hardware-path validation.
        // Keep playback uninterrupted during AI/LLM inference.
        ReleaseMathAnimationResources();
        CancelScheduledMediaElementRelease();

        MediaElement mediaElement =
            EnsureMediaElement();
        mediaElement.IsVisible = true;

        string path =
            LiveWallpaperManager.WallpaperPath;

        if (!string.Equals(
                _loadedPath,
                path,
                StringComparison.Ordinal))
        {
            _loadedPath = path;
            mediaElement.Source =
                MediaSource.FromFile(path);
        }
        else
        {
            try
            {
                mediaElement.Play();
            }
            catch
            {
                // AutoPlay starts playback once the native source is ready.
            }
        }

        StartAdaptiveContrast();
    }

    private void RefreshVisibility()
    {
        bool shouldShow =
            LiveWallpaperManager.IsEnabled;
        bool showMath =
            shouldShow &&
            LiveWallpaperManager.Mode ==
                LiveWallpaperMode.MathAnimation;
        bool showMp4 =
            shouldShow &&
            LiveWallpaperManager.Mode ==
                LiveWallpaperMode.Mp4;

        IsVisible = shouldShow;

        if (_mathAnimationView is not null)
        {
            _mathAnimationView.IsVisible =
                showMath && _isPageActive;
        }

        if (_readabilityScrim is not null)
        {
            _readabilityScrim.IsVisible =
                shouldShow && _isPageActive;
        }

        if (_mediaElement is not null)
        {
            _mediaElement.IsVisible =
                showMp4 && _isPageActive;
        }

        if (!shouldShow)
        {
            ReleaseMathAnimationResources();
            StopAdaptiveContrast();
            ReleaseScrim();
            ReleaseSource();
            ScheduleMediaElementRelease();
            AppThemeManager.ResetLiveWallpaperAdaptiveContrast();
            return;
        }

        if (!showMath)
        {
            ReleaseMathAnimationResources();
        }

        if (!showMp4)
        {
            StopAdaptiveContrast();
            PauseMediaPlayback();
            ScheduleMediaElementRelease();
        }
    }

    private GraphicsView EnsureMathAnimationView()
    {
        if (_mathAnimationView is not null)
        {
            return _mathAnimationView;
        }

        _mathDrawable =
            new MathAnimatedBackgroundDrawable();

        _mathAnimationView = new GraphicsView
        {
            Drawable = _mathDrawable,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = false,
            ZIndex = 0
        };

        Children.Add(_mathAnimationView);
        return _mathAnimationView;
    }

    private void EnsureScrim()
    {
        if (_readabilityScrim is not null)
        {
            _readabilityScrim.IsVisible = true;
            return;
        }

        _readabilityScrim = new BoxView
        {
            Opacity = 1d,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = true,
            ZIndex = 2
        };

        _readabilityScrim.SetDynamicResource(
            BoxView.ColorProperty,
            "LiveWallpaperScrimColor");

        Children.Add(_readabilityScrim);
    }

    private void ReleaseScrim()
    {
        BoxView? scrim = _readabilityScrim;
        if (scrim is null)
        {
            return;
        }

        try
        {
            Children.Remove(scrim);
        }
        catch
        {
        }

        _readabilityScrim = null;
    }

    private MediaElement EnsureMediaElement()
    {
        if (_mediaElement is not null)
        {
            return _mediaElement;
        }

        var mediaElement = new MediaElement
        {
            Aspect = Aspect.AspectFill,
            ShouldAutoPlay = true,
            ShouldLoopPlayback = true,
            ShouldMute = true,
            ShouldKeepScreenOn = false,
            ShouldShowPlaybackControls = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = false,
            ZIndex = 1
        };

        _mediaElement = mediaElement;
        Children.Add(mediaElement);
        return mediaElement;
    }

    private void EnsureAnimationTimer()
    {
        if (_animationTimer is not null)
        {
            return;
        }

        _animationTimer = Dispatcher.CreateTimer();
        _animationTimer.Interval = AnimationInterval;
        _animationTimer.IsRepeating = true;
        _animationTimer.Tick += OnAnimationTick;
    }

    private void StartMathAnimation()
    {
        GraphicsView view =
            EnsureMathAnimationView();
        view.IsVisible = true;

        EnsureAnimationTimer();

        if (_animationTimer?.IsRunning == false)
        {
            _animationTimer.Start();
        }

        view.Invalidate();
    }

    private void ReleaseMathAnimationResources()
    {
        if (_animationTimer is not null)
        {
            try
            {
                _animationTimer.Stop();
                _animationTimer.Tick -= OnAnimationTick;
            }
            catch
            {
            }

            _animationTimer = null;
        }

        GraphicsView? view = _mathAnimationView;
        if (view is not null)
        {
            try
            {
                view.IsVisible = false;
                Children.Remove(view);
            }
            catch
            {
            }
        }

        _mathAnimationView = null;
        _mathDrawable = null;
    }

    private void OnAnimationTick(
        object? sender,
        EventArgs e)
    {
        if (!_isPageActive ||
            !LiveWallpaperManager.IsMathAnimationEnabled ||
            _mathAnimationView is null ||
            _mathDrawable is null)
        {
            ReleaseMathAnimationResources();
            return;
        }

        _mathDrawable.TimeSeconds +=
            AnimationInterval.TotalSeconds;
        _mathAnimationView.Invalidate();
    }

    private void StartAdaptiveContrast()
    {
        _frameProfile ??=
            LiveWallpaperFrameAnalysis.TryLoad();

        if (_frameProfile is null ||
            _frameProfile.LuminanceSamples.Length == 0)
        {
            AppThemeManager.ResetLiveWallpaperAdaptiveContrast();
            return;
        }

        ApplyAdaptiveContrastForCurrentPosition();

        if (_contrastTimer is null)
        {
            _contrastTimer = Dispatcher.CreateTimer();
            _contrastTimer.Interval = ContrastUpdateInterval;
            _contrastTimer.IsRepeating = true;
            _contrastTimer.Tick += OnContrastTick;
        }

        if (!_contrastTimer.IsRunning)
        {
            _contrastTimer.Start();
        }
    }

    private void StopAdaptiveContrast()
    {
        if (_contrastTimer is not null)
        {
            try
            {
                _contrastTimer.Stop();
                _contrastTimer.Tick -= OnContrastTick;
            }
            catch
            {
            }

            _contrastTimer = null;
        }

        _frameProfile = null;
    }

    private void OnContrastTick(
        object? sender,
        EventArgs e)
    {
        if (!_isPageActive ||
            !LiveWallpaperManager.IsMp4Enabled ||
            _mediaElement is null ||
            _frameProfile is null)
        {
            StopAdaptiveContrast();
            return;
        }

        ApplyAdaptiveContrastForCurrentPosition();
    }

    private void ApplyAdaptiveContrastForCurrentPosition()
    {
        MediaElement? mediaElement =
            _mediaElement;
        LiveWallpaperFrameProfile? profile =
            _frameProfile;

        if (mediaElement is null || profile is null)
        {
            return;
        }

        try
        {
            double luminance =
                profile.GetLuminance(mediaElement.Position);

            AppThemeManager.SetLiveWallpaperFrameLuminance(
                luminance);
        }
        catch
        {
            // Playback must not be affected if position metadata is unavailable
            // for a frame while the native player is changing state.
        }
    }

    private void PauseMediaPlayback()
    {
        MediaElement? mediaElement =
            _mediaElement;

        if (mediaElement is null ||
            (_loadedPath is null &&
             mediaElement.Source is null))
        {
            return;
        }

        try
        {
            mediaElement.Pause();
        }
        catch
        {
        }
    }

    private void ReleaseSource()
    {
        MediaElement? mediaElement =
            _mediaElement;

        if (mediaElement is null ||
            (_loadedPath is null &&
             mediaElement.Source is null))
        {
            _loadedPath = null;
            return;
        }

        PauseMediaPlayback();

        try
        {
            mediaElement.Source = null;
        }
        catch
        {
        }

        _loadedPath = null;
    }

    private void ScheduleMediaElementRelease()
    {
        if (_mediaElement is null)
        {
            return;
        }

        int generation =
            ++_mediaReleaseGeneration;

        Dispatcher.DispatchDelayed(
            MediaElementReleaseDelay,
            () =>
            {
                if (generation != _mediaReleaseGeneration)
                {
                    return;
                }

                bool needsMp4Now =
                    _isPageActive &&
                    LiveWallpaperManager.IsMp4Enabled;

                if (needsMp4Now)
                {
                    return;
                }

                ReleaseMediaElement();
            });
    }

    private void CancelScheduledMediaElementRelease()
    {
        _mediaReleaseGeneration++;
    }

    private void ReleaseMediaElement()
    {
        MediaElement? mediaElement =
            _mediaElement;

        if (mediaElement is null)
        {
            _loadedPath = null;
            return;
        }

        StopAdaptiveContrast();
        ReleaseSource();

        try
        {
            mediaElement.IsVisible = false;
            Children.Remove(mediaElement);
        }
        catch
        {
        }

        _mediaElement = null;
    }
}
