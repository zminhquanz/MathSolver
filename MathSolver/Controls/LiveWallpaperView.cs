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
        TimeSpan.FromMilliseconds(250);

    // Shell caches the four learning pages. Keep a single global owner so a
    // stale OnDisappearing/OnAppearing sequence can never leave two native
    // video decoders (or two animation timers) alive at the same time.
    private static readonly object PlaybackOwnerLock = new();
    private static WeakReference<LiveWallpaperView>? s_playbackOwner;

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
    private bool _ownsPlayback;
    private bool _mp4PlaybackReportedActive;
    private bool _isHostSuspended;

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
        ClaimPlaybackOwnership();
        RefreshPlayback();
    }

    public void Pause()
    {
        _isPageActive = false;
        _refreshGeneration++;

        ReleasePlaybackOwnership();
        ReleaseAllAnimatedResources(immediateMediaRelease: true);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (!_isSubscribed)
        {
            LiveWallpaperManager.SettingsChanged +=
                OnWallpaperSettingsChanged;
            LiveWallpaperManager.HostSuspended +=
                OnHostSuspended;
            LiveWallpaperManager.HostResumed +=
                OnHostResumed;
            _isSubscribed = true;
        }

        // Existing wallpapers are validated by Settings/import. Do not open a
        // metadata/thumbnail decoder merely because Shell preloads an inactive
        // learning page.
        RefreshPlayback();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_isSubscribed)
        {
            LiveWallpaperManager.SettingsChanged -=
                OnWallpaperSettingsChanged;
            LiveWallpaperManager.HostSuspended -=
                OnHostSuspended;
            LiveWallpaperManager.HostResumed -=
                OnHostResumed;
            _isSubscribed = false;
        }

        _isPageActive = false;
        _refreshGeneration++;
        _mediaReleaseGeneration++;

        ReleasePlaybackOwnership();
        ReleaseAllAnimatedResources(immediateMediaRelease: true);
    }

    private void OnWallpaperSettingsChanged(
        object? sender,
        EventArgs e)
    {
        QueuePlaybackRefresh();
    }

    private void OnHostSuspended(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                if (_isHostSuspended)
                {
                    return;
                }

                _isHostSuspended = true;
                _refreshGeneration++;
                _mediaReleaseGeneration++;

                // Keep page/activity state and ownership intact, but drop every
                // transient native surface while the window is minimized. This
                // lowers minimized RAM and avoids Windows invalidating an old
                // MediaPlayer surface behind our back.
                ReleaseAllAnimatedResources(immediateMediaRelease: true);
            });
    }

    private void OnHostResumed(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                if (!_isHostSuspended)
                {
                    return;
                }

                _isHostSuspended = false;

                // Recreate the native player/GraphicsView from scratch instead
                // of calling Play() on a MediaElement whose composition surface
                // may have been reclaimed during a long minimize.
                int generation = ++_refreshGeneration;
                Dispatcher.DispatchDelayed(
                    TimeSpan.FromMilliseconds(48),
                    () =>
                    {
                        if (generation != _refreshGeneration ||
                            _isHostSuspended)
                        {
                            return;
                        }

                        RefreshPlayback();
                    });
            });
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

        if (!LiveWallpaperManager.IsEnabled ||
            !_isPageActive ||
            _isHostSuspended)
        {
            ReleasePlaybackOwnership();
            ReleaseAllAnimatedResources(immediateMediaRelease: true);
            return;
        }

        ClaimPlaybackOwnership();
        EnsureScrim();

        LiveWallpaperMode mode =
            LiveWallpaperManager.Mode;

        if (mode == LiveWallpaperMode.MathAnimation)
        {
            // Math Animation is intentionally allowed to continue during AI/LLM
            // inference. At 24 FPS its ambient drawing overhead is very small.
            StopAdaptiveContrast();
            AppThemeManager.ResetLiveWallpaperAdaptiveContrast();

            // Drop the media source immediately so decoder surfaces are
            // returned even though the MediaElement shell itself is released
            // on a short delay to keep Picker mode switching re-entrancy safe.
            ReleaseSource();
            ScheduleMediaElementRelease();
            StartMathAnimation();
            return;
        }

        // MP4 mode was accepted only after H.264/hardware-path validation.
        // Keep playback uninterrupted during AI/LLM inference. Tell the manager
        // before creating the player so optional frame analysis cannot overlap
        // a second native decoder with live playback.
        ReleaseMathAnimationResources();
        CancelScheduledMediaElementRelease();
        SetMp4PlaybackReportedActive(true);

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
                showMath && _isPageActive && !_isHostSuspended;
        }

        if (_readabilityScrim is not null)
        {
            _readabilityScrim.IsVisible =
                shouldShow && _isPageActive && !_isHostSuspended;
        }

        if (_mediaElement is not null)
        {
            _mediaElement.IsVisible =
                showMp4 && _isPageActive && !_isHostSuspended;
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
            ReleaseSource();
            ScheduleMediaElementRelease();
        }
    }

    private void ClaimPlaybackOwnership()
    {
        LiveWallpaperView? previousOwner = null;

        lock (PlaybackOwnerLock)
        {
            if (s_playbackOwner is not null &&
                s_playbackOwner.TryGetTarget(out LiveWallpaperView? current) &&
                !ReferenceEquals(current, this))
            {
                previousOwner = current;
            }

            s_playbackOwner = new WeakReference<LiveWallpaperView>(this);
            _ownsPlayback = true;
        }

        // Never tear down a previous native handler while holding the static
        // lock. A Shell tab transition can otherwise deadlock the UI thread.
        previousOwner?.DeactivateForOwnershipTransfer();
    }

    private void ReleasePlaybackOwnership()
    {
        lock (PlaybackOwnerLock)
        {
            if (!_ownsPlayback)
            {
                return;
            }

            if (s_playbackOwner is not null &&
                s_playbackOwner.TryGetTarget(out LiveWallpaperView? current) &&
                ReferenceEquals(current, this))
            {
                s_playbackOwner = null;
            }

            _ownsPlayback = false;
        }
    }

    private void DeactivateForOwnershipTransfer()
    {
        _isPageActive = false;
        _refreshGeneration++;
        _mediaReleaseGeneration++;
        _ownsPlayback = false;
        ReleaseAllAnimatedResources(immediateMediaRelease: true);
    }

    private void ReleaseAllAnimatedResources(bool immediateMediaRelease)
    {
        ReleaseMathAnimationResources();
        StopAdaptiveContrast();
        ReleaseScrim();

        if (immediateMediaRelease)
        {
            ReleaseMediaElement();
        }
        else
        {
            ReleaseSource();
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

        if (view is not null)
        {
            try
            {
                view.Handler?.DisconnectHandler();
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

    private void SetMp4PlaybackReportedActive(bool active)
    {
        if (_mp4PlaybackReportedActive == active)
        {
            return;
        }

        _mp4PlaybackReportedActive = active;
        LiveWallpaperManager.NotifyMp4PlaybackState(active);
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
        SetMp4PlaybackReportedActive(false);

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

        // Prevent the native player from scheduling another loop/autoplay
        // transition while its handler is being disconnected.
        try
        {
            mediaElement.ShouldAutoPlay = false;
            mediaElement.ShouldLoopPlayback = false;
            mediaElement.IsVisible = false;
            Children.Remove(mediaElement);
        }
        catch
        {
        }

        // Removing a MAUI view from the visual tree does not guarantee that a
        // cached Shell page immediately disconnects its native handler. Force
        // the disconnect here so MediaPlayer/ExoPlayer can release decoder
        // surfaces, queues and textures without waiting for a future GC.
        try
        {
            mediaElement.Handler?.DisconnectHandler();
        }
        catch
        {
        }

        _mediaElement = null;
    }
}
