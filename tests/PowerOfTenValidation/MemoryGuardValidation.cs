using MathSolver.Numerics;
using MathSolver.Services;

internal static class MemoryGuardValidation
{
    internal static void Run()
    {
        int checks = 0;
        const long gib = 1024L * 1024 * 1024;
        foreach (long? ram in new long?[] { null, -1, 0, 4 * gib, 8 * gib, 12 * gib - 1,
                     12 * gib, 16 * gib, 32 * gib })
        {
            int reads = 0;
            var guard = new NttPowerMemoryGuard(() => { reads++; return ram; });
            foreach (int exponent in new[] { 0, 1, 9_999_999, 10_000_000 })
            {
                guard.EnsureAllowed(exponent);
                if (reads != 0) throw new Exception("Small powers must not require a RAM query.");
                checks++;
            }
            foreach (int exponent in new[] { 10_000_001, 40_000_000, 100_000_000 })
            {
                bool rejected = false;
                try { guard.EnsureAllowed(exponent); }
                catch (InvalidOperationException ex)
                {
                    rejected = true;
                    if (!ex.Message.Contains("12 GB") || !ex.Message.Contains("10.000.000")) throw;
                }
                if (rejected == (ram >= 12 * gib)) throw new Exception($"Wrong gate: {ram}, {exponent}");
                checks++;
            }
            foreach (int exponent in new[] { -1, 100_000_001, int.MaxValue })
            {
                try { guard.EnsureAllowed(exponent); throw new Exception("Missing range check."); }
                catch (ArgumentOutOfRangeException) { checks++; }
            }
        }

        // Check real entry points without allocating a large result.
        foreach (bool segmented in new[] { false, true })
        {
            try
            {
                if (segmented) ParallelBigUnsigned.PowMemoryBounded(3, 100_000_001, 1, null, default);
                else ParallelBigUnsigned.Pow(3, 100_000_001, 1, null, default);
                throw new Exception("NTT entry point did not enforce the maximum.");
            }
            catch (ArgumentOutOfRangeException) { checks++; }
        }

        long? detected = PhysicalMemoryInfo.ReadTotalBytes();
        Console.WriteLine($"PASS: {checks} RAM gate checks. Detected physical bytes: {detected?.ToString() ?? "unknown"}");
    }
}
