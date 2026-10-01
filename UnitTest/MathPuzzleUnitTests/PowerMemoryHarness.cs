using MathSolver.Numerics;
using System.Globalization;
using System.Numerics;

// This harness links the production PowerRootView.Memory.cs. Only the MAUI
// surface and numerical serializer are replaced; archive/gate/lifetime logic
// runs unchanged. A small threshold keeps correctness tests lightweight.
namespace MathSolver.Services
{
    public static class AppMemoryPolicy
    {
        public const long LargeResultThresholdBytes = 64;
        public static readonly TimeSpan InactiveResultDelay = TimeSpan.FromSeconds(60);
        public static bool IsInBackground { get; set; }
        public static Task CollectReleasedMemoryAsync(long _) => Task.CompletedTask;
    }
}

namespace MathSolver.Views
{
    internal static class FileSystem
    {
        internal static string AppDataDirectory { get; } =
            Path.Combine(Path.GetTempPath(), $"mathsolver-power-memory-{Guid.NewGuid():N}");
    }

    public partial class PowerRootView
    {
        private PowerCalculationState? _calculationState;
        private int _calculationVersion;
        private bool _isCalculating;
        private bool _isExporting;

        internal BigInteger ResidentValue => _calculationState?.Result ?? BigInteger.Zero;
        internal string? StoredPath => _calculationState?.CachedResultPath;
        internal string? Preview => _calculationState?.CompactResult;
        internal CancellationToken IdleToken => _inactiveResultCancellation?.Token ?? default;
        internal void SetBusy(bool calculating, bool exporting)
        {
            _isCalculating = calculating;
            _isExporting = exporting;
        }

        internal void SetResult(BigInteger value, Action<CancellationToken>? beforeWrite = null)
        {
            ReleaseStoredResult();
            _calculationVersion++;
            _calculationState = new(value, "Retained preview", null, null, null, false, beforeWrite);
        }

        internal void ClearResult() => ReleaseStoredResult();

        private static void WriteFullResultFile(string path, PowerCalculationState state,
            Action? progress, CancellationToken token, bool useSimd)
        {
            state.BeforeWrite?.Invoke(token);
            token.ThrowIfCancellationRequested();
            File.WriteAllText(path, state.Result.ToString(CultureInfo.InvariantCulture));
        }

        private sealed record PowerCalculationState(
            BigInteger Result, string CompactResult, ParallelBigUnsigned? ParallelMagnitude,
            LargeBinaryUnsigned? BinaryMagnitude, string? CachedResultPath,
            bool WasBinaryMagnitude, Action<CancellationToken>? BeforeWrite)
        {
            public bool HasBinaryMagnitude => WasBinaryMagnitude || BinaryMagnitude is not null;
        }
    }
}
