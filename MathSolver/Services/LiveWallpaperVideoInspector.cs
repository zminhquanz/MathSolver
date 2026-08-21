#if ANDROID
using Android.Media;
using Android.OS;
#endif

#if WINDOWS
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
#endif

namespace MathSolver.Services;


public enum LiveWallpaperVideoValidationError
{
    NotH264,
    HardwareH264DecoderUnavailable
}

public sealed class LiveWallpaperVideoValidationException : Exception
{
    public LiveWallpaperVideoValidationException(
        LiveWallpaperVideoValidationError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public LiveWallpaperVideoValidationError Error { get; }
}

public readonly record struct LiveWallpaperVideoInspection(
    bool IsH264,
    bool CanUseHardwarePreferredH264Path,
    string CodecDisplayName,
    string? HardwareDecoderName);

/// <summary>
/// Validates the MP4 wallpaper before it replaces the currently working file.
/// H.264/AVC is intentionally required because it has the broadest dedicated
/// hardware-decoder coverage on Windows and Android.
/// </summary>
public static class LiveWallpaperVideoInspector
{
    public static async Task<LiveWallpaperVideoInspection> InspectAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

#if WINDOWS
        return await InspectWindowsAsync(path, cancellationToken);
#elif ANDROID
        return await Task.Run(
            () => InspectAndroid(path, cancellationToken),
            cancellationToken);
#else
        // The public release currently targets Windows/Android. Keep other
        // MAUI targets source-compatible and let their native player decide.
        return new(
            IsH264: true,
            CanUseHardwarePreferredH264Path: true,
            CodecDisplayName: "H.264 / AVC",
            HardwareDecoderName: null);
#endif
    }

#if WINDOWS
    private static async Task<LiveWallpaperVideoInspection> InspectWindowsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        StorageFile file =
            await StorageFile.GetFileFromPathAsync(path);

        cancellationToken.ThrowIfCancellationRequested();

        MediaClip clip =
            await MediaClip.CreateFromFileAsync(file);

        VideoEncodingProperties properties =
            clip.GetVideoEncodingProperties();

        string subtype =
            properties.Subtype ?? string.Empty;

        bool isH264 =
            string.Equals(
                subtype,
                MediaEncodingSubtypes.H264,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                subtype,
                MediaEncodingSubtypes.H264Es,
                StringComparison.OrdinalIgnoreCase) ||
            subtype.Contains(
                "H264",
                StringComparison.OrdinalIgnoreCase) ||
            subtype.Contains(
                "AVC",
                StringComparison.OrdinalIgnoreCase);

        var query = new CodecQuery();
        var decoders =
            await query.FindAllAsync(
                CodecKind.Video,
                CodecCategory.Decoder,
                MediaEncodingSubtypes.H264);

        cancellationToken.ThrowIfCancellationRequested();

        // Windows MediaPlayer is backed by Media Foundation. Media Foundation
        // enables DXVA/D3D video acceleration when the GPU/driver and the H.264
        // profile support it. CodecQuery does not expose a hardware/software
        // flag, so decoder presence is the strongest public WinRT preflight;
        // playback itself remains hardware-preferred by the OS.
        bool hasDecoder = decoders.Count > 0;
        string? decoderName =
            decoders.FirstOrDefault()?.DisplayName;

        return new(
            isH264,
            hasDecoder,
            "H.264 / AVC",
            decoderName);
    }
#endif

#if ANDROID
    private static LiveWallpaperVideoInspection InspectAndroid(
        string path,
        CancellationToken cancellationToken)
    {
        const string H264Mime = "video/avc";

        using var extractor = new MediaExtractor();
        extractor.SetDataSource(path);

        bool isH264 = false;
        bool foundVideoTrack = false;
        MediaFormat? h264Format = null;

        for (int index = 0; index < extractor.TrackCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MediaFormat? format =
                extractor.GetTrackFormat(index);

            string? mime =
                format?.GetString(MediaFormat.KeyMime);

            if (string.IsNullOrWhiteSpace(mime) ||
                !mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Validate the primary video track rather than accepting an MP4
            // merely because some secondary video track happens to be AVC.
            foundVideoTrack = true;
            isH264 = string.Equals(
                mime,
                H264Mime,
                StringComparison.OrdinalIgnoreCase);

            if (isH264)
            {
                h264Format = format;
            }

            break;
        }

        if (!foundVideoTrack || !isH264)
        {
            return new(
                IsH264: false,
                CanUseHardwarePreferredH264Path: false,
                CodecDisplayName: "H.264 / AVC",
                HardwareDecoderName: null);
        }

        string? hardwareDecoderName = null;
        var codecList =
            new MediaCodecList(MediaCodecListKind.AllCodecs);

        foreach (MediaCodecInfo codec in codecList.GetCodecInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (codec.IsEncoder)
            {
                continue;
            }

            bool supportsH264 =
                codec.GetSupportedTypes().Any(
                    type => string.Equals(
                        type,
                        H264Mime,
                        StringComparison.OrdinalIgnoreCase));

            if (!supportsH264)
            {
                continue;
            }

            if (h264Format is not null &&
                Build.VERSION.SdkInt > BuildVersionCodes.Lollipop)
            {
                try
                {
                    MediaCodecInfo.CodecCapabilities capabilities =
                        codec.GetCapabilitiesForType(H264Mime);

                    if (!capabilities.IsFormatSupported(h264Format))
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }
            }

            bool isHardwareAccelerated;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                isHardwareAccelerated =
                    codec.IsHardwareAccelerated &&
                    !codec.IsSoftwareOnly;
            }
            else
            {
                // Android exposes the explicit hardware/software flags only
                // from API 29. For older releases mirror ExoPlayer's practical
                // heuristic and exclude the common platform software codecs.
                string name = codec.Name ?? string.Empty;
                isHardwareAccelerated =
                    !name.StartsWith("OMX.google.", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("c2.android.", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("software", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("sw.", StringComparison.OrdinalIgnoreCase);
            }

            if (isHardwareAccelerated)
            {
                hardwareDecoderName = codec.Name;
                break;
            }
        }

        return new(
            IsH264: true,
            CanUseHardwarePreferredH264Path:
                !string.IsNullOrWhiteSpace(hardwareDecoderName),
            CodecDisplayName: "H.264 / AVC",
            HardwareDecoderName: hardwareDecoderName);
    }
#endif
}
