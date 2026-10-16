using MathSolver.Numerics;
using MathSolver.Services;
using System.Globalization;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckNttKernelExecution()
    {
        bool previousAvx2 = CalculationAccelerationManager.UsePowerNttAvx2;
        bool previousSse = CalculationAccelerationManager.UsePowerNttSse;
        bool previous512 = CalculationAccelerationManager.AllowAvx512;
        try
        {
            bool can512 = Avx512F.IsSupported && Vector512.IsHardwareAccelerated && Avx2.IsSupported;
            foreach (int mode in new[] { 0, 1, 2, 3 })
            {
                if (mode == 1 && !Avx2.IsSupported) continue;
                if (mode == 2 && !can512) { Console.WriteLine("  SKIP AVX-512: runtime does not support it."); continue; }
                if (mode == 3 && (!Sse2.IsSupported || !Sse41.IsSupported)) continue;
                CalculationAccelerationManager.UsePowerNttAvx2 = mode is 1 or 2;
                CalculationAccelerationManager.UsePowerNttSse = mode == 3;
                CalculationAccelerationManager.AllowAvx512 = mode == 2;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var result = ParallelBigUnsigned.Pow(123456789, 8192, 4, null, timeout.Token);
                Require(result.Magnitude.ToDecimalString() == BigInteger.Pow(123456789, 8192).ToString(CultureInfo.InvariantCulture),
                    $"NTT arithmetic changed in SIMD mode {mode}.");
                CheckUsage(result.Diagnostics, mode);
                var frozen = result.Diagnostics.KernelUsage.ToArray();
                var split = ParallelBigUnsigned.Pow(987654321, 8191, 4, null, timeout.Token);
                Require(split.Diagnostics.UsedExponentSplit, "Test must exercise branch telemetry aggregation.");
                Require(split.Magnitude.ToDecimalString() == BigInteger.Pow(987654321, 8191).ToString(CultureInfo.InvariantCulture),
                    $"Split exponent arithmetic changed in mode {mode}.");
                CheckUsage(split.Diagnostics, mode);
                Require(result.Diagnostics.KernelUsage.SequenceEqual(frozen), "A later calculation mutated a completed snapshot.");
                foreach (bool persistent in new[] { false, true })
                {
                    var binary = ParallelBigUnsigned.PowFiveAndShift(32768, 4, null, timeout.Token,
                        maximumTransformLength: 4096, persistentScheduling: persistent);
                    Require(binary.Value == BigInteger.Pow(10, 32768), $"Binary CRT/carry mismatch in mode {mode}, persistent={persistent}.");
                    CheckUsage(binary.Diagnostics!, mode);
                }
                Require(result.Diagnostics.Avx512Enabled == (mode == 2), "Run policy must be captured independently of executed kernels.");
            }
            CalculationAccelerationManager.UsePowerNttAvx2 = Avx2.IsSupported;
            CalculationAccelerationManager.UsePowerNttSse = false;
            CalculationAccelerationManager.AllowAvx512 = can512;
            var tiny = ParallelBigUnsigned.Pow(2, 0, 1, null, CancellationToken.None);
            Require(tiny.Diagnostics.KernelUsage.Count == 0, "Hardware capability must not create execution records for an empty calculation.");

            // Single-worker jobs and simultaneous jobs must retain separate counters.
            var jobs = new[] { 8192, 4097 }.Select(exponent => Task.Run(() =>
            {
                var job = ParallelBigUnsigned.Pow(123456789, exponent, 1, null, CancellationToken.None);
                Require(job.Magnitude.ToDecimalString() == BigInteger.Pow(123456789, exponent).ToString(CultureInfo.InvariantCulture),
                    "Concurrent single-worker NTT result mismatch.");
                CheckUsage(job.Diagnostics, can512 ? 2 : Avx2.IsSupported ? 1 : 0);
                return job.Diagnostics;
            })).ToArray();
            Task.WaitAll(jobs);
            Require(!ReferenceEquals(jobs[0].Result.KernelUsage, jobs[1].Result.KernelUsage), "Concurrent jobs shared a telemetry snapshot.");

            using var cancelled = new CancellationTokenSource();
            bool observedCancellation = false;
            try
            {
                ParallelBigUnsigned.Pow(123456789, 8192, 4,
                    (done, total) => { if (done >= total - 1) cancelled.Cancel(); }, cancelled.Token);
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { observedCancellation = true; }
            Require(observedCancellation, "Cancellation test must stop an active NTT job.");
            var afterCancellation = ParallelBigUnsigned.Pow(123456789, 8192, 3, null, CancellationToken.None);
            Require(afterCancellation.Magnitude.ToDecimalString() == BigInteger.Pow(123456789, 8192).ToString(CultureInfo.InvariantCulture),
                "Worker counters were not cleaned up after cancellation.");
            CheckUsage(afterCancellation.Diagnostics, can512 ? 2 : Avx2.IsSupported ? 1 : 0);

            if (can512)
            {
                // Larger, non-power-of-two exponent exercises cached/global stage shapes
                // and segmented CRT/carry with an uneven final coefficient range.
                var larger = ParallelBigUnsigned.PowFiveAndShift(524289, 3, null, CancellationToken.None,
                    maximumTransformLength: 1 << 16, persistentScheduling: true);
                Require(larger.Value == BigInteger.Pow(10, 524289), "Larger persistent AVX-512 CRT/carry result mismatch.");
                Require(larger.Diagnostics!.SegmentedNttPairCount > 0, "Larger test must exercise segmented multiplication.");
                CheckUsage(larger.Diagnostics!, 2);
            }

            // AVX-512 permission alone must not bypass the master NTT SIMD gate.
            CalculationAccelerationManager.UsePowerNttAvx2 = false;
            CalculationAccelerationManager.AllowAvx512 = true;
            var disabled = ParallelBigUnsigned.Pow(123456789, 8192, 3, null, CancellationToken.None);
            CheckUsage(disabled.Diagnostics, 0);
            Require(!disabled.Diagnostics.Avx512Enabled, "AVX-512 policy bypassed disabled NTT acceleration.");
        }
        finally
        {
            CalculationAccelerationManager.UsePowerNttAvx2 = previousAvx2;
            CalculationAccelerationManager.UsePowerNttSse = previousSse;
            CalculationAccelerationManager.AllowAvx512 = previous512;
        }

        static void CheckUsage(ParallelPowerDiagnostics diagnostics, int mode)
        {
            Require(diagnostics.NttMultiplicationCount > 0, "Test must execute actual NTT work.");
            foreach (var phase in new[] { NttKernelPhase.Forward, NttKernelPhase.Inverse, NttKernelPhase.Pointwise, NttKernelPhase.Crt, NttKernelPhase.Carry })
                Require(diagnostics.KernelUsage.Any(u => u.Phase == phase && u.Calls > 0), $"Missing execution telemetry for {phase}, mode={mode}.");
            Require(diagnostics.KernelUsage.All(u => u.Calls > 0), "Unused kernels must not appear as executed.");
            Require(!diagnostics.KernelUsage.Any(u => u.Isa == NttKernelIsa.Avx512) || mode == 2,
                "AVX-512 ran when disabled or unsupported.");
            if (mode == 2)
            {
                foreach (var phase in new[] { NttKernelPhase.Forward, NttKernelPhase.Inverse, NttKernelPhase.Pointwise })
                    Require(diagnostics.KernelUsage.Any(u => u.Phase == phase && u.Isa == NttKernelIsa.Avx512), $"AVX-512 not activated for {phase}.");
                if (Avx512DQ.IsSupported)
                    Require(diagnostics.KernelUsage.Any(u => u.Phase == NttKernelPhase.Crt && u.Isa == NttKernelIsa.Avx512), "AVX-512DQ CRT dispatch was not recorded.");
            }
            if (mode == 0) Require(diagnostics.KernelUsage.All(u => u.Isa == NttKernelIsa.Scalar), "Scalar mode reported SIMD execution.");
            if (mode == 3) Require(diagnostics.KernelUsage.Any(u => u.Isa == NttKernelIsa.Sse) &&
                diagnostics.KernelUsage.All(u => u.Isa is NttKernelIsa.Sse or NttKernelIsa.Scalar), "SSE fallback reported a wider ISA.");
            Console.WriteLine($"  mode={mode}; NTT={diagnostics.NttMultiplicationCount}; dispatches={diagnostics.KernelUsage.Sum(u => u.Calls)}");
            if (mode == 2)
                Console.WriteLine("    AVX-512: " + string.Join(", ", diagnostics.KernelUsage.Where(u => u.Isa == NttKernelIsa.Avx512).Select(u => $"{u.Phase}={u.Calls}")));
        }
    }
}
