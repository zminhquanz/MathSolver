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

    private const int CurrentValidationVersion = 2;

    private static readonly SemaphoreSlim ValidationGate =
        new(1, 1);

    private const string WallpaperFolderName =
        "Wallpapers";
    private const string WallpaperFileName =
        "live_wallpaper.mp4";

    public const double MaximumMp4DurationSeconds =
        LiveWallpaperVideoInspector.MaximumDurationSeconds;

    public static event EventHandler? SettingsChanged;

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

        if (IsHardwareH264Validated)
        {
            return true;
        }

        await ValidationGate.WaitAsync(cancellationToken);

        try
        {
            if (IsHardwareH264Validated)
            {
                return true;
            }

            LiveWallpaperVideoInspection inspection =
                await LiveWallpaperVideoInspector.InspectAsync(
                    WallpaperPath,
                    cancellationToken);

            bool isOptimized =
                inspection.IsH264 &&
                inspection.CanUseHardwarePreferredH264Path &&
                LiveWallpaperVideoInspector.IsDurationAllowed(
                    inspection.Duration);

            Preferences.Default.Set(
                HardwareH264ValidatedPreferenceKey,
                isOptimized);
            Preferences.Default.Set(
                ValidationVersionPreferenceKey,
                isOptimized
                    ? CurrentValidationVersion
                    : 0);

            if (!isOptimized &&
                Mode == LiveWallpaperMode.Mp4)
            {
                Preferences.Default.Set(
                    EnabledPreferenceKey,
                    false);
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

        Preferences.Default.Set(
            ModePreferenceKey,
            (int)mode);

        // Do not silently switch to a non-working MP4 background. The user can
        // still choose MP4 mode first, then import a valid clip.
        if (mode == LiveWallpaperMode.Mp4 &&
            (!HasWallpaper || !IsHardwareH264Validated))
        {
            Preferences.Default.Set(
                EnabledPreferenceKey,
                false);
        }

        AppThemeManager.RefreshVisualResources();
        SettingsChanged?.Invoke(null, EventArgs.Empty);
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

        Preferences.Default.Set(
            EnabledPreferenceKey,
            normalized);

        AppThemeManager.RefreshVisualResources();

        SettingsChanged?.Invoke(
            null,
            EventArgs.Empty);
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
            await using Stream source =
                await selectedFile.OpenReadAsync();

            await using (var destination =
                new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 128,
                    useAsync: true))
            {
                await source.CopyToAsync(
                    destination,
                    1024 * 128,
                    cancellationToken);

                await destination.FlushAsync(
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            LiveWallpaperVideoInspection inspection =
                await LiveWallpaperVideoInspector.InspectAsync(
                    temporaryPath,
                    cancellationToken);

            if (!LiveWallpaperVideoInspector.IsDurationAllowed(
                    inspection.Duration))
            {
                throw new LiveWallpaperVideoValidationException(
                    LiveWallpaperVideoValidationError.DurationTooLong);
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

            AppThemeManager.RefreshVisualResources();

            SettingsChanged?.Invoke(
                null,
                EventArgs.Empty);
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

    public static void RemoveWallpaper()
    {
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

        AppThemeManager.RefreshVisualResources();

        // First notify active views so MediaElement releases its source/file
        // handle. This matters on Windows, where MediaPlayer can keep the MP4
        // locked until Source is cleared.
        SettingsChanged?.Invoke(
            null,
            EventArgs.Empty);

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
