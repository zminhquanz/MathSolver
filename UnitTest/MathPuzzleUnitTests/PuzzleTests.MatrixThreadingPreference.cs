using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Testing;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    public static void CheckMatrixThreadingPreference()
    {
        MatrixCalculationAttempt Run(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation, MatrixBackend backend)
            => MatrixCalculationRunner.TryCalculate(a, b, operation, backend);
        bool prior = CalculationThreadingManager.UseMultithreading;
        var a = new IntegerMatrix(1024, 1024);
        var b = new IntegerMatrix(1024, 1024);
        var narrow = new IntegerMatrix(1024, 17);
        for (int i = 0; i < a.Data.Length; i++) { a.Data[i] = i % 7 - 3; b.Data[i] = i % 11 - 5; }
        for (int i = 0; i < narrow.Data.Length; i++) narrow.Data[i] = i % 5 - 2;
        int cases = 0;
        try
        {
            foreach (bool enabled in new[] { false, true, false })
            {
                CalculationThreadingManager.SetUseMultithreading(enabled);
                int budget = CalculationThreadingManager.MaxDegreeOfParallelism;
                foreach (var backend in new[] { MatrixBackend.Scalar, MatrixBackend.Auto })
                foreach (var operation in new[] { MatrixOperation.Add, MatrixOperation.Subtract, MatrixOperation.Multiply })
                {
                    var right = operation == MatrixOperation.Multiply ? narrow : b;
                    using var attempt = Run(a, right, operation, backend);
                    TestAssert.That(attempt.Result is not null && !attempt.IsCancelled, "Changing worker policy must retain a successful result.");
                    var result = attempt.Result!;
                    int expectedLimit = operation == MatrixOperation.Multiply ? Math.Min(budget, 8) : budget;
                    TestAssert.That(result.WorkerLimit == expectedLimit && result.WorkerCount >= 1 && result.WorkerCount <= expectedLimit,
                        "Disabled multithreading must cap all 1024-square kernels at one worker; enabled policy must use the normal bounded plan.");
                    TestAssert.That(result.UniformScan.WorkerCount <= budget && result.UniformScan.WorkerLimit <= budget,
                        "Verification workers must also obey the shared preference.");
                    foreach (var (row, column) in new[] { (0, 0), (511, 8), (1023, 16) })
                    {
                        long expected = 0;
                        if (operation == MatrixOperation.Multiply)
                            for (int k = 0; k < 1024; k++) expected += (long)((row * 1024 + k) % 7 - 3) * ((k * 17 + column) % 5 - 2);
                        else
                        {
                            int index = row * 1024 + column;
                            expected = operation == MatrixOperation.Add ? index % 7 - 3 + index % 11 - 5 : (index % 7 - 3) - (index % 11 - 5);
                        }
                        TestAssert.That(result.GetValue(row, column) == expected, "Threading preference must not change exact matrix arithmetic.");
                    }
                    if (backend == MatrixBackend.Scalar) TestAssert.That(result.VectorBatches == 0, "Thread count must remain independent of SIMD selection.");
                    else if (System.Runtime.Intrinsics.X86.Sse2.IsSupported)
                        TestAssert.That(result.VectorBatches > 0, "Disabling multiple workers must retain the requested SIMD kernel.");
                    cases++;
                }
                var uniform = new IntegerMatrix(1024, 1024); Array.Fill(uniform.Data, 3);
                var uniformRight = new IntegerMatrix(1024, 17); Array.Fill(uniformRight.Data, 3);
                using var shortcut = Run(uniform, uniformRight, MatrixOperation.Multiply, MatrixBackend.Auto);
                TestAssert.That(shortcut.Result is { UsedUniformShortcut: true } compressed && compressed.GetValue(1023, 16) == 9216
                    && compressed.UniformScan.WorkerLimit == Math.Min(budget, 8) && compressed.UniformScan.WorkerCount <= budget,
                    "Uniform shortcuts must also use only one verification worker when the setting is off.");
                cases++;
            }
            CalculationThreadingManager.SetUseMultithreading(false);
            var above = new IntegerMatrix(1025, 1024);
            using var larger = Run(above, above, MatrixOperation.Add, MatrixBackend.Auto);
            TestAssert.That(larger.Result is { WorkerLimit: 1, WorkerCount: 1 }, "Matrices above 1024-square must stay single-worker when disabled.");
            CalculationThreadingManager.SetUseMultithreading(true);
            var below = new IntegerMatrix(1023, 1024);
            using var smaller = Run(below, below, MatrixOperation.Add, MatrixBackend.Auto);
            TestAssert.That(smaller.Result is { WorkerLimit: 1, WorkerCount: 1 }, "The existing size threshold must remain when multithreading is enabled.");
            cases += 2;
        }
        finally { CalculationThreadingManager.SetUseMultithreading(prior); }
        Console.WriteLine($"Checked {cases} hardware-preference matrix cases: off/on/off, 1024-square and boundaries, add/subtract/multiply, verification, exact values and independent SIMD.");
    }
}
