> Retired experiment. Linked SIMD sources now come from ../LegacySingleThreadSimd, not the application. Names such as production-avx2 below refer to the historical implementation.

# Limb32 single-thread square experiment and production validation

The workload is integer power at exponent 1,000,000 on one calling thread, without NTT/CRT or worker tasks. Production source is linked directly in the project.

- `BaselinePower.cs`: snapshot of the controller before the limb32 change; `current` always means this stable baseline, including its enabled AVX2 prefix. It is not raw `BigInteger.Pow` and not the new app code.
- `Limb32Square.cs`: configurable prototype (scalar, AVX2, AVX-512F; adjustable leaf size).
- `SquareBridge.cs`, `InstrumentedPower.generated.cs`: capture exact intermediate square inputs and replace selected runtime square batches for the experiment.
- `Generate.ps1`: regenerate the instrumented controller from the frozen baseline. Do not regenerate the baseline from production when reproducing this comparison.
- `Program.cs`: correctness, fallback, cancellation, dispatch bounds, micro and whole-power benchmarks.
- `production-avx2` / `production-avx512`: invoke the actual linked `SingleThreadBigIntegerPower` and `SimdBigIntegerSquare` application code. No preference stub replaces this arithmetic.
- `Results`: raw JSONL, summary medians, and source fingerprints of the final production measurement.

## Run from repository root (PowerShell)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/SingleThreadLimb32/Generate.ps1
dotnet build tests/SingleThreadLimb32 -c Release
dotnet run --project tests/SingleThreadLimb32 -c Release --no-build -- validate

# All hardware intrinsics disabled: log must show Avx2=false and Avx512=false.
$env:DOTNET_EnableHWIntrinsic='0'
dotnet run --project tests/SingleThreadLimb32 -c Release --no-build -- validate
$env:DOTNET_EnableHWIntrinsic=$null

# Default JIT, rotating matched runs; repeat with bases 17 and 65537.
dotnet run --project tests/SingleThreadLimb32 -c Release --no-build -- bench 3 1000000 9 current,production-avx2,production-avx512

# Capture the last three distinct square inputs of the existing power schedule.
dotnet run --project tests/SingleThreadLimb32 -c Release --no-build -- capture 65537 1000000

# Exploratory square measurement, including BigInteger conversion.
$env:DOTNET_TieredCompilation='0'
dotnet run --project tests/SingleThreadLimb32 -c Release --no-build -- micro 3 1000000 5 current,avx2-128,avx512-256
$env:DOTNET_TieredCompilation=$null
```

Prototype mode syntax: `<backend>-<leaf>[-<minimumWords>-<maximumWords>]`. Mode `current` in a microbenchmark is runtime `value * value` on the captured operand. In whole-power benchmarks it is the frozen controller.

Full-power timings include construction/rental of square workspaces, all conversions and result allocation. Warmup uses exponent 100,000, so the first million-exponent production sample can still contain cold large-path costs. Reported production medians include that sample; sample order rotates each round. Hardware intrinsics are enabled by the requested test mode, independent of MAUI preferences. The application additionally applies its shared SIMD preference at the engine entry point.

Microbenchmarks prime the scratch pool and operand conversion outside timing, then measure `SquareBigInteger` (both import and export included). `square-words` additionally measures arithmetic starting with uint32 words. Each result is checked against runtime squaring; full-power hashes are compared outside timing. Capture expands runtime square batches to record exact mathematical intermediates and is not a performance baseline.

Allocation counters are managed allocations on the caller thread, not peak RAM. Pool state and the extra representations affect allocation volume. Run timing commands sequentially without other CPU benchmarks.

[Production decision and results](../../MathSolver/SINGLE_THREAD_LIMB32_REPORT.md).

## Whole-power regression reproduction

`runtime` runs the frozen pre-limb32 controller with the custom SIMD prefix disabled; `current` runs that same controller with its AVX2 prefix. `regressed-avx2` / `regressed-avx512` use `RegressedPower.cs`, a frozen copy of the over-broad production dispatch before the Debug/whole-result guard. These modes intentionally reproduce the regression and must not be used by the app.

```powershell
dotnet run --project tests/SingleThreadLimb32 -c Debug -- bench 999999999999999999 1000000 1 runtime,current,production-avx512
# Deliberately slow, for diagnosis only:
dotnet run --project tests/SingleThreadLimb32 -c Debug --no-build -- bench 999999999999999999 1000000 1 regressed-avx512
```

Production now disables limb32 in Debug and limits Release dispatch by the estimated complete result size, in addition to the individual-square bounds. Run validation in both build configurations to check these gates. `Optimized` in machine metadata reports whether the harness assembly disables JIT optimization.
