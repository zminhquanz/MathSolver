using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services;

public enum AppLanguage { Vietnamese, English }
public static class AppLanguageManager
{
    public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.English;
}
internal static class LocalizationService
{
    internal static string TranslateKey(string key) => key;
}
internal enum CalculationSimdMode { Portable, Scalar, Sse, AvxAvx2, Avx512, ArmNeon }

internal static class CalculationAccelerationManager
{
    internal static bool UsePowerExportSimd => false;
    internal static bool UsePowerNttAvx2 { get; set; } = Avx2.IsSupported;
    internal static bool UsePowerNttSse { get; set; }
    internal static bool UsePowerNttNeon => false;
    internal static bool UsePowerNttNeonForExponent(int exponent) => false;
    internal static bool AllowAvx512 => false;
    internal static bool IsAndroidNeonExecutionAllowed => false;
    internal static CalculationSimdMode EffectiveSimdMode => CalculationSimdMode.AvxAvx2;
}

internal sealed class NttPowerMemoryGuard
{
    internal static NttPowerMemoryGuard Shared { get; } = new();
    internal void EnsureAllowed(int exponent) { }
}
