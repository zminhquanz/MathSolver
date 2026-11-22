#if WINDOWS && MATHSOLVER_WINDOWS_ARTWORK_PACK
using MathSolver.Services.Artwork;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace MathSolver.Platforms.Windows;

/// <summary>
/// Reads MAUI FileImageSource artwork from a single release pack without
/// extracting PNG or SVG files to disk. Native app and splash icons remain
/// owned by WinUI packaging.
/// </summary>
public sealed class PackedFileImageSourceService : ImageSourceService,
    IImageSourceService<FileImageSource>
{
    private static readonly ArtworkProvider Provider = new(
        Path.Combine(AppContext.BaseDirectory, ArtworkProvider.PackFileName));

    public override async Task<IImageSourceServiceResult<Microsoft.UI.Xaml.Media.ImageSource>?>
        GetImageSourceAsync(IImageSource imageSource, float scale = 1,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (imageSource is not FileImageSource { File: { Length: > 0 } filename })
            return null;

        if (filename == Path.GetFileName(filename) &&
            (filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
             filename.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)))
        {
            var entry = await Provider.GetAsync(filename, cancellationToken);
            if (entry is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var input = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(input.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(entry.Bytes);
                    await writer.StoreAsync();
                }
                input.Seek(0);
                if (entry.IsSvg)
                {
                    var svg = new SvgImageSource();
                    var status = await svg.SetSourceAsync(input);
                    if (status != SvgImageSourceLoadStatus.Success)
                        throw new InvalidDataException($"Unable to decode packed SVG {entry.Name}: {status}");
                    return new ImageSourceServiceResult(svg, true);
                }

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(input);
                return new ImageSourceServiceResult(bitmap, true);
            }
        }

        // MAUI still generates raster resources for SVG icons and platform art.
        var source = new BitmapImage(new Uri($"ms-appx:///{filename.TrimStart('/')}", UriKind.Absolute));
        return new ImageSourceServiceResult(source, true);
    }
}
#endif
