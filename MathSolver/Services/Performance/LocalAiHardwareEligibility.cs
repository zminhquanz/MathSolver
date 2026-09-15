using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services;

/// <summary>Local AI requires Windows, AVX2 support and at least 12 GiB of physical RAM.</summary>
public static class LocalAiHardwareEligibility
{
    public const long MinimumPhysicalBytes = 12L * 1024 * 1024 * 1024;

    // Installed RAM does not change during a normal app session. An unknown
    // reading leaves AI hidden; Android never queries RAM to enable local AI.
    private static readonly Lazy<bool> Availability = new(() =>
        OperatingSystem.IsWindows() &&
        // Check runtime/CPU capability, independently of the app's SIMD preference.
        Avx2.IsSupported &&
        PhysicalMemoryInfo.ReadTotalBytes() >= MinimumPhysicalBytes);

    public static bool IsAvailable => Availability.Value;
}
