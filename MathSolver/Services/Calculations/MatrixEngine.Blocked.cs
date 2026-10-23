using System.Diagnostics;
using System.Numerics;

namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    private sealed class TileWorkspace(int size, int group)
    {
        internal readonly long[] Left = new long[size * size * group], Right = new long[size * size * group];
        internal readonly long[] SourceRight = new long[size * size * group];
        internal readonly Signed192[] Accumulators = new Signed192[size * size * group * group];
        // Grouped work is multiplication only and never needs elementwise scratch.
        internal readonly Int128[] Wide = group == 1 ? new Int128[size * size] : [];
        internal readonly BigInteger[] Values = new BigInteger[size * size];
        internal long PackingTicks, KernelTicks, ConversionTicks, PackedA, PackedB, PairBatches;
    }

    private static MatrixCalculation? CalculateBlocked(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend, MatrixWorkPlan plan, MatrixExecutionOptions options,
        CancellationToken token, MatrixProgressReporter progress)
    {
        progress.Phase(MatrixCalculationStage.CheckingInputs);
        if (token.IsCancellationRequested) return null;
        var store = new MatrixBlockStore(a.Rows, b.Columns, plan.BlockSize, plan.FileBacked,
            options.TemporaryDirectory, plan.CacheBytes);
        bool published = false;
        Action<MatrixCalculationStage> activity = progress.Activity;
        try
        {
            int size = plan.BlockSize;
            int blockRows = (a.Rows + size - 1) / size, blockColumns = (b.Columns + size - 1) / size;
            int count = checked(blockRows * blockColumns);
            if (TryUniformResult(a, b, operation, backend, plan.WorkerLimit, token, progress,
                out BigInteger constant, out MatrixUniformScanStatistics scan))
            {
                for (int index = 0; index < count; index++)
                {
                    if (token.IsCancellationRequested) return null;
                    int r = index / blockColumns, c = index % blockColumns;
                    store.WriteConstantBlock(r, c, Math.Min(size, a.Rows - r * size),
                        Math.Min(size, b.Columns - c * size), constant, activity);
                    if (index == count - 1 || index % Math.Max(1, count / 100) == 0)
                        progress.Report(.2 + .8 * (index + 1d) / count);
                }
                progress.Phase(MatrixCalculationStage.Finalizing, .99);
                if (token.IsCancellationRequested) return null;
                store.Complete();
                var result = new MatrixCalculation(store, operation, MatrixBackend.Scalar, 0, 1, plan, uniform: true)
                    { UniformScan = scan, Execution = new(default, default, default, 0, 0, 0, store.Statistics) };
                published = true;
                return result;
            }
            progress.Phase(MatrixCalculationStage.Packing, .2);
            if (token.IsCancellationRequested) return null;
            int group = operation == MatrixOperation.Multiply && options.ReusePackedPanels ? 2 : 1;
            int groupColumns = (blockColumns + group - 1) / group;
            int groupCount = ((blockRows + group - 1) / group) * groupColumns;
            int next = -1, active = 0, peak = 0, completed = 0;
            long batches = 0, packing = 0, kernel = 0, conversion = 0, packedA = 0, packedB = 0, pairs = 0;
            object progressGate = new();
            void Worker(int _)
            {
                if (token.IsCancellationRequested) return;
                var workspace = new TileWorkspace(size, group);
                int current = Interlocked.Increment(ref active), observed;
                do { observed = Volatile.Read(ref peak); }
                while (current > observed && Interlocked.CompareExchange(ref peak, current, observed) != observed);
                long localBatches = 0;
                try
                {
                    int index;
                    while ((index = Interlocked.Increment(ref next)) < groupCount)
                    {
                        if (token.IsCancellationRequested) return;
                        int blockRow = index / groupColumns * group, blockColumn = index % groupColumns * group;
                        int row = blockRow * size, column = blockColumn * size;
                        int height = Math.Min(size * group, a.Rows - row), width = Math.Min(size * group, b.Columns - column);
                        if (operation == MatrixOperation.Multiply)
                            localBatches += MultiplyGroup(a, b, row, column, height, width, size, backend, options.MultiplyKernel, workspace, token, progress);
                        else
                        {
                            progress.Activity(MatrixCalculationStage.Multiplying);
                            long start = Stopwatch.GetTimestamp();
                            localBatches += ElementwiseTile(a, b, row, column, height, width, operation, backend, workspace, token);
                            workspace.KernelTicks += Stopwatch.GetTimestamp() - start;
                        }
                        if (token.IsCancellationRequested) return;
                        for (int r = 0; r < height; r += size)
                        for (int c = 0; c < width; c += size)
                        {
                            int h = Math.Min(size, height - r), w = Math.Min(size, width - c);
                            if (operation == MatrixOperation.Multiply)
                            {
                                long start = Stopwatch.GetTimestamp();
                                for (int i = 0; i < h; i++)
                                for (int j = 0; j < w; j++)
                                    workspace.Values[i * w + j] = workspace.Accumulators[(r + i) * width + c + j].ToBigInteger();
                                workspace.ConversionTicks += Stopwatch.GetTimestamp() - start;
                            }
                            if (token.IsCancellationRequested) return;
                            store.WriteBlock(blockRow + r / size, blockColumn + c / size, h, w,
                                workspace.Values.AsSpan(0, h * w), activity);
                            Array.Clear(workspace.Values, 0, h * w);
                            lock (progressGate)
                            {
                                completed++;
                                progress.Report(.2 + .8 * completed / count);
                            }
                        }
                    }
                }
                finally
                {
                    Interlocked.Add(ref batches, localBatches);
                    Interlocked.Add(ref packing, workspace.PackingTicks);
                    Interlocked.Add(ref kernel, workspace.KernelTicks);
                    Interlocked.Add(ref conversion, workspace.ConversionTicks);
                    Interlocked.Add(ref packedA, workspace.PackedA);
                    Interlocked.Add(ref packedB, workspace.PackedB);
                    Interlocked.Add(ref pairs, workspace.PairBatches);
                    Interlocked.Decrement(ref active);
                }
            }
            if (plan.WorkerLimit == 1) Worker(0);
            else Parallel.For(0, plan.WorkerLimit, new ParallelOptions { MaxDegreeOfParallelism = plan.WorkerLimit }, Worker);
            progress.Phase(MatrixCalculationStage.Finalizing, .99);
            if (token.IsCancellationRequested) return null;
            store.Complete();
            static TimeSpan Elapsed(long ticks) => TimeSpan.FromSeconds(ticks / (double)Stopwatch.Frequency);
            var completedResult = new MatrixCalculation(store, operation, backend, batches, peak, plan)
            {
                UniformScan = scan,
                Execution = new(Elapsed(packing), Elapsed(kernel), Elapsed(conversion), packedA, packedB, pairs, store.Statistics)
            };
            published = true;
            return completedResult;
        }
        finally { if (!published) store.Dispose(); }
    }

    private static long MultiplyGroup(IntegerMatrix a, IntegerMatrix b, int row, int column, int height, int width,
        int size, MatrixBackend backend, MatrixMultiplyKernel selection, TileWorkspace workspace, CancellationToken token,
        MatrixProgressReporter progress)
    {
        Array.Clear(workspace.Accumulators, 0, height * width);
        int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
        long batches = 0;
        for (int k = 0; k < a.Columns; k += size)
        {
            if (token.IsCancellationRequested) return 0;
            int depth = Math.Min(size, a.Columns - k);
            progress.Activity(MatrixCalculationStage.Packing);
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < height; i++)
            {
                if (token.IsCancellationRequested) return 0;
                a.ReadRow(row + i, k, workspace.Left.AsSpan(i * depth, depth));
            }
            for (int d = 0; d < depth; d++)
            {
                if (token.IsCancellationRequested) return 0;
                b.ReadRow(k + d, column, workspace.SourceRight.AsSpan(d * width, width));
            }
            // Transpose small cache-local squares: source reads are contiguous within each square.
            for (int d0 = 0; d0 < depth; d0 += 8)
            for (int j0 = 0; j0 < width; j0 += 8)
            for (int d = d0; d < Math.Min(depth, d0 + 8); d++)
            for (int j = j0; j < Math.Min(width, j0 + 8); j++)
                workspace.Right[j * depth + d] = workspace.SourceRight[d * width + j];
            workspace.PackingTicks += Stopwatch.GetTimestamp() - start;
            workspace.PackedA += (long)height * depth;
            workspace.PackedB += (long)width * depth;
            progress.Activity(MatrixCalculationStage.Multiplying);
            start = Stopwatch.GetTimestamp();
            // SSE2 pairing spills heavily on machines without AVX. Short panels
            // also fail to repay the extra accumulators, even on wider backends.
            bool paired = backend != MatrixBackend.Scalar && (selection == MatrixMultiplyKernel.PairedOutputs
                || selection == MatrixMultiplyKernel.Auto && (backend == MatrixBackend.Avx2 && depth >= 256
                    || backend == MatrixBackend.Avx512 && depth >= 256));
            for (int i = 0; i < height; i++)
            {
                if (token.IsCancellationRequested) return 0;
                int j = 0, startA = i * depth;
                if (paired)
                for (; j + 1 < width; j += 2)
                {
                    var p0 = new Signed192(); var p1 = new Signed192();
                    int b0 = j * depth, b1 = b0 + depth;
                    int processed = backend switch
                    {
                        MatrixBackend.Sse2 => DotPair128(workspace.Left, startA, workspace.Right, b0, b1, depth, ref p0, ref p1),
                        MatrixBackend.Avx2 => DotPair256(workspace.Left, startA, workspace.Right, b0, b1, depth, ref p0, ref p1),
                        _ => DotPair512(workspace.Left, startA, workspace.Right, b0, b1, depth, ref p0, ref p1)
                    };
                    batches += 2L * (processed / lanes);
                    workspace.PairBatches += processed / lanes;
                    for (; processed < depth; processed++)
                    {
                        p0.Add((Int128)workspace.Left[startA + processed] * workspace.Right[b0 + processed]);
                        p1.Add((Int128)workspace.Left[startA + processed] * workspace.Right[b1 + processed]);
                    }
                    workspace.Accumulators[i * width + j].Add(p0);
                    workspace.Accumulators[i * width + j + 1].Add(p1);
                }
                for (; j < width; j++)
                {
                    int startB = j * depth;
                    var partial = new Signed192();
                    int processed = backend switch
                    {
                        MatrixBackend.Sse2 => Dot128(workspace.Left, startA, workspace.Right, startB, depth, ref partial),
                        MatrixBackend.Avx2 => Dot256(workspace.Left, startA, workspace.Right, startB, depth, ref partial),
                        MatrixBackend.Avx512 => Dot512(workspace.Left, startA, workspace.Right, startB, depth, ref partial),
                        _ => 0
                    };
                    if (backend != MatrixBackend.Scalar) batches += processed / lanes;
                    for (; processed < depth; processed++)
                        partial.Add((Int128)workspace.Left[startA + processed] * workspace.Right[startB + processed]);
                    workspace.Accumulators[i * width + j].Add(partial);
                }
            }
            workspace.KernelTicks += Stopwatch.GetTimestamp() - start;
        }
        return batches;
    }

    private static long ElementwiseTile(IntegerMatrix a, IntegerMatrix b, int row, int column, int height, int width,
        MatrixOperation operation, MatrixBackend backend, TileWorkspace workspace, CancellationToken token)
    {
        for (int r = 0; r < height; r++)
        {
            if (token.IsCancellationRequested) return 0;
            a.ReadRow(row + r, column, workspace.Left.AsSpan(r * width, width));
            b.ReadRow(row + r, column, workspace.Right.AsSpan(r * width, width));
        }
        bool subtract = operation == MatrixOperation.Subtract;
        int count = height * width;
        int processed = backend switch
        {
            MatrixBackend.Sse2 => Elementwise128(workspace.Left, workspace.Right, workspace.Wide, 0, count, subtract),
            MatrixBackend.Avx2 => Elementwise256(workspace.Left, workspace.Right, workspace.Wide, 0, count, subtract),
            MatrixBackend.Avx512 => Elementwise512(workspace.Left, workspace.Right, workspace.Wide, 0, count, subtract),
            _ => 0
        };
        int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
        long batches = backend == MatrixBackend.Scalar ? 0 : processed / lanes;
        for (; processed < count; processed++)
            workspace.Wide[processed] = subtract ? (Int128)workspace.Left[processed] - workspace.Right[processed]
                : (Int128)workspace.Left[processed] + workspace.Right[processed];
        for (int i = 0; i < count; i++)
        {
            if ((i & 4095) == 0 && token.IsCancellationRequested) return 0;
            workspace.Values[i] = (BigInteger)workspace.Wide[i];
        }
        return batches;
    }

    private static bool TryUniformResult(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend, int workers, CancellationToken token, IProgress<double>? progress,
        out BigInteger value, out MatrixUniformScanStatistics statistics)
    {
        value = 0;
        statistics = default;
        if (TryScanUniform(a, backend, workers, token, progress is null ? null : new UniformScanProgress(progress, 0)) is not { } left)
            return false;
        statistics = left.Statistics;
        if (operation == MatrixOperation.Multiply && left.IsUniform && left.Value == 0)
        {
            progress?.Report(.2);
            return true;
        }
        if (TryScanUniform(b, backend, workers, token, progress is null ? null : new UniformScanProgress(progress, .1)) is not { } right)
            return false;
        statistics = new((MatrixBackend)Math.Max((int)left.Statistics.Backend, (int)right.Statistics.Backend),
            left.Statistics.VectorBatches + right.Statistics.VectorBatches,
            Math.Max(left.Statistics.WorkerCount, right.Statistics.WorkerCount),
            Math.Max(left.Statistics.WorkerLimit, right.Statistics.WorkerLimit),
            left.Statistics.CellsExamined + right.Statistics.CellsExamined);
        if (operation == MatrixOperation.Multiply && right.IsUniform && right.Value == 0) return true;
        if (!left.IsUniform || !right.IsUniform) return false;
        value = operation switch
        {
            MatrixOperation.Multiply => (BigInteger)left.Value * right.Value * a.Columns,
            MatrixOperation.Subtract => (BigInteger)left.Value - right.Value,
            _ => (BigInteger)left.Value + right.Value
        };
        return true;
    }
}
