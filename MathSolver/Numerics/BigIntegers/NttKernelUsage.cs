using System.Runtime.CompilerServices;

namespace MathSolver.Numerics;

internal enum NttKernelPhase { Forward, Inverse, Pointwise, Crt, Carry, Twiddle }
internal enum NttKernelIsa { Scalar, Sse, Neon, Avx2, Avx512 }
/// <summary>Executed range/kernel batches, not operation counts or elapsed time.
/// Different kernel shapes can emit different numbers of batches for identical work.</summary>
internal sealed record NttKernelUsage(NttKernelPhase Phase, NttKernelIsa Isa, long Calls);

/// <summary>One counter array per executing thread; no atomics in arithmetic loops.</summary>
internal sealed class NttKernelCounter : IDisposable
{
    internal const int IsaCount = (int)NttKernelIsa.Avx512 + 1;
    private const int CounterCount = ((int)NttKernelPhase.Twiddle + 1) * IsaCount;
    private readonly ThreadLocal<long[]> _threads = new(() => new long[CounterCount], trackAllValues: true);
    private IReadOnlyList<NttKernelUsage>? _frozen;

    internal long[] CurrentThreadCounters => _threads.Value!;

    internal IReadOnlyList<NttKernelUsage> Snapshot()
    {
        if (_frozen is not null) return _frozen;
        var totals = new long[CounterCount];
        // Called only at a completed worker barrier or after the team joins.
        foreach (var values in _threads.Values)
            for (int i = 0; i < totals.Length; i++) totals[i] += values[i];
        return Enumerable.Range(0, totals.Length).Where(i => totals[i] > 0)
            .Select(i => new NttKernelUsage((NttKernelPhase)(i / IsaCount), (NttKernelIsa)(i % IsaCount), totals[i])).ToArray();
    }

    public void Dispose()
    {
        _frozen ??= Snapshot();
        _threads.Dispose();
    }
}

internal static class NttKernelExecution
{
    [ThreadStatic] private static long[]? _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Record(NttKernelPhase phase, NttKernelIsa isa)
    {
        if (_current is { } counts) counts[(int)phase * NttKernelCounter.IsaCount + (int)isa]++;
    }

    internal static Scope Bind(NttKernelCounter counter)
    {
        var scope = new Scope(_current);
        // Resolve ThreadLocal once per worker lifetime, not at each kernel batch.
        _current = counter.CurrentThreadCounters;
        return scope;
    }

    internal readonly struct Scope(long[]? previous) : IDisposable
    {
        public void Dispose() => _current = previous;
    }

    internal static IReadOnlyList<NttKernelUsage> Merge(IEnumerable<NttKernelUsage> rows) => rows
        .GroupBy(row => (row.Phase, row.Isa))
        .Select(group => new NttKernelUsage(group.Key.Phase, group.Key.Isa, group.Sum(row => row.Calls)))
        .OrderBy(row => row.Phase).ThenBy(row => row.Isa).ToArray();
}
