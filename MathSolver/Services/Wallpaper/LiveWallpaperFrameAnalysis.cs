#if ANDROID
using Android.Media;
using Android.OS;
#endif

#if WINDOWS
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
#endif

using System.Text.Json;

namespace MathSolver.Services;

/// <summary>
/// A tiny precomputed brightness timeline for an MP4 wallpaper. The video is
/// sampled only while it is imported (or once after upgrading an older saved
/// wallpaper). Runtime playback merely looks up one byte based on the current
/// MediaElement.Position, so adaptive contrast does not create a second decoder
/// workload while the wallpaper is playing.
/// </summary>
public sealed record LiveWallpaperFrameProfile(
    int Version,
    double DurationSeconds,
    double SampleIntervalSeconds,
    byte[] LuminanceSamples)
{
    public double GetLuminance(TimeSpan position)
    {
        if (LuminanceSamples.Length == 0)
        {
            return 0.5d;
        }

        if (LuminanceSamples.Length == 1 ||
            SampleIntervalSeconds <= 0d)
        {
            return LuminanceSamples[0] / 255d;
        }

        double seconds = Math.Max(0d, position.TotalSeconds);

        if (DurationSeconds > 0d)
        {
            seconds %= DurationSeconds;
        }

        int index = (int)Math.Round(
            seconds / SampleIntervalSeconds,
            MidpointRounding.AwayFromZero);

        index = Math.Clamp(
            index,
            0,
            LuminanceSamples.Length - 1);

        return LuminanceSamples[index] / 255d;
    }
}

public static class LiveWallpaperFrameAnalysis
{
    private const int CurrentProfileVersion = 1;
    private const double MinimumSampleIntervalSeconds = 1.0d;
    // Adaptive text only needs coarse scene brightness, not video-rate data.
    // Keeping this small is important on mobile because thumbnail extraction
    // opens a second native decode path during one-time profile generation.
    private const int MaximumSampleCount = 16;
    private const int SampleWidth = 24;
    private const int SampleHeight = 14;

    private static readonly object CacheLock = new();
    private static LiveWallpaperFrameProfile? _cachedProfile;
    private static bool _cacheLoaded;

    public static string ProfilePath =>
        Path.Combine(
            FileSystem.AppDataDirectory,
            "Wallpapers",
            "live_wallpaper_luminance.json");

    public static bool HasCurrentProfile
    {
        get
        {
            LiveWallpaperFrameProfile? profile = TryLoad();
            return profile?.Version == CurrentProfileVersion;
        }
    }

    public static LiveWallpaperFrameProfile? TryLoad()
    {
        lock (CacheLock)
        {
            if (_cacheLoaded)
            {
                return _cachedProfile;
            }

            _cacheLoaded = true;

            if (!File.Exists(ProfilePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(ProfilePath);
                LiveWallpaperFrameProfile? profile =
                    JsonSerializer.Deserialize<LiveWallpaperFrameProfile>(json);

                if (profile is null ||
                    profile.Version != CurrentProfileVersion)
                {
                    _cachedProfile = null;
                    return null;
                }

                _cachedProfile = profile;
                return profile;
            }
            catch
            {
                _cachedProfile = null;
                return null;
            }
        }
    }

    public static async Task<LiveWallpaperFrameProfile?> AnalyzeAsync(
        string path,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (duration <= TimeSpan.Zero)
        {
            return null;
        }

#if WINDOWS
        LiveWallpaperFrameProfile? profile =
            await AnalyzeWindowsAsync(
                path,
                duration,
                cancellationToken);

        // MediaComposition/MediaClip are WinRT objects whose native decoder
        // surfaces can otherwise remain accounted to the process until a much
        // later GC. The analysis is rare (once per imported wallpaper), so a
        // background cleanup after the method's native locals have gone out of
        // scope trades a tiny one-time cost for a much lower steady RAM floor.
        ScheduleWindowsNativeMediaCleanup();
        return profile;
#elif ANDROID
        return await Task.Run(
            () => AnalyzeAndroid(
                path,
                duration,
                cancellationToken),
            cancellationToken);
#else
        return null;
#endif
    }

    public static async Task SaveAsync(
        LiveWallpaperFrameProfile? profile,
        CancellationToken cancellationToken = default)
    {
        profile ??= new LiveWallpaperFrameProfile(
            CurrentProfileVersion,
            0d,
            0d,
            Array.Empty<byte>());

        string? folder = Path.GetDirectoryName(ProfilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string temporaryPath = ProfilePath + ".tmp";
        string json = JsonSerializer.Serialize(profile);

        await File.WriteAllTextAsync(
            temporaryPath,
            json,
            cancellationToken);

        File.Move(
            temporaryPath,
            ProfilePath,
            overwrite: true);

        lock (CacheLock)
        {
            _cachedProfile = profile;
            _cacheLoaded = true;
        }
    }

    public static void Delete()
    {
        lock (CacheLock)
        {
            _cachedProfile = null;
            _cacheLoaded = true;
        }

        try
        {
            if (File.Exists(ProfilePath))
            {
                File.Delete(ProfilePath);
            }
        }
        catch
        {
            // Best effort only. A stale profile is ignored/replaced on the next
            // successful import because SaveAsync overwrites this file.
        }
    }

    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _cachedProfile = null;
            _cacheLoaded = false;
        }
    }

    private static double GetSampleIntervalSeconds(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return MinimumSampleIntervalSeconds;
        }

        // Cap thumbnail decodes to 16 per video. Short clips still sample at
        // one-second resolution; a 120-second clip samples about every 7.5 s.
        // Text polarity uses hysteresis, so this is enough to follow major scene
        // changes without retaining dozens of native thumbnail decode surfaces.
        return Math.Max(
            MinimumSampleIntervalSeconds,
            duration.TotalSeconds / MaximumSampleCount);
    }

    private static int GetSampleCount(
        TimeSpan duration,
        double sampleIntervalSeconds)
    {
        if (duration <= TimeSpan.Zero)
        {
            return 1;
        }

        return Math.Clamp(
            (int)Math.Ceiling(
                duration.TotalSeconds /
                Math.Max(0.001d, sampleIntervalSeconds)),
            1,
            MaximumSampleCount);
    }

    private static TimeSpan GetSampleTime(
        int index,
        TimeSpan duration,
        double sampleIntervalSeconds)
    {
        double seconds = index * sampleIntervalSeconds;
        double maximum = Math.Max(
            0d,
            duration.TotalSeconds - 0.01d);

        return TimeSpan.FromSeconds(
            Math.Min(seconds, maximum));
    }

#if WINDOWS
    private static void ScheduleWindowsNativeMediaCleanup()
    {
        _ = Task.Run(
            () =>
            {
                try
                {
                    GC.Collect(
                        1,
                        GCCollectionMode.Optimized,
                        blocking: false,
                        compacting: false);
                }
                catch
                {
                }
            });
    }

    private static async Task<LiveWallpaperFrameProfile?> AnalyzeWindowsAsync(
        string path,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        StorageFile file =
            await StorageFile.GetFileFromPathAsync(path);

        MediaClip clip =
            await MediaClip.CreateFromFileAsync(file);

        var composition = new MediaComposition();
        composition.Clips.Add(clip);

        double sampleIntervalSeconds =
            GetSampleIntervalSeconds(duration);
        int count = GetSampleCount(
            duration,
            sampleIntervalSeconds);
        var samples = new byte[count];
        var valid = new bool[count];
        int completed = 0;

        try
        {
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var thumbnail =
                        await composition.GetThumbnailAsync(
                            GetSampleTime(
                                index,
                                duration,
                                sampleIntervalSeconds),
                            SampleWidth,
                            SampleHeight,
                            VideoFramePrecision.NearestFrame);

                    BitmapDecoder decoder =
                        await BitmapDecoder.CreateAsync(thumbnail);

                    var transform = new BitmapTransform
                    {
                        ScaledWidth = (uint)SampleWidth,
                        ScaledHeight = (uint)SampleHeight
                    };

                    PixelDataProvider pixelProvider =
                        await decoder.GetPixelDataAsync(
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Ignore,
                            transform,
                            ExifOrientationMode.IgnoreExifOrientation,
                            ColorManagementMode.DoNotColorManage);

                    byte[] pixels =
                        pixelProvider.DetachPixelData();

                    samples[index] =
                        CalculateBgraLuminance(pixels);
                    valid[index] = true;
                    completed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Keep analysis optional. Failed samples are filled from the
                    // nearest successfully decoded sample below.
                }
            }

            return completed == 0
                ? null
                : CreateProfile(
                    duration,
                    sampleIntervalSeconds,
                    samples,
                    valid);
        }
        finally
        {
            // Drop composition references promptly so Media Foundation can
            // return thumbnail decoder surfaces even when analysis is canceled.
            composition.Clips.Clear();
        }
    }

    private static byte CalculateBgraLuminance(byte[] pixels)
    {
        if (pixels.Length < 4)
        {
            return 128;
        }

        int width = SampleWidth;
        int height = Math.Max(1, pixels.Length / (width * 4));
        double weightedTotal = 0d;
        double totalWeight = 0d;

        for (int pixelIndex = 0;
             pixelIndex < width * height;
             pixelIndex++)
        {
            int index = pixelIndex * 4;
            if (index + 3 >= pixels.Length)
            {
                break;
            }

            int x = pixelIndex % width;
            int y = pixelIndex / width;
            double weight =
                GetCenterWeight(x, y, width, height);

            double blue = pixels[index] / 255d;
            double green = pixels[index + 1] / 255d;
            double red = pixels[index + 2] / 255d;
            double luminance =
                0.2126d * red +
                0.7152d * green +
                0.0722d * blue;

            weightedTotal += luminance * weight;
            totalWeight += weight;
        }

        return totalWeight <= 0d
            ? (byte)128
            : (byte)Math.Round(
                Math.Clamp(
                    weightedTotal / totalWeight,
                    0d,
                    1d) * 255d);
    }
#endif

#if ANDROID
    private static LiveWallpaperFrameProfile? AnalyzeAndroid(
        string path,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        using var retriever = new MediaMetadataRetriever();
        retriever.SetDataSource(path);

        double sampleIntervalSeconds =
            GetSampleIntervalSeconds(duration);
        int count = GetSampleCount(
            duration,
            sampleIntervalSeconds);
        var samples = new byte[count];
        var valid = new bool[count];
        int completed = 0;

        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Android.Graphics.Bitmap? bitmap = null;

            try
            {
                long timeMicroseconds =
                    (long)(GetSampleTime(
                        index,
                        duration,
                        sampleIntervalSeconds).TotalSeconds *
                           1_000_000d);

                bitmap =
                    Build.VERSION.SdkInt >= BuildVersionCodes.OMr1
                        ? retriever.GetScaledFrameAtTime(
                            timeMicroseconds,
                            Option.Closest,
                            SampleWidth,
                            SampleHeight)
                        : retriever.GetFrameAtTime(
                            timeMicroseconds,
                            Option.Closest);

                if (bitmap is null)
                {
                    continue;
                }

                samples[index] =
                    CalculateAndroidBitmapLuminance(bitmap);
                valid[index] = true;
                completed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Optional visual metadata must never make an otherwise valid
                // H.264 wallpaper fail to import.
            }
            finally
            {
                bitmap?.Dispose();
            }
        }

        return completed == 0
            ? null
            : CreateProfile(
                duration,
                sampleIntervalSeconds,
                samples,
                valid);
    }

    private static byte CalculateAndroidBitmapLuminance(
        Android.Graphics.Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        if (width <= 0 || height <= 0)
        {
            return 128;
        }

        int stepX = Math.Max(1, width / SampleWidth);
        int stepY = Math.Max(1, height / SampleHeight);
        double weightedTotal = 0d;
        double totalWeight = 0d;

        for (int y = stepY / 2;
             y < height;
             y += stepY)
        {
            for (int x = stepX / 2;
                 x < width;
                 x += stepX)
            {
                int argb = bitmap.GetPixel(x, y);
                double red = ((argb >> 16) & 0xFF) / 255d;
                double green = ((argb >> 8) & 0xFF) / 255d;
                double blue = (argb & 0xFF) / 255d;
                double luminance =
                    0.2126d * red +
                    0.7152d * green +
                    0.0722d * blue;
                double weight =
                    GetCenterWeight(x, y, width, height);

                weightedTotal += luminance * weight;
                totalWeight += weight;
            }
        }

        return totalWeight <= 0d
            ? (byte)128
            : (byte)Math.Round(
                Math.Clamp(
                    weightedTotal / totalWeight,
                    0d,
                    1d) * 255d);
    }
#endif

    private static double GetCenterWeight(
        int x,
        int y,
        int width,
        int height)
    {
        bool center =
            x >= width * 0.15d &&
            x <= width * 0.85d &&
            y >= height * 0.12d &&
            y <= height * 0.88d;

        // AspectFill commonly crops outer edges on a different window aspect
        // ratio. Give the center more weight so contrast follows what the user
        // is most likely to see behind the learning content.
        return center ? 1.6d : 0.65d;
    }

    private static LiveWallpaperFrameProfile CreateProfile(
        TimeSpan duration,
        double sampleIntervalSeconds,
        byte[] samples,
        bool[] valid)
    {
        // Fill isolated decode failures so runtime lookup never interprets a
        // missing sample as an intentionally black frame.
        int lastValid = -1;

        for (int index = 0; index < samples.Length; index++)
        {
            if (valid[index])
            {
                lastValid = index;
                continue;
            }

            int next = index + 1;
            while (next < samples.Length && !valid[next])
            {
                next++;
            }

            if (lastValid >= 0)
            {
                samples[index] = samples[lastValid];
            }
            else if (next < samples.Length)
            {
                samples[index] = samples[next];
            }
            else
            {
                samples[index] = 128;
            }
        }

        return new LiveWallpaperFrameProfile(
            CurrentProfileVersion,
            duration.TotalSeconds,
            sampleIntervalSeconds,
            samples);
    }
}
