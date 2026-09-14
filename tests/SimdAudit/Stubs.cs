namespace MathSolver.Services;

// Only substitutes the MAUI preference service. Arithmetic is compiled from source.
internal static class CalculationAccelerationManager
{
    public static bool AllowAvx512 => UsePowerNttAvx2 && System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
    public static bool UsePowerNttAvx2 =>
        Environment.GetEnvironmentVariable("SIMD_AUDIT_MODE") != "scalar" &&
        System.Runtime.Intrinsics.X86.Avx2.IsSupported;
}

internal static class SimdAuditPolicy
{
    private static readonly HashSet<string> Disabled = new(
        (Environment.GetEnvironmentVariable("SIMD_AUDIT_DISABLE") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
    public static bool Allows(string kernel) => !Disabled.Contains(kernel);
    private static readonly string? Chain = Environment.GetEnvironmentVariable("SIMD_AUDIT_CHAIN");
    public static bool UseLeftToRight(bool production) => Chain switch
    {
        "ltr" => true,
        "rtl" => false,
        _ => production && Allows("LeftToRight")
    };
}
