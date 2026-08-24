namespace MathSolver.Services;

public enum LiveWallpaperMode
{
    MathAnimation = 0,
    Mp4 = 1
}

/// <summary>
/// Stores the user's animated-background preference. MP4 wallpapers are copied
/// into Math Solver's private app-data directory so Android does not lose URI
/// permissions after restart. The built-in Math animation uses GraphicsView and
/// needs no external file.
/// </summary>
public static class LiveWallpaperManager
{
    private const string EnabledPreferenceKey =
        "LiveWallpaper.Enabled";
    private const string FileNamePreferenceKey =
        "LiveWallpaper.FileName";
    private const string HardwareH264ValidatedPreferenceKey =
        "LiveWallpaper.HardwareH264Validated";
    private const string ValidationVersionPreferenceKey =
        "LiveWallpaper.ValidationVersion";
    private const string ModePreferenceKey =
        "LiveWallpaper.Mode";

    private const int CurrentValidationVersion = 3;

    private static readonly SemaphoreSlim ValidationGate =
        new(1, 1);

    private static readonly object FrameAnalysisLock = new();
    private static CancellationTokenSource? _frameAnalysisCancellation;
    private static int _frameAnalysisGeneration;
    private static bool _isFrameAnalysisRunning;
    private static bool _isMp4PlaybackActive;
    private static bool _isHostSuspended;
    private static TimeSpan _pendingFrameAnalysisDuration;

    private const string WallpaperFolderName =
        "Wallpapers";
    private const string WallpaperFileName =
        "live_wallpaper.mp4";

    public const double MaximumMp4DurationSeconds =
        LiveWallpaperVideoInspector.MaximumDurationSeconds;

    /// <summary>
    /// True only while the optional low-resolution luminance timeline is being
    /// built. MP4 validation/import is already complete at this point.
    /// </summary>
    public static bool IsFrameAnalysisRunning
    {
        get
        {
            lock (FrameAnalysisLock)
            {
                return _isFrameAnalysisRunning;
            }
        }
    }

    public static event EventHandler? SettingsChanged;

    // Native hosts can temporarily lose their composition/video surface while
    // the app window is minimized. These events deliberately do not change the
    // user's wallpaper preference; active LiveWallpaperView instances only
    // release/recreate transient rendering resources around the host state.
    public static event EventHandler? HostSuspended;
    public static event EventHandler? HostResumed;

    public static void NotifyHostSuspended()
    {
        lock (FrameAnalysisLock)
        {
            _isHostSuspended = true;
        }

        // A minimized app should not replace the live decoder with the optional
        // thumbnail-analysis decoder. Keep pending work deferred until playback
        // later becomes inactive while the host is visible again.
        CancelFrameAnalysis();
        HostSuspended?.Invoke(null, EventArgs.Empty);
    }

    public static void NotifyHostResumed()
    {
        lock (FrameAnalysisLock)
        {
            _isHostSuspended = false;
        }

        HostResumed?.Invoke(null, EventArgs.Empty);
    }

    public static string WallpaperPath =>
        Path.Combine(
            FileSystem.AppDataDirectory,
            WallpaperFolderName,
            WallpaperFileName);

    public static bool HasWallpaper =>
        File.Exists(WallpaperPath);

    public static LiveWallpaperMode Mode
    {
        get
        {
            int stored = Preferences.Default.Get(
                ModePreferenceKey,
                -1);

            if (Enum.IsDefined(
                    typeof(LiveWallpaperMode),
                    stored))
            {
                return (LiveWallpaperMode)stored;
            }

            // Preserve the behavior of installations that already had an MP4
            // before the Math GraphicsView mode was introduced.
            return HasWallpaper
                ? LiveWallpaperMode.Mp4
                : LiveWallpaperMode.MathAnimation;
        }
    }

    public static bool IsEnabled
    {
        get
        {
            if (!Preferences.Default.Get(
                    EnabledPreferenceKey,
                    false))
            {
                return false;
            }

            return Mode switch
            {
                LiveWallpaperMode.MathAnimation => true,
                LiveWallpaperMode.Mp4 =>
                    HasWallpaper &&
                    IsHardwareH264Validated,
                _ => false
            };
        }
    }

    public static bool IsMathAnimationEnabled =>
        IsEnabled &&
        Mode == LiveWallpaperMode.MathAnimation;

    public static bool IsMp4Enabled =>
        IsEnabled &&
        Mode == LiveWallpaperMode.Mp4;

    public static bool IsHardwareH264Validated =>
        HasWallpaper &&
        Preferences.Default.Get(
            HardwareH264ValidatedPreferenceKey,
            false) &&
        Preferences.Default.Get(
            ValidationVersionPreferenceKey,
            0) == CurrentValidationVersion;

    public static string? OriginalFileName
    {
        get
        {
            string fileName =
                Preferences.Default.Get(
                    FileNamePreferenceKey,
                    string.Empty);

            return string.IsNullOrWhiteSpace(fileName)
                ? null
                : fileName;
        }
    }

    public static async Task<bool> EnsureOptimizedWallpaperAsync(
        CancellationToken cancellationToken = default)
    {
        if (!HasWallpaper)
        {
            return false;
        }

        if (IsHardwareH264Validated &&
            (LiveWallpaperFrameAnalysis.HasCurrentProfile ||
             IsFrameAnalysisRunning))
        {
            return true;
        }

        await ValidationGate.WaitAsync(cancellationToken);

        try
        {
            if (IsHardwareH264Validated &&
                (LiveWallpaperFrameAnalysis.HasCurrentProfile ||
                 IsFrameAnalysisRunning))
            {
                return true;
            }

            // Metadata/codec validation is intentionally the only awaited
            // work here. The more expensive brightness timeline is optional
            // visual metadata and is queued after the wallpaper is accepted.
            LiveWallpaperVideoInspection inspection =
                await LiveWallpaperVideoInspector.InspectAsync(
                    WallpaperPath,
                    cancellationToken);

            bool isOptimized =
                inspection.IsH264 &&
                inspection.CanUseHardwarePreferredH264Path &&
                LiveWallpaperVideoInspector.IsDurationAllowed(
                    inspection.Duration) &&
                LiveWallpaperVideoInspector.IsResolutionAllowed(inspection);

            Preferences.Default.Set(
                HardwareH264ValidatedPreferenceKey,
                isOptimized);
            Preferences.Default.Set(
                ValidationVersionPreferenceKey,
                isOptimized
                    ? CurrentValidationVersion
                    : 0);

            if (isOptimized &&
                !LiveWallpaperFrameAnalysis.HasCurrentProfile)
            {
                ScheduleFrameAnalysis(inspection.Duration);
            }

            if (!isOptimized)
            {
                CancelFrameAnalysis();
                LiveWallpaperFrameAnalysis.Delete();

                if (Mode == LiveWallpaperMode.Mp4)
                {
                    Preferences.Default.Set(
                        EnabledPreferenceKey,
                        false);
                }
            }

            AppThemeManager.RefreshVisualResources();
            SettingsChanged?.Invoke(null, EventArgs.Empty);
            return isOptimized;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            Preferences.Default.Set(
                HardwareH264ValidatedPreferenceKey,
                false);
            Preferences.Default.Set(
                ValidationVersionPreferenceKey,
                0);
            CancelFrameAnalysis();
            LiveWallpaperFrameAnalysis.Delete();

            if (Mode == LiveWallpaperMode.Mp4)
            {
                Preferences.Default.Set(
                    EnabledPreferenceKey,
                    false);
            }

            AppThemeManager.RefreshVisualResources();
            SettingsChanged?.Invoke(null, EventArgs.Empty);
            return false;
        }
        finally
        {
            ValidationGate.Release();
        }
    }

    public static void SetMode(
        LiveWallpaperMode mode)
    {
        if (Mode == mode &&
            Preferences.Default.Get(
                ModePreferenceKey,
                -1) == (int)mode)
        {
            return;
        }

#if WINDOWS
        // Both directions rebuild native presentation state: MP4 -> static/math
        // tears a MediaElement down, while static/math -> MP4 creates one. Gate
        // resource propagation in both cases.
        AppThemeManager.NotifyLiveWallpaperNativeTransition();
#endif

        Preferences.Default.Set(
            ModePreferenceKey,
            (int)mode);

        if (mode != LiveWallpaperMode.Mp4)
        {
            CancelFrameAnalysis();
            _pendingFrameAnalysisDuration = TimeSpan.Zero;
        }

        // Do not silently switch to a non-working MP4 background. The user can
        // still choose MP4 mode first, then import a valid clip.
        if (mode == LiveWallpaperMode.Mp4 &&
            (!HasWallpaper || !IsHardwareH264Validated))
        {
            Preferences.Default.Set(
                EnabledPreferenceKey,
                false);
        }

        AppThemeManager.PrepareLiveWallpaperAdaptiveContrastForCurrentState();

        // Let active wallpaper hosts start their teardown/switch before glass
        // resources are reconciled. AppThemeManager also gates the actual WinUI
        // ResourceDictionary mutation until native surfaces are safe.
        SettingsChanged?.Invoke(null, EventArgs.Empty);
        AppThemeManager.RefreshVisualResources();
    }

    public static void SetEnabled(bool enabled)
    {
        bool canEnable =
            Mode == LiveWallpaperMode.MathAnimation ||
            (Mode == LiveWallpaperMode.Mp4 &&
             HasWallpaper &&
             IsHardwareH264Validated);

        bool normalized =
            enabled && canEnable;

        if (Preferences.Default.Get(
                EnabledPreferenceKey,
                false) == normalized)
        {
            return;
        }

#if WINDOWS
        // Enabling is just as much a native transition as disabling: a new
        // MediaElement/WinUI composition surface is created immediately after
        // SettingsChanged. Gate DynamicResource propagation for both directions.
        AppThemeManager.NotifyLiveWallpaperNativeTransition();
#endif

        Preferences.Default.Set(
            EnabledPreferenceKey,
            normalized);

        // Resolve the target polarity *before* active views react to
        // SettingsChanged. OFF follows the static app theme; MP4 bootstraps its
        // first-frame polarity from the persisted luminance profile.
        AppThemeManager.PrepareLiveWallpaperAdaptiveContrastForCurrentState();

        if (!normalized)
        {
            CancelFrameAnalysis();
            _pendingFrameAnalysisDuration = TimeSpan.Zero;
        }

        // Signal hosts first so MediaElement/GraphicsView teardown begins before
        // opaque Light/Dark resources are restored.
        SettingsChanged?.Invoke(
            null,
            EventArgs.Empty);

        AppThemeManager.RefreshVisualResources();
    }

    public static async Task ImportMp4Async(
        FileResult selectedFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedFile);

        if (!string.Equals(
                Path.GetExtension(selectedFile.FileName),
                ".mp4",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Only MP4 video files are supported.");
        }

        string? folder =
            Path.GetDirectoryName(WallpaperPath);

        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new InvalidOperationException(
                "Unable to resolve the wallpaper storage folder.");
        }

        Directory.CreateDirectory(folder);

        string temporaryPath =
            Path.Combine(
                folder,
                $"live_wallpaper_{Guid.NewGuid():N}.mp4");

        try
        {
            // Stop only stale analysis from an older import. The currently
            // working wallpaper/profile remains intact until the new file has
            // passed H.264, duration and hardware-decoder validation.
            CancelFrameAnalysis();

            Task<LiveWallpaperVideoInspection>? directInspection = null;
#if WINDOWS
            // On WinUI FilePicker exposes the real source path. Start the cheap
            // metadata/codec inspection in parallel with the OS file copy so
            // valid local clips become ready sooner.
            string? directSourcePath = selectedFile.FullPath;
            if (!string.IsNullOrWhiteSpace(directSourcePath) &&
                File.Exists(directSourcePath))
            {
                directInspection =
                    LiveWallpaperVideoInspector.InspectAsync(
                        directSourcePath,
                        cancellationToken);
            }
#endif

            await CopySelectedFileAsync(
                selectedFile,
                temporaryPath,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            LiveWallpaperVideoInspection inspection =
                directInspection is not null
                    ? await directInspection
                    : await LiveWallpaperVideoInspector.InspectAsync(
                        temporaryPath,
                        cancellationToken);

            if (!LiveWallpaperVideoInspector.IsDurationAllowed(
                    inspection.Duration))
            {
                throw new LiveWallpaperVideoValidationException(
                    LiveWallpaperVideoValidationError.DurationTooLong);
            }

            if (!LiveWallpaperVideoInspector.IsResolutionAllowed(inspection))
            {
                throw new LiveWallpaperVideoValidationException(
                    LiveWallpaperVideoValidationError.ResolutionTooHigh);
            }

            if (!inspection.IsH264)
            {
                throw new LiveWallpaperVideoValidationException(
                    LiveWallpaperVideoValidationError.NotH264);
            }

            if (!inspection.CanUseHardwarePreferredH264Path)
            {
                throw new LiveWallpaperVideoValidationException(
                    LiveWallpaperVideoValidationError.HardwareH264DecoderUnavailable);
            }

            // Acceptance happens here. Do NOT block it on thumbnail/frame
            // analysis: that used to make a valid file appear unresponsive for
            // several seconds and temporarily retained native decoder buffers.
#if WINDOWS
            AppThemeManager.NotifyLiveWallpaperNativeTransition();
#endif
            File.Move(
                temporaryPath,
                WallpaperPath,
                overwrite: true);

            Preferences.Default.Set(
                FileNamePreferenceKey,
                selectedFile.FileName);
            Preferences.Default.Set(
                HardwareH264ValidatedPreferenceKey,
                true);
            Preferences.Default.Set(
                ValidationVersionPreferenceKey,
                CurrentValidationVersion);
            Preferences.Default.Set(
                ModePreferenceKey,
                (int)LiveWallpaperMode.Mp4);
            Preferences.Default.Set(
                EnabledPreferenceKey,
                true);

            // Never let the old video's luminance profile affect the newly
            // accepted wallpaper while the replacement profile is built.
            LiveWallpaperFrameAnalysis.Delete();

            AppThemeManager.PrepareLiveWallpaperAdaptiveContrastForCurrentState();

            // Notify hosts first so the newly accepted player begins native
            // setup; the gated resource refresh follows after that transition.
            SettingsChanged?.Invoke(
                null,
                EventArgs.Empty);
            AppThemeManager.RefreshVisualResources();

            // Optional luminance analysis is started only after the transition
            // target has been prepared. If playback wins the race, the manager
            // defers analysis rather than opening a second decoder.
            ScheduleFrameAnalysis(inspection.Duration);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Best-effort cleanup only. A failed temp cleanup must not
                    // invalidate a successfully imported wallpaper.
                }
            }
        }
    }

    private static async Task CopySelectedFileAsync(
        FileResult selectedFile,
        string destinationPath,
        CancellationToken cancellationToken)
    {
#if WINDOWS
        // WinUI FilePicker normally returns a real filesystem path. Let the OS
        // perform the copy in a worker thread; this is faster and avoids an
        // extra managed copy buffer for large clips.
        string? fullPath = selectedFile.FullPath;
        if (!string.IsNullOrWhiteSpace(fullPath) &&
            File.Exists(fullPath))
        {
            await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Copy(
                        fullPath,
                        destinationPath,
                        overwrite: true);
                    cancellationToken.ThrowIfCancellationRequested();
                },
                cancellationToken);
            return;
        }
#endif

        await using Stream source =
            await selectedFile.OpenReadAsync();

        await using var destination =
            new FileStream(
                destinationPath,
                new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = 64 * 1024,
                    Options =
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan
                });

        // 256 KiB is large enough for good sequential throughput without
        // creating a multi-megabyte transient allocation on mobile.
        await source.CopyToAsync(
            destination,
            256 * 1024,
            cancellationToken);

        await destination.FlushAsync(cancellationToken);
    }

    private static void ScheduleFrameAnalysis(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || !HasWallpaper)
        {
            return;
        }

        lock (FrameAnalysisLock)
        {
            _pendingFrameAnalysisDuration = duration;

            // Never open a second decoder while MediaElement/ExoPlayer is
            // already rendering the live wallpaper. Adaptive contrast metadata
            // is optional; it can be generated the next time playback stops.
            if (_isMp4PlaybackActive)
            {
                return;
            }
        }

        CancellationTokenSource cancellation = new();
        int generation;

        CancellationTokenSource? previous;

        lock (FrameAnalysisLock)
        {
            previous = _frameAnalysisCancellation;
            _frameAnalysisCancellation = cancellation;
            generation = ++_frameAnalysisGeneration;
            _isFrameAnalysisRunning = true;
        }

        // Let the superseded task own/dispose its CTS in finally; disposing it
        // here can race with native frame extraction still observing Token.
        try
        {
            previous?.Cancel();
        }
        catch
        {
        }

        // Tell Settings that the file is already accepted but contrast metadata
        // is still being optimized in the background.
        SettingsChanged?.Invoke(null, EventArgs.Empty);

        _ = RunFrameAnalysisAsync(
            generation,
            duration,
            cancellation);
    }

    private static async Task RunFrameAnalysisAsync(
        int generation,
        TimeSpan duration,
        CancellationTokenSource cancellation)
    {
        try
        {
            // Let the picker close and the accepted filename/playback render
            // before opening the optional thumbnail decoder pipeline.
            await Task.Delay(
                    TimeSpan.FromMilliseconds(900),
                    cancellation.Token)
                .ConfigureAwait(false);

            lock (FrameAnalysisLock)
            {
                if (_isMp4PlaybackActive)
                {
                    return;
                }
            }

            LiveWallpaperFrameProfile? profile =
                await LiveWallpaperFrameAnalysis.AnalyzeAsync(
                        WallpaperPath,
                        duration,
                        cancellation.Token)
                    .ConfigureAwait(false);

            lock (FrameAnalysisLock)
            {
                if (generation != _frameAnalysisGeneration ||
                    cancellation.IsCancellationRequested)
                {
                    return;
                }
            }

            await LiveWallpaperFrameAnalysis.SaveAsync(
                    profile,
                    cancellation.Token)
                .ConfigureAwait(false);

            lock (FrameAnalysisLock)
            {
                if (generation == _frameAnalysisGeneration)
                {
                    _pendingFrameAnalysisDuration = TimeSpan.Zero;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Adaptive contrast is optional. Persist an empty current profile
            // so a decoder failure is not retried on every page load.
            try
            {
                lock (FrameAnalysisLock)
                {
                    if (generation != _frameAnalysisGeneration ||
                        cancellation.IsCancellationRequested)
                    {
                        return;
                    }
                }

                await LiveWallpaperFrameAnalysis.SaveAsync(
                    null,
                    CancellationToken.None);
            }
            catch
            {
                LiveWallpaperFrameAnalysis.InvalidateCache();
            }
        }
        finally
        {
            bool notify = false;

            lock (FrameAnalysisLock)
            {
                if (generation == _frameAnalysisGeneration)
                {
                    _isFrameAnalysisRunning = false;

                    if (ReferenceEquals(
                            _frameAnalysisCancellation,
                            cancellation))
                    {
                        _frameAnalysisCancellation = null;
                    }

                    notify = true;
                }
            }

            cancellation.Dispose();

            if (notify)
            {
                SettingsChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static void NotifyMp4PlaybackState(bool isActive)
    {
        TimeSpan deferredDuration = TimeSpan.Zero;
        CancellationTokenSource? cancellation = null;

        lock (FrameAnalysisLock)
        {
            if (_isMp4PlaybackActive == isActive)
            {
                return;
            }

            _isMp4PlaybackActive = isActive;

            if (isActive)
            {
                cancellation = _frameAnalysisCancellation;
                if (cancellation is not null)
                {
                    _frameAnalysisGeneration++;
                    _frameAnalysisCancellation = null;
                    _isFrameAnalysisRunning = false;
                }
            }
            else if (!_isHostSuspended &&
                     _pendingFrameAnalysisDuration > TimeSpan.Zero &&
                     !LiveWallpaperFrameAnalysis.HasCurrentProfile &&
                     HasWallpaper)
            {
                deferredDuration = _pendingFrameAnalysisDuration;
            }
        }

        try
        {
            cancellation?.Cancel();
        }
        catch
        {
        }

        if (!isActive && deferredDuration > TimeSpan.Zero)
        {
            ScheduleFrameAnalysis(deferredDuration);
        }
    }

    private static void CancelFrameAnalysis()
    {
        CancellationTokenSource? cancellation = null;
        bool notify = false;

        lock (FrameAnalysisLock)
        {
            _frameAnalysisGeneration++;
            cancellation = _frameAnalysisCancellation;
            _frameAnalysisCancellation = null;

            if (_isFrameAnalysisRunning)
            {
                _isFrameAnalysisRunning = false;
                notify = true;
            }
        }

        if (cancellation is not null)
        {
            try
            {
                cancellation.Cancel();
            }
            catch
            {
            }
        }

        if (notify)
        {
            SettingsChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void RemoveWallpaper()
    {
#if WINDOWS
        if (IsEnabled)
        {
            AppThemeManager.NotifyLiveWallpaperNativeTransition();
        }
#endif

        Preferences.Default.Remove(
            EnabledPreferenceKey);
        Preferences.Default.Remove(
            FileNamePreferenceKey);
        Preferences.Default.Remove(
            HardwareH264ValidatedPreferenceKey);
        Preferences.Default.Remove(
            ValidationVersionPreferenceKey);
        Preferences.Default.Set(
            ModePreferenceKey,
            (int)LiveWallpaperMode.MathAnimation);

        CancelFrameAnalysis();
        LiveWallpaperFrameAnalysis.Delete();
        AppThemeManager.PrepareLiveWallpaperAdaptiveContrastForCurrentState();

        // First notify active views so MediaElement releases its source/file
        // handle. This matters on Windows, where MediaPlayer can keep the MP4
        // locked until Source is cleared. Resource restoration is queued only
        // after teardown has begun.
        SettingsChanged?.Invoke(
            null,
            EventArgs.Empty);
        AppThemeManager.RefreshVisualResources();

        if (File.Exists(WallpaperPath))
        {
            try
            {
                File.Delete(WallpaperPath);
            }
            catch
            {
                // The setting is already disabled. A leftover file can be
                // replaced by the next import even if this best-effort cleanup
                // could not complete immediately.
            }
        }

        SettingsChanged?.Invoke(
            null,
            EventArgs.Empty);
    }

    public static void ResetToDefault()
    {
        Preferences.Default.Remove(
            ModePreferenceKey);
        RemoveWallpaper();
        Preferences.Default.Remove(
            ModePreferenceKey);
    }
}
