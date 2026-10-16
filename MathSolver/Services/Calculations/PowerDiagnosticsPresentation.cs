using MathSolver.Numerics;

namespace MathSolver.Services.Core;

internal sealed record PowerTimingRow(string Key, TimeSpan Elapsed);
internal sealed record PowerStageRow(NttGlobalStageProfileEntry Entry, double GroupSharePercent);

/// <summary>Presentation projections only; no changes to arithmetic or profiler counters.</summary>
internal static class PowerDiagnosticsPresentation
{
    internal static IReadOnlyList<NttKernelUsage> KernelRows(ParallelPowerDiagnostics diagnostics) =>
        NttKernelExecution.Merge(diagnostics.KernelUsage.Where(row => row.Calls > 0));

    internal static IReadOnlyList<PowerTimingRow> PhaseRows(ParallelPowerDiagnostics diagnostics) =>
        Measured([
            new("BitReversal", diagnostics.BitReversal),
            new("Forward", diagnostics.ForwardTransform),
            new("Pointwise", diagnostics.Pointwise),
            new("Inverse", diagnostics.InverseTransform),
            new("Crt", diagnostics.Crt),
            new("Carry", diagnostics.Carry)
        ]);

    internal static IReadOnlyList<PowerTimingRow> Measured(IEnumerable<PowerTimingRow> rows) =>
        rows.Where(row => row.Elapsed > TimeSpan.Zero).ToArray();

    internal static IReadOnlyList<PowerTimingRow> ForwardRows(ParallelPowerDiagnostics diagnostics) => Measured([
        new("LocalL3", diagnostics.ForwardLocalL3), new("LocalL2", diagnostics.ForwardLocalL2),
        new("LocalL1", diagnostics.ForwardLocalL1), new("GlobalCached", diagnostics.ForwardGlobalCached),
        new("GlobalUncached", diagnostics.ForwardGlobalUncached),
        new("GlobalTwiddle", diagnostics.ForwardGlobalTwiddlePreparation), new("LocalTwiddle", diagnostics.ForwardLocalTwiddlePreparation)
    ]);

    internal static IReadOnlyList<PowerTimingRow> InverseRows(ParallelPowerDiagnostics diagnostics) => Measured([
        new("LocalL3", diagnostics.InverseLocalL3), new("LocalL2", diagnostics.InverseLocalL2),
        new("LocalL1", diagnostics.InverseLocalL1), new("GlobalCached", diagnostics.InverseGlobalCached),
        new("GlobalUncached", diagnostics.InverseGlobalUncached), new("FinalPrefix", diagnostics.InverseFinalPrefix)
    ]);

    internal static IReadOnlyList<PowerTimingRow> InverseL1Rows(ParallelPowerDiagnostics diagnostics) => Measured([
        new("Packed816", diagnostics.InverseL1Packed816), new("GenericPair", diagnostics.InverseL1GenericStagePair),
        new("Radix4", diagnostics.InverseL1Radix4Tail)
    ]);

    internal static IReadOnlyList<PowerStageRow> StageRows(
        IReadOnlyList<NttGlobalStageProfileEntry> profiles, int? limit = 5)
    {
        var measured = profiles.Where(p => p.Elapsed > TimeSpan.Zero).ToArray();
        var totals = measured.GroupBy(p => p.Kind)
            .ToDictionary(group => group.Key, group => group.Sum(p => p.Elapsed.TotalSeconds));
        var ordered = measured.OrderByDescending(p => p.Elapsed).ThenBy(p => p.Kind)
            .ThenBy(p => p.TransformLength).ThenBy(p => p.StageLength);
        return ordered.Take(limit is null ? int.MaxValue : Math.Max(0, limit.Value))
            .Select(p => new PowerStageRow(p, p.Elapsed.TotalSeconds / totals[p.Kind] * 100d)).ToArray();
    }
}
