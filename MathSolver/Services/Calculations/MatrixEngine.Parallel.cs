namespace MathSolver.Services.Core;

public static partial class MatrixEngine
{
    private readonly record struct RowExecution(long Batches, int Workers);

    private static RowExecution ExecuteRows(int rows, int workers, Func<int, long> calculate,
        CancellationToken token, IProgress<double>? progress, double start, double end)
    {
        int active = 0, peak = 0, completed = 0, reported = 0;
        int reportEvery = Math.Max(1, rows / 100);
        object progressGate = new();
        long Run(int row)
        {
            if (token.IsCancellationRequested) return 0;
            int current = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref peak); }
            while (current > observed && Interlocked.CompareExchange(ref peak, current, observed) != observed);
            long batches;
            try { batches = calculate(row); }
            finally { Interlocked.Decrement(ref active); }
            if (progress is not null)
            {
                lock (progressGate)
                {
                    if (token.IsCancellationRequested) return batches;
                    completed++;
                    if (completed == rows || completed - reported >= reportEvery)
                    {
                        reported = completed;
                        progress.Report(start + (end - start) * completed / rows);
                    }
                }
            }
            return batches;
        }

        long total = 0;
        if (workers <= 1 || rows <= 1)
        {
            for (int row = 0; row < rows && !token.IsCancellationRequested; row++) total += Run(row);
        }
        else
        {
            // Passing the token to Parallel.For would throw on normal user stop.
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Min(rows, workers) };
            Parallel.For(0, rows, options, () => 0L, (row, state, local) =>
            {
                if (token.IsCancellationRequested) { state.Stop(); return local; }
                return local + Run(row);
            },
                local => Interlocked.Add(ref total, local));
        }
        return new(total, peak);
    }
}
