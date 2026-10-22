using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services.Core;

/// <summary>Observed input verification, independent of matrix arithmetic telemetry.</summary>
public readonly record struct MatrixUniformScanStatistics(MatrixBackend Backend, long VectorBatches,
    int WorkerCount, int WorkerLimit, long CellsExamined);

internal readonly record struct MatrixUniformScanResult(bool IsUniform, long Value, MatrixUniformScanStatistics Statistics);

public static partial class MatrixEngine
{
    // Register-only comparisons over bounded chunks; never duplicate the source matrix.
    internal static MatrixUniformScanResult ScanUniform(long[] values, MatrixBackend maximumBackend,
        int maximumWorkers, CancellationToken token = default, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0) throw new ArgumentException("A uniform scan requires at least one value.", nameof(values));
        if (maximumWorkers < 1) throw new ArgumentOutOfRangeException(nameof(maximumWorkers));
        if (!Enum.IsDefined(maximumBackend)) throw new ArgumentOutOfRangeException(nameof(maximumBackend));
        token.ThrowIfCancellationRequested();
        MatrixBackend backend = maximumBackend == MatrixBackend.Scalar ? MatrixBackend.Scalar
            : (maximumBackend is MatrixBackend.Auto or MatrixBackend.Avx512) && Avx512F.IsSupported ? MatrixBackend.Avx512
            : (maximumBackend is MatrixBackend.Auto or MatrixBackend.Avx512 or MatrixBackend.Avx2) && Avx2.IsSupported ? MatrixBackend.Avx2
            : Sse2.IsSupported ? MatrixBackend.Sse2 : MatrixBackend.Scalar;
        const int chunkSize = 65536;
        int chunks = (values.Length + chunkSize - 1) / chunkSize;
        int workers = values.Length >= MatrixLimits.ParallelThresholdCells
            ? Math.Min(chunks, Math.Min(maximumWorkers, Environment.ProcessorCount)) : 1;
        long value = values[0], batches = 0, cells = 0;
        int next = -1, mismatch = 0, active = 0, peak = 0, completed = 0, reported = 0;
        object progressGate = new();
        progress?.Report(0);
        void Worker(int _)
        {
            int chunk = Interlocked.Increment(ref next);
            if (chunk >= chunks || Volatile.Read(ref mismatch) != 0) return;
            int current = Interlocked.Increment(ref active), observed;
            do { observed = Volatile.Read(ref peak); }
            while (current > observed && Interlocked.CompareExchange(ref peak, current, observed) != observed);
            long localBatches = 0, localCells = 0;
            try
            {
                do
                {
                    int end = Math.Min(values.Length, (chunk + 1) * chunkSize);
                    for (int start = chunk * chunkSize; start < end; start += 4096)
                    {
                        token.ThrowIfCancellationRequested();
                        if (Volatile.Read(ref mismatch) != 0) return;
                        if (!CompareUniformRange(values, start, Math.Min(end, start + 4096), value,
                            backend, ref localBatches, ref localCells))
                        {
                            Interlocked.Exchange(ref mismatch, 1);
                            return;
                        }
                    }
                    if (progress is not null)
                    {
                        lock (progressGate)
                        {
                            completed++;
                            if (completed == chunks || completed - reported >= Math.Max(1, chunks / 100))
                            {
                                reported = completed;
                                progress.Report((double)completed / chunks);
                            }
                        }
                    }
                } while (Volatile.Read(ref mismatch) == 0 && (chunk = Interlocked.Increment(ref next)) < chunks);
            }
            finally
            {
                Interlocked.Add(ref batches, localBatches);
                Interlocked.Add(ref cells, localCells);
                Interlocked.Decrement(ref active);
            }
        }
        if (workers == 1) Worker(0);
        else Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = token }, Worker);
        token.ThrowIfCancellationRequested();
        progress?.Report(1);
        token.ThrowIfCancellationRequested();
        return new(mismatch == 0, value, new(batches == 0 ? MatrixBackend.Scalar : backend, batches, peak, workers, cells));
    }

    private static bool CompareUniformRange(long[] values, int start, int end, long value, MatrixBackend backend,
        ref long batches, ref long cells)
    {
        int i = start;
        int lanes = backend == MatrixBackend.Avx512 ? 8 : backend == MatrixBackend.Avx2 ? 4 : 2;
        if (backend == MatrixBackend.Avx512)
        {
            var expected = Vector512.Create(value);
            for (; i <= end - 8; i += 8)
            {
                if (!Vector512.EqualsAll(Vector512.LoadUnsafe(ref values[i]), expected))
                {
                    batches += (i - start) / 8 + 1; cells += i - start + 8;
                    return false;
                }
            }
        }
        else if (backend == MatrixBackend.Avx2)
        {
            var expected = Vector256.Create(value);
            for (; i <= end - 4; i += 4)
            {
                if (Avx2.MoveMask(Avx2.CompareEqual(Vector256.LoadUnsafe(ref values[i]), expected).AsByte()) != -1)
                {
                    batches += (i - start) / 4 + 1; cells += i - start + 4;
                    return false;
                }
            }
        }
        else if (backend == MatrixBackend.Sse2)
        {
            var expected = Vector128.Create(value).AsInt32();
            for (; i <= end - 2; i += 2)
            {
                // SSE2 compares 32-bit halves; require every byte to match for exact Int64 equality.
                if (Sse2.MoveMask(Sse2.CompareEqual(Vector128.LoadUnsafe(ref values[i]).AsInt32(), expected).AsByte()) != 65535)
                {
                    batches += (i - start) / 2 + 1; cells += i - start + 2;
                    return false;
                }
            }
        }
        batches += (i - start) / lanes;
        cells += i - start;
        int scalarStart = i;
        for (; i < end; i++)
        {
            if (values[i] != value)
            {
                cells += i - scalarStart + 1;
                return false;
            }
        }
        cells += end - scalarStart;
        return true;
    }

    private sealed class UniformScanProgress(IProgress<double>? target, double offset) : IProgress<double>
    {
        public void Report(double value) => target?.Report(offset + .1 * value);
    }
}
