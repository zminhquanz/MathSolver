# Materialized powers of ten

Links the production arithmetic, engine routing and decimal writer without MAUI. No packages are required beyond the .NET 10 SDK.

```powershell
dotnet build tests/PowerOfTenValidation -c Release
dotnet tests/PowerOfTenValidation/bin/Release/net10.0/PowerOfTenValidation.dll validate 10 1000000 8
dotnet tests/PowerOfTenValidation/bin/Release/net10.0/PowerOfTenValidation.dll bench 10 1000000 8 7 direct,single,parallel8
dotnet tests/PowerOfTenValidation/bin/Release/net10.0/PowerOfTenValidation.dll bench 1000 1000000 8 7 direct,single,parallel8
dotnet tests/PowerOfTenValidation/bin/Release/net10.0/PowerOfTenValidation.dll export 10 1000000 8 1 single,parallel8
```

Arguments: command, Int64 base, exponent, worker budget, rounds, comma-separated modes.

- `direct`: the existing optimized single-thread controller applied directly to the original base; **not** the old symbolic zero writer or a bare BigInteger.Pow call.
- `single`: `5^(kn)` using the runtime controller, followed by a BigInteger shift.
- `parallel` / `parallel8` / `parallel24`: binary-limb NTT power, followed by packing and shifting. An explicit worker mode bypasses UI size selection for controlled comparisons.
- `export`: additionally runs the exact production BigIntegerDecimalWriter into a StringWriter, checks the digits and block-progress count, and reports conversion time separately. It excludes physical disk I/O.

`validate` checks 164 cases/assertions: materialized results against BigInteger.Pow; negative signs and exponent zero; routing and worker policy; invalid/oversized input; progress and cancellation; 16-bit shift boundaries; forced segmented convolution at N=1024; all-ones/random binary squares around the schoolbook/NTT/segment boundaries, reusing pair buffers; ordinary decimal NTT regression checks; and the shared decimal writer. Binary-kernel tests live in a test-only partial class so no validation hooks are added to production.

```powershell
$env:DOTNET_EnableAVX512 = '0'
dotnet tests/PowerOfTenValidation/bin/Release/net10.0/PowerOfTenValidation.dll validate 10 1000000 8
$env:POWER_TEN_SCALAR = '1'
dotnet tests/PowerOfTenValidation/bin/Release/net10.0/PowerOfTenValidation.dll validate 10 1000000 8
Remove-Item Env:POWER_TEN_SCALAR
Remove-Item Env:DOTNET_EnableAVX512
```

Check the banner: AVX2 fallback requires AVX512=false; scalar convolution requires Simd=false. Tests also exercise N=2^22 with the reduced twiddle cache via base 10^18, exponent 1M. These results must match the native BigInteger factorization hash.

Benchmark modes alternate forward/reverse order, warm up at exponent 1000, and collect before each sample. The first large NTT sample can include JIT compilation. JSON and SHA-256 are outside the arithmetic timer. Every mode in a workload must return the same signed BigInteger byte hash.

Memory sampling uses a separate thread at 10 ms intervals plus the final sample. `PeakPrivate` is sampled private process memory, including runtime/JIT/GC and worker stacks; it can miss shorter peaks. It is neither result size nor a hard bound. `AllocatedBytes` covers all process threads, including the small monitoring overhead. For mode comparisons use separate fresh processes (`memory-*` logs), because the runtime may retain committed pages from preceding modes. Array leases and retained-pool peaks are reported separately from process memory.

Raw results and build logs: `artifacts/power-of-ten/` (ignored). `results-20260911.csv` snapshots the final 42 timing samples, 4 separate-process memory samples, and 4 export records. `MathSolver/POWER_OF_TEN_ARITHMETIC_20260911.md` describes the production decision and limits.

Current large-capacity regression: `large 1000000000000000000 100000000 24` actually computes 5^1,800,000,000 with segmented binary NTT, shifts 1,800,000,000 bits, and retains a packed radix-2^32 integer. It checks three independent modular residues, bit length and the shifted low-bit region. The result array is 747,433,824 bytes. This command does not export 1.8 billion decimal digits. `large 1000000000000000000 40000000 24` covers the 40M case. Single-thread dense powers at these sizes are intentionally not benchmarked.

`validate` now includes forced large-result routing on small inputs, scalar Karatsuba squares, segmented NTT packing, arbitrary binary-to-decimal conversion (including zero/random/all-ones data), sign and cancellation. `wide` materializes arrays across 2^31/2^32 bits and validates a scalar sparse square beyond BigInteger capacity.

`binary-bench 10 1000000 8 7` compares the existing factorized BigInteger path against forced packed single/multi backends. Equality checks and conversion back to BigInteger are outside the arithmetic timer. `binary-export -10 1000001 8 1` also converts and compares the full decimal text; no disk I/O is timed.

The decimal-radix shortcut has been removed. Current architecture, limits and results: `MathSolver/POWER_OF_TEN_BINARY_REDESIGN_20260911.md`. Previous decimal-shift reports describe a superseded implementation.

The subsequent NTT optimization is documented in `MathSolver/POWER_OF_TEN_NTT_TUNING_20260911.md`. Source links now use `ParallelBigUnsigned_BinaryPower.cs` and `ParallelBigUnsigned_BinaryImport.cs`.

`ntt-tune <base> <exponent> <workers> <capLog2> [cacheForward] [persistent]` checks three modular residues and records diagnostics. Cap 0 (with no extra overrides) calls the production engine and its automatic memory policy. Explicit caps 22/24/25/26 force the kernel configuration and packed output. `22 false false` reproduces the old transform/cache/scheduler settings. Timings exclude modular validation, JSON and TXT conversion. Run one timing process at a time; separate processes avoid retained buffers changing the next mode.

`ntt-reference 999999999999999999 100000000 24` calls the original memory-bounded engine and checks its decimal magnitude modulo the same three primes. The banner records whether the assembly disables JIT optimization; Debug and Release timings must be compared separately. `validate` exercises cached/uncached spectra, static/legacy scheduling, short tails, cancellation during work and reuse afterward. Large fallback validation logs are correctness checks, not comparable performance samples when run concurrently.
