using System.Numerics;

namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    private sealed class TileWorkspace(int size)
    {
        internal readonly long[] Left = new long[size * size], Right = new long[size * size];
        internal readonly long[] Row = new long[size];
        internal readonly Signed192[] Accumulators = new Signed192[size * size];
        internal readonly Int128[] Wide = new Int128[size * size];
        internal readonly BigInteger[] Values = new BigInteger[size * size];
    }

    private static MatrixCalculation? CalculateBlocked(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend, MatrixWorkPlan plan, MatrixExecutionOptions options,
        CancellationToken token, IProgress<double>? progress)
    {
        if (token.IsCancellationRequested) return null;
        var store = new MatrixBlockStore(a.Rows, b.Columns, plan.BlockSize, plan.FileBacked,
            options.TemporaryDirectory, plan.CacheBytes);
        bool published = false;
        try
        {
            int size = plan.BlockSize;
            int blockRows = (a.Rows + size - 1) / size;
            int blockColumns = (b.Columns + size - 1) / size;
            int count = checked(blockRows * blockColumns);
            if (TryUniformResult(a, b, operation, backend, plan.WorkerLimit, token, progress,
                out BigInteger constant, out MatrixUniformScanStatistics scan))
            {
                for (int index = 0; index < count; index++)
                {
                    if (token.IsCancellationRequested) return null;
                    int r = index / blockColumns, c = index % blockColumns;
                    store.WriteConstantBlock(r, c, Math.Min(size, a.Rows - r * size),
                        Math.Min(size, b.Columns - c * size), constant);
                    if (index == count - 1 || index % Math.Max(1, count / 100) == 0)
                        progress?.Report(.2 + .8 * (index + 1d) / count);
                }
                if (token.IsCancellationRequested) return null;
                store.Complete();
                var result = new MatrixCalculation(store, operation, MatrixBackend.Scalar, 0, 1, plan, uniform: true)
                    { UniformScan = scan };
                published = true;
                return result;
            }

            if (token.IsCancellationRequested) return null;
            int next = -1, active = 0, peak = 0, completed = 0, reported = 0;
            long batches = 0;
            object progressGate = new();
            void Worker(int _)
            {
                if (token.IsCancellationRequested) return;
                var workspace = new TileWorkspace(size);
                int current = Interlocked.Increment(ref active);
                int observed;
                do { observed = Volatile.Read(ref peak); }
                while (current > observed && Interlocked.CompareExchange(ref peak, current, observed) != observed);
                long localBatches = 0;
                try
                {
                    int index;
                    while ((index = Interlocked.Increment(ref next)) < count)
                    {
                        if (token.IsCancellationRequested) return;
                        int blockRow = index / blockColumns, blockColumn = index % blockColumns;
                        int row = blockRow * size, column = blockColumn * size;
                        int height = Math.Min(size, a.Rows - row), width = Math.Min(size, b.Columns - column);
                        long? tileBatches = operation == MatrixOperation.Multiply
                            ? MultiplyTile(a, b, row, column, height, width, size, backend, workspace, token)
                            : ElementwiseTile(a, b, row, column, height, width, operation, backend, workspace, token);
                        if (tileBatches is not { } completedBatches || token.IsCancellationRequested) return;
                        localBatches += completedBatches;
                        store.WriteBlock(blockRow, blockColumn, height, width, workspace.Values.AsSpan(0, height * width));
                        Array.Clear(workspace.Values, 0, height * width);
                        if (progress is not null)
                        {
                            lock (progressGate)
                            {
                                completed++;
                                if (completed == count || completed - reported >= Math.Max(1, count / 100))
                                {
                                    reported = completed;
                                    progress.Report(.2 + .8 * completed / count);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Interlocked.Add(ref batches, localBatches);
                    Interlocked.Decrement(ref active);
                }
            }
            if (plan.WorkerLimit == 1) Worker(0);
            else Parallel.For(0, plan.WorkerLimit,
                new ParallelOptions { MaxDegreeOfParallelism = plan.WorkerLimit }, Worker);
            if (token.IsCancellationRequested) return null;
            store.Complete();
            var completedResult = new MatrixCalculation(store, operation, backend, batches, peak, plan)
                { UniformScan = scan };
            published = true;
            return completedResult;
        }
        finally { if (!published) store.Dispose(); }
    }

    private static long? MultiplyTile(IntegerMatrix a, IntegerMatrix b, int row, int column, int height, int width,
        int size, MatrixBackend backend, TileWorkspace workspace, CancellationToken token)
    {
        Array.Clear(workspace.Accumulators, 0, height * width);
        int lanes = backend == MatrixBackend.Sse2 ? 2 : backend == MatrixBackend.Avx2 ? 4 : 8;
        long batches = 0;
        for (int k = 0; k < a.Columns; k += size)
        {
            if (token.IsCancellationRequested) return null;
            int depth = Math.Min(size, a.Columns - k);
            for (int i = 0; i < height; i++)
            {
                if (token.IsCancellationRequested) return null;
                a.CopyRowTo(row + i, k, workspace.Left.AsSpan(i * depth, depth));
            }
            // Transpose only the current panel, never the full B matrix.
            // Read each source row once so file-backed storage can copy a range
            // under one cache lookup instead of locking/seeking for each cell.
            for (int d = 0; d < depth; d++)
            {
                if (token.IsCancellationRequested) return null;
                b.CopyRowTo(k + d, column, workspace.Row.AsSpan(0, width));
                for (int j = 0; j < width; j++)
                    workspace.Right[j * depth + d] = workspace.Row[j];
            }
            for (int i = 0; i < height; i++)
            {
                if (token.IsCancellationRequested) return null;
                for (int j = 0; j < width; j++)
                {
                    int startA = i * depth, startB = j * depth;
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
                    // Dot kernels assign the partial sum, so add it to earlier inner blocks.
                    workspace.Accumulators[i * width + j].Add(partial);
                }
            }
        }
        for (int i = 0; i < height * width; i++)
        {
            if ((i & 4095) == 0 && token.IsCancellationRequested) return null;
            workspace.Values[i] = workspace.Accumulators[i].ToBigInteger();
        }
        return batches;
    }

    private static long? ElementwiseTile(IntegerMatrix a, IntegerMatrix b, int row, int column, int height, int width,
        MatrixOperation operation, MatrixBackend backend, TileWorkspace workspace, CancellationToken token)
    {
        for (int r = 0; r < height; r++)
        {
            if (token.IsCancellationRequested) return null;
            a.CopyRowTo(row + r, column, workspace.Left.AsSpan(r * width, width));
            b.CopyRowTo(row + r, column, workspace.Right.AsSpan(r * width, width));
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
            if ((i & 4095) == 0 && token.IsCancellationRequested) return null;
            workspace.Values[i] = (BigInteger)workspace.Wide[i];
        }
        return batches;
    }

    private static bool TryUniformResult(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend, int workers, CancellationToken token, IProgress<double>? progress,
        out BigInteger value, out MatrixUniformScanStatistics statistics)
    {
        value = 0;
        var leftScan = TryScanUniform(a, backend, workers, token, progress is null ? null : new UniformScanProgress(progress, 0));
        statistics = default;
        if (leftScan is not { } left) return false;
        statistics = left.Statistics;
        if (operation == MatrixOperation.Multiply && left.IsUniform && left.Value == 0)
        {
            progress?.Report(.2);
            return true;
        }
        var rightScan = TryScanUniform(b, backend, workers, token, progress is null ? null : new UniformScanProgress(progress, .1));
        if (rightScan is not { } right) return false;
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
