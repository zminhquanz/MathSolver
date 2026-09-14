using MathSolver.Services;
using System.Runtime.Intrinsics.X86;
static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
Check(CalculationAccelerationManager.SelectedSimdMode == CalculationSimdMode.Auto, "Default is Auto");
Check(CalculationAccelerationManager.EffectiveSimdMode != CalculationSimdMode.Auto, "Auto resolves to concrete backend");
foreach (var mode in CalculationAccelerationManager.AvailableSelectableModes)
{
    CalculationAccelerationManager.SetSelectedSimdMode(mode);
    Check(CalculationAccelerationManager.SelectedSimdMode == mode, "Selection " + mode);
    Check(Microsoft.Maui.Storage.Preferences.Default.Get("CalculationAcceleration.SimdMode", "Auto") == mode.ToString(), "Persist " + mode);
    Check(CalculationAccelerationManager.UseSingleThreadBigIntegerAvx2 == (CalculationAccelerationManager.AllowAvx && CalculationAccelerationManager.IsSingleThreadBigIntegerAccelerationAvailable), "BigInteger gate " + mode);
    if (mode == CalculationSimdMode.Sse) Check(!CalculationAccelerationManager.UsePowerNttAvx2 && !CalculationAccelerationManager.AllowAvx512 && !CalculationAccelerationManager.UsePowerExportSimd, "SSE disallows wider kernels");
    if (mode == CalculationSimdMode.AvxAvx2) Check(!CalculationAccelerationManager.AllowAvx512 && CalculationAccelerationManager.UsePowerNttAvx2 == Avx2.IsSupported, "AVX2 cap");
}
CalculationAccelerationManager.ResetToDefault();
Check(CalculationAccelerationManager.SelectedSimdMode == CalculationSimdMode.Auto, "Reset Auto");
CalculationAccelerationManager.SetUseSimd(false);
Check(!CalculationAccelerationManager.UseSingleThreadBigIntegerAvx2 && !CalculationAccelerationManager.AllowAvx && !CalculationAccelerationManager.AllowAvx512 && !CalculationAccelerationManager.UsePowerNttAvx2 && !CalculationAccelerationManager.UsePowerExportSimd, "Disabled gates all kernels");
CalculationAccelerationManager.SetUseSimd(true);
Check(CalculationAccelerationManager.UseSingleThreadBigIntegerAvx2 == (CalculationAccelerationManager.AllowAvx && CalculationAccelerationManager.IsSingleThreadBigIntegerAccelerationAvailable), "BigInteger AVX2 follows selected mode");
namespace MathSolver.Services { public enum AppLanguage { English } public static class AppLanguageManager { public static AppLanguage CurrentLanguage => AppLanguage.English; } }
namespace Microsoft.Maui.Storage { public class Preferences { public static Preferences Default { get; } = new(); private readonly Dictionary<string,object> values = new(); public T Get<T>(string key, T fallback) => values.TryGetValue(key,out var value) ? (T)value : fallback; public void Set<T>(string key,T value) => values[key] = value!; } }
