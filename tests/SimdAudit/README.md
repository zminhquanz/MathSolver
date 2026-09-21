# Power SIMD audit

This console harness compiles `ParallelBigUnsigned.cs` directly, without MAUI.
Only the preference service is replaced. No benchmark option is added to production.
Run commands from the repository root with .NET 10, in Release, with one benchmark
process at a time. Do not build or run other CPU-intensive work during timed samples.

## Production validation and profiling

```powershell
dotnet build tests/SimdAudit/SimdAudit.csproj -c Release
dotnet tests/SimdAudit/bin/Release/net10.0/SimdAudit.dll validate
dotnet tests/SimdAudit/bin/Release/net10.0/SimdAudit.dll bench 999999999999999999 100000000 24 1
$env:DOTNET_TieredCompilation = '0'
dotnet tests/SimdAudit/bin/Release/net10.0/SimdAudit.dll micro
Remove-Item Env:DOTNET_TieredCompilation
```

Arguments after `bench`: unsigned base, exponent, worker count, repetitions.
The harness warms with exponent 100,000 before timing. Formatting and SHA-256
are outside the stopwatch. JSON includes complete stage diagnostics and a hash
of the canonical little-endian base-10,000 limbs.

For an AVX2-only process, set `DOTNET_EnableAVX512=0` **before launching dotnet**.
For the engine's scalar backend, set `SIMD_AUDIT_MODE=scalar`. Check the machine
header in every log. Restore environment variables afterwards. The scalar mode
disables explicit NTT/CRT acceleration; framework array copies and memory management
retain their runtime implementations. The AVX2 runtime switch also affects framework
code, so whole-process comparisons alone do not isolate an individual kernel.

## Isolated large-mode ablations

`Prepare.ps1` creates a copy under ignored `artifacts/simd-audit`, adding test-only
gates around the existing large-mode dispatch properties. The generated file is
never compiled into the application. Build it using the absolute path:

```powershell
./tests/SimdAudit/Prepare.ps1
dotnet build tests/SimdAudit/SimdAudit.csproj -c Release -p:AuditSource="$PWD/artifacts/simd-audit/Instrumented.cs" -o artifacts/simd-audit/bench
./tests/SimdAudit/Run-Ablations.ps1
./tests/SimdAudit/Summarize.ps1
```

The runner disables one AVX-512 group at a time, inserts controls between groups,
and checks the known 100M hash. It preserves the worker team, exponentiation graph,
buffer limits and all unrelated kernel choices. A disabled AVX-512 gate falls back
to whichever AVX2/scalar path production already provides.

In a generated build, `SIMD_AUDIT_CHAIN=rtl` or `ltr` fixes the power chain for
fair kernel comparisons. Current production uses left-to-right for all nontrivial
powers, independently of SIMD width. The pre-audit revision `daa8f95` used it only
with the shared AVX-512 flag. Historical whole-backend speedups therefore include
an algorithm difference and must not be attributed exclusively to SIMD.

`Prepare.ps1 -SourceFile <path>` can instrument a saved historical source file.
`-SmallCandidate packed`, `inverse-l2` or `both` reproduces the isolated <=10M
experiments when used with the pre-audit source. Neither experiment was adopted:
the packed candidate failed to retain a consistent whole-power advantage in
uninstrumented builds and the inverse-L2 candidate did not add a clear total gain.

## Kernel checks and interpretation

The micro suite creates direct delegates to current private production helpers.
Twiddle generation, context construction, reflection and delegate compilation are
outside the timer. It compares ordinary and Low32 AVX2/AVX-512 stage pairs against
a fused scalar Shoup comparator with the same two-stage memory traffic. A separate
two-pass modular reference verifies all implementations for both primes and guarded
offsets 0/1/15. Seven alternating samples also compare repeated outputs.

Pointwise covers products, squares, aliasing, odd starts and short tails. CRT is
compared with its production scalar fallback and checked against both residues.
Runs used for micro conclusions disable tiered compilation; the scalar comparator
also requests optimized JIT code, as the production NTT helpers do. Small micro
timings include delegate/loop overhead and are not an application
dispatch threshold by themselves. The fused scalar pair is a test comparator,
not a claim that every production scalar path already uses that exact loop.
An eight-element call to an AVX-512 pointwise/CRT entry point executes its scalar
tail; the entry-point label does not imply that such a tiny call uses vector lanes.

Production scalar routing does not populate every local-stage diagnostic bucket.
Do not interpret a zero scalar L2/L3 bucket as zero work, or compare those buckets
directly with the SIMD cache-blocked route. Local timers are accumulated worker
maxima; they are not independently additive wall-clock phases.

Results apply to the measured CPU/runtime/worker counts. Keep raw samples, compare
alternating whole-power runs, and change production dispatch only after correctness
and repeatable throughput evidence. No automatic CPU tuning or environment-controlled
experimental gate belongs in the production application.
