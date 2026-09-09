using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services;

// Replace only the MAUI preferences dependency; all arithmetic, dispatch,
// diagnostics, memory pools and worker scheduling come from production source.
internal static class CalculationAccelerationManager
{
    public static bool Enabled { get; set; } = true;

    public static bool UsePowerNttAvx2 => Enabled && Avx2.IsSupported;
}
