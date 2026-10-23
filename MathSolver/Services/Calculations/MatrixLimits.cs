namespace MathSolver.Services.Core;

public static class MatrixLimits
{
    public const int MaximumDimension = 16384;
    public const int MaximumMobileInputDimension = 8192;
    // The engine retains its desktop benchmark ceiling. Application input
    // paths must apply this smaller bound on mobile before staging any buffers.
    public static int GetInputMaximumDimension(bool desktopBenchmark) =>
        desktopBenchmark ? MaximumDimension : MaximumMobileInputDimension;
    public const int ParallelThresholdCells = 1024 * 1024;
    public const int MaximumClipboardCharacters = 32 * 1024 * 1024;
}
