#if ANDROID
using System.Runtime.InteropServices;

namespace MathSolver.Platforms.Android;

/// <summary>
/// Android ARM64 fallback for benchmark-only SIMD paths.
///
/// .NET for Android runs on Mono. Depending on the execution engine, managed
/// System.Runtime.Intrinsics.Arm IsSupported values can be false even when the
/// Linux kernel exposes the corresponding ARM capability. This tiny native
/// library is used only by Hardware Information benchmarks so the app can
/// measure NEON/SVE on real ARM64 devices without changing production math
/// algorithms.
/// </summary>
internal static class AndroidArmSimdBenchmarkNative
{
    private const string LibraryName = "mathsolver_armbench";

    private static readonly Lazy<bool> LoadState =
        new(TryLoadLibrary);

    public static bool IsAvailable =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
        LoadState.Value;

    public static float RunNeonFloat(
        int iterations,
        float seed) =>
        NeonFloat(iterations, seed);

    public static double RunNeonDouble(
        int iterations,
        double seed) =>
        NeonDouble(iterations, seed);

    public static float RunSveFloat(
        int iterations,
        float seed) =>
        SveFloat(iterations, seed);

    public static double RunSveDouble(
        int iterations,
        double seed) =>
        SveDouble(iterations, seed);

    private static bool TryLoadLibrary()
    {
        // AndroidNativeLibrary thường được .NET for Android preload sẵn.
        // LoadLibrary ở đây chỉ là fallback; dù OEM/runtime báo thư viện đã
        // được load theo cách khác, vẫn thử entry point bằng P/Invoke.
        try
        {
            Java.Lang.JavaSystem.LoadLibrary(LibraryName);
        }
        catch
        {
            // Continue and let DllImport resolve the packaged .so.
        }

        try
        {
            return ArmBenchVersion() == 1;
        }
        catch
        {
            return false;
        }
    }

    [DllImport(LibraryName, EntryPoint = "ms_armbench_version")]
    private static extern int ArmBenchVersion();

    [DllImport(LibraryName, EntryPoint = "ms_neon_float")]
    private static extern float NeonFloat(int iterations, float seed);

    [DllImport(LibraryName, EntryPoint = "ms_neon_double")]
    private static extern double NeonDouble(int iterations, double seed);

    [DllImport(LibraryName, EntryPoint = "ms_sve_float")]
    private static extern float SveFloat(int iterations, float seed);

    [DllImport(LibraryName, EntryPoint = "ms_sve_double")]
    private static extern double SveDouble(int iterations, double seed);
}
#endif
