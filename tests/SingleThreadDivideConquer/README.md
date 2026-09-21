> Retired experiment. Linked SIMD sources now come from ../LegacySingleThreadSimd, not the application. Names such as production-avx2 below refer to the historical implementation.

# Single-thread divide-and-conquer SIMD experiment

This harness compiles the current production power controller and AVX2 kernel. It also generates explicitly named experimental copies; it does not change application dispatch or call NTT, Tasks, or Parallel.For. All timed arithmetic runs synchronously on the caller thread.

## Files

- `CandidateSquare.cs`: base-2^16 Karatsuba square, sum/difference formulations, scalar/AVX2/AVX-512DQ leaves, one pooled scratch arena and leaf accumulator per power.
- `Generate.ps1`: regenerates the three `.generated.cs` files from production. `AuditKernel` exposes the existing AVX2 leaf helpers; `CandidatePower` expands the handoff window; `ProfileCurrent` measures large runtime square batches.
- `Program.cs`: correctness/progress/cancellation checks, isolated square benchmarks and alternating-order full-power benchmarks. Result hashes are checked outside timing.
- `Results`: raw JSONL and median summary from this experiment.

## Reproduce (PowerShell, repository root)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/SingleThreadDivideConquer/Generate.ps1
dotnet build tests/SingleThreadDivideConquer -c Release

dotnet run --project tests/SingleThreadDivideConquer -c Release --no-build -- validate 3 1000000 1 current,kara-avx512-65536-128,diff-avx2-65536-128,diff-avx512-65536-128

$env:DOTNET_TieredCompilation='0'
dotnet run --project tests/SingleThreadDivideConquer -c Release --no-build -- micro
dotnet run --project tests/SingleThreadDivideConquer -c Release --no-build -- bench 3 1000000 7 runtime,current,kara-avx2-16384-128,kara-avx512-16384-128,kara-avx512-65536-128,profile
$env:DOTNET_TieredCompilation=$null

dotnet run --project tests/SingleThreadDivideConquer -c Release --no-build -- bench 17 1000000 9 current,diff-avx512-65536-128
```

Run commands sequentially, without another CPU benchmark at the same time. Disabling tiering controls JIT transitions during short samples; verify any proposed production winner again with default JIT settings. Microbenchmarks are a screening tool, not proof of an end-to-end improvement. Leaf configurations are tested in fixed order; power configurations rotate their order each round.

`runtime` means the shared production controller without the app's custom SIMD prefix. It is not a guarantee that the .NET runtime performs no hardware intrinsics internally. `current` means the existing SIMD-enabled application controller. `profile` adds timers and must not be used as the performance baseline.

`kara-<kernel>-<limit>-<leaf>` uses sum-form Karatsuba; `diff-...` uses the difference form. Sizes are input limb counts, with 16 bits per limb. The limit controls the one-way handoff to BigInteger. Both variants retain the existing runtime batching after that handoff. Small multiply-by-base operations still use the existing AVX2 kernel; `scalar` selects only the square leaf implementation.

The isolated square test excludes representation conversion and per-power workspace rental. Full-power tests include those costs. `Allocated` measures managed allocations on the caller thread, not peak RAM; pooled-buffer misses make it vary with run order.

AVX-512 uses F + DQ intrinsics. Unsupported square backends fall back to AVX2/scalar; the experimental power controller falls back to runtime when the AVX2 prefix is unavailable. Modes are forced by the harness, not selected from MAUI preferences. Do not copy its dispatch into production without the app's SIMD gates and repeatable whole-power gains.

Decision and measurements: [report](../../MathSolver/SINGLE_THREAD_DIVIDE_CONQUER_REPORT.md).
