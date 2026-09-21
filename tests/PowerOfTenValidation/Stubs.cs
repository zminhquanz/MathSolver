namespace MathSolver.Services;
internal static class CalculationAccelerationManager
{
    public static bool UsePowerNttSse => false;
    public static bool AllowAvx512 => UsePowerNttAvx2 && System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
    public static bool UsePowerNttAvx2 => Environment.GetEnvironmentVariable("POWER_TEN_SCALAR") != "1" &&
        System.Runtime.Intrinsics.X86.Avx2.IsSupported;
}
