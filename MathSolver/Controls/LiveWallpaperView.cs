using CommunityToolkit.Maui.Views;
using MathSolver.Graphics;
using MathSolver.Services;
using Microsoft.Maui.Dispatching;

namespace MathSolver.Controls;

/// <summary>
/// Shared animated-background host for the four main learning tabs. The built-in
/// GraphicsView animation yields to latency-sensitive local AI work. A validated
/// H.264 MP4 keeps playing because its native hardware-decoder path does not use
/// the CPU-heavy software decode path that originally motivated the AI pause.
/// Native media objects are created lazily and released when they are not needed
/// so inactive tabs do not retain decoder buffers or video textures.
/// </summary>
public sealed class LiveWallpaperView : Grid
{
    private static readonly TimeSpan AnimationInterval =
        TimeSpan.FromMilliseconds(1000d / 24d);

    private static readonly TimeSpan MediaElementReleaseDelay =
        TimeSpan.FromMilliseconds(750);

    private readonly MathAnimatedBackgroundDrawable _mathDrawable;
    private readonly GraphicsView _mathAnimationView;
    private readonly BoxView _readabilityScrim;

    private MediaElement? _mediaElement;
    private IDispatcherTimer? _animationTimer;
    private bool _isPageActive;
    private bool _isSubscribed;
    private string? _loadedPath;
    private int _refreshGeneration;
    private int _mediaReleaseGeneration;

    public LiveWallpaperView()
    {
        InputTransparent = true;
        ZIndex = -100;

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

        _readabilityScrim = new BoxView
        {
            Opacity = 1d,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = 2
        };
        _readabilityScrim.SetDynamicResource(
            BoxView.ColorProperty,
            "LiveWallpaperScrimColor");

        // MediaElement is intentionally not created here. Most users can use
        // the lightweight Math animation without allocating a native player,
        // decoder surfaces, or video textures on every main page instance.
        Children.Add(_mathAnimationView);
        Children.Add(_readabilityScrim);

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
        StopMathAnimation();

        // Detaching Source immediately releases the expensive decoder buffers
        // and file handle. The MediaElement shell itself is retired shortly
        // afterwards so fast tab switches do not churn native player creation.
        ReleaseSource();
        ScheduleMediaElementRelease();
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (!_isSubscribed)
        {
            LiveWallpaperManager.SettingsChanged +=
                OnWallpaperSettingsChanged;
            LiveWallpaperPlaybackCoordinator.PlaybackPolicyChanged +=
                OnPlaybackPolicyChanged;
            _isSubscribed = true;
        }

        // Existing MP4 wallpapers from versions before the duration/hardware
        // policy are validated once on first load. The GraphicsView mode has no
        // file to validate.
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
            LiveWallpaperPlaybackCoordinator.PlaybackPolicyChanged -=
                OnPlaybackPolicyChanged;
            _isSubscribed = false;
        }

        _isPageActive = false;
        _refreshGeneration++;
        _mediaReleaseGeneration++;
        StopMathAnimation();
        ReleaseMediaElement();
    }

    private void OnWallpaperSettingsChanged(
        object? sender,
        EventArgs e)
    {
        QueuePlaybackRefresh();
    }

    private void OnPlaybackPolicyChanged(
        object? sender,
        EventArgs e)
    {
        QueuePlaybackRefresh();
    }

    private void QueuePlaybackRefresh()
    {
        int generation = ++_refreshGeneration;

        // Do not tear down/start native media while the Picker is still inside
        // its SelectionChanged call stack. Coalescing to the next UI frame also
        // makes rapid Math <-> MP4 switching deterministic instead of letting
        // stale refreshes race each other.
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

        if (!IsVisible || !_isPageActive)
        {
            StopMathAnimation();
            PauseMediaPlayback();
            return;
        }

        LiveWallpaperMode mode =
            LiveWallpaperManager.Mode;

        if (mode == LiveWallpaperMode.MathAnimation)
        {
            PauseMediaPlayback();
            ScheduleMediaElementRelease();

            if (LiveWallpaperPlaybackCoordinator.IsPlaybackSuspended)
            {
                StopMathAnimation();
                return;
            }

            StartMathAnimation();
            return;
        }

        // MP4 mode is only considered enabled after H.264 + hardware-path
        // validation. Keep it running during local LLM inference; the fixed
        // function video decoder has a much smaller CPU cost than software
        // decode, and this avoids an unnecessary visual pause for the user.
        StopMathAnimation();
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
                // AutoPlay will start it once the native source is ready.
            }
        }
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
        _mathAnimationView.IsVisible = showMath;
        _readabilityScrim.IsVisible = shouldShow;

        if (_mediaElement is not null)
        {
            _mediaElement.IsVisible =
                showMp4 && _isPageActive;
        }

        if (!showMath)
        {
            StopMathAnimation();
        }

        if (!showMp4)
        {
            PauseMediaPlayback();
            ScheduleMediaElementRelease();
        }
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

        // ZIndex keeps video above the Math drawable and below the readability
        // scrim, so the player can be added lazily without reshuffling children.
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
        EnsureAnimationTimer();

        if (_animationTimer?.IsRunning == false)
        {
            _animationTimer.Start();
        }

        _mathAnimationView.Invalidate();
    }

    private void StopMathAnimation()
    {
        if (_animationTimer?.IsRunning == true)
        {
            _animationTimer.Stop();
        }
    }

    private void OnAnimationTick(
        object? sender,
        EventArgs e)
    {
        if (!_isPageActive ||
            !_mathAnimationView.IsVisible ||
            LiveWallpaperPlaybackCoordinator.IsPlaybackSuspended)
        {
            StopMathAnimation();
            return;
        }

        _mathDrawable.TimeSeconds +=
            AnimationInterval.TotalSeconds;
        _mathAnimationView.Invalidate();
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

        // Pause first, then detach the source. Avoid MediaElement.Stop() here:
        // on WinUI the native MediaPlayer can synchronously transition state
        // during rapid source changes and stall the UI thread. Source=null is
        // enough to release decoder buffers, textures, and the file handle.
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
