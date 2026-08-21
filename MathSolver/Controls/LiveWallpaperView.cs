using CommunityToolkit.Maui.Views;
using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>
/// Lightweight MP4 wallpaper host shared by the four main learning tabs.
/// Playback exists only while the owning page is active, so inactive Shell
/// tabs do not continue decoding video in the background.
/// </summary>
public sealed class LiveWallpaperView : Grid
{
    private readonly MediaElement _mediaElement;
    private readonly BoxView _readabilityScrim;

    private bool _isPageActive;
    private bool _isSubscribed;
    private string? _loadedPath;

    public LiveWallpaperView()
    {
        InputTransparent = true;
        ZIndex = -100;

        _mediaElement = new MediaElement
        {
            Aspect = Aspect.AspectFill,
            ShouldAutoPlay = true,
            ShouldLoopPlayback = true,
            ShouldMute = true,
            ShouldKeepScreenOn = false,
            ShouldShowPlaybackControls = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        _readabilityScrim = new BoxView
        {
            Opacity = 1d,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _readabilityScrim.SetDynamicResource(
            BoxView.ColorProperty,
            "LiveWallpaperScrimColor");

        Children.Add(_mediaElement);
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

        // Release the file handle, not just the decoder clock. Settings can
        // replace/remove the MP4 while this main tab is not active, including
        // on Windows where an opened MediaPlayer source can lock the file.
        ReleaseSource();
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

        // Existing wallpapers from versions before the hardware-decode policy
        // are validated once on first load. New imports are already validated.
        await LiveWallpaperManager.EnsureOptimizedWallpaperAsync();
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
        ReleaseSource();
    }

    private void OnWallpaperSettingsChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            RefreshPlayback);
    }

    private void OnPlaybackPolicyChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            RefreshPlayback);
    }

    private void RefreshPlayback()
    {
        RefreshVisibility();

        if (!IsVisible ||
            !_isPageActive ||
            LiveWallpaperPlaybackCoordinator.IsPlaybackSuspended)
        {
            try
            {
                // Keep the native source warm while latency-sensitive work
                // runs, but stop frame decode/composition immediately. This
                // resumes much faster than rebuilding the player source.
                _mediaElement.Pause();
            }
            catch
            {
            }

            return;
        }

        string path =
            LiveWallpaperManager.WallpaperPath;

        if (!string.Equals(
                _loadedPath,
                path,
                StringComparison.Ordinal))
        {
            _loadedPath = path;
            _mediaElement.Source =
                MediaSource.FromFile(path);
        }
        else
        {
            try
            {
                _mediaElement.Play();
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

        IsVisible = shouldShow;

        if (!shouldShow)
        {
            ReleaseSource();
        }
    }

    private void ReleaseSource()
    {
        try
        {
            _mediaElement.Stop();
        }
        catch
        {
        }

        _mediaElement.Source = null;
        _loadedPath = null;
    }
}
