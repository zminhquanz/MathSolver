namespace MathSolver.Services;

/// <summary>Zoom is relative to Fit; offsets preserve the point under a finger or mouse.</summary>
internal readonly record struct DiagramPreviewViewport(double Width, double Height,
    double ImageWidth, double ImageHeight, double Scale, double ViewportWidth, double ViewportHeight)
{
    internal const double MaximumZoom = 4;
    internal static double ClampZoom(double zoom) => Math.Clamp(zoom, 1, MaximumZoom);

    internal static DiagramPreviewViewport Fit(double width, double height,
        double sourceWidth, double sourceHeight, double zoom)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        sourceWidth = Math.Max(1, sourceWidth);
        sourceHeight = Math.Max(1, sourceHeight);
        // Leave space for overlaid native scrollbars on both axes.
        double scale = Math.Min(Math.Max(1, width - 20) / sourceWidth,
            Math.Max(1, height - 20) / sourceHeight) * ClampZoom(zoom);
        double imageWidth = sourceWidth * scale, imageHeight = sourceHeight * scale;
        return new(Math.Max(width, imageWidth + 20), Math.Max(height, imageHeight + 20),
            imageWidth, imageHeight, scale, width, height);
    }

    internal static double AnchorOffset(double oldOffset, double anchor,
        double oldContent, double oldImage, double newContent, double newImage, double viewport, double? newAnchor = null)
    {
        double point = oldOffset + anchor - (oldContent - oldImage) / 2;
        double offset = point * newImage / Math.Max(1, oldImage) + (newContent - newImage) / 2 - (newAnchor ?? anchor);
        return Math.Clamp(offset, 0, Math.Max(0, newContent - viewport));
    }

    internal static (double Width, double Height, double Padding, bool Short) Panel(double width, double height)
    {
        double gap = width < 600 || height < 500 ? 6 : 20;
        return (Math.Max(1, Math.Min(1440, width - 2 * gap)),
            Math.Max(1, Math.Min(1100, height - 2 * gap)), height < 500 ? 8 : 16, height < 500);
    }
}
