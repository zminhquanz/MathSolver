> Retired experiment. Linked SIMD sources now come from ../LegacySingleThreadSimd, not the application. Names such as production-avx2 below refer to the historical implementation.

# Single-thread BigInteger power audit

This console harness links the production controller and AVX2 kernels without MAUI. Arithmetic runs synchronously on one thread. It does not run NTT or exponents of 10M/100M.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/SingleThreadPowerAudit/Prepare.ps1
dotnet build tests/SingleThreadPowerAudit -c Release
dotnet tests/SingleThreadPowerAudit/bin/Release/net10.0/SingleThreadPowerAudit.dll bench 3 1000000 15 old-scalar,old-simd,runtime,simd 1000000
dotnet tests/SingleThreadPowerAudit/bin/Release/net10.0/SingleThreadPowerAudit.dll bench 999999999999999999 1000000 3 old-scalar,old-simd,runtime 1000000
dotnet tests/SingleThreadPowerAudit/bin/Release/net10.0/SingleThreadPowerAudit.dll validate 3 1000000 1 runtime,simd,old-simd,scalar-window,cut64,cut128,cut256,cut512,win3,win4,no-window
```

Arguments: command, signed Int64 base, exponent, rounds, comma-separated modes, warmup exponent (default 10,000). A full-size warmup uses one untimed power per mode; the default uses four smaller warmups. Each round rotates mode order. Hashing, result serialization and output are outside the arithmetic timer. Allocation counts include the synchronous arithmetic and its progress callback, not hashing. ArrayPool reuse and JIT warmup can change the first sample substantially.

- `old-scalar`: previous right-to-left loop from PowerRootEngine.
- `old-simd`: original full backend from commit `91bb63a58f0c3c55534b696b4a0a14a3f2ebddd6`.
- `runtime`: current controller with custom SIMD disabled, matching production dispatch. Its binary prefix and batching boundary are identical to `simd`.
- `simd`: explicitly force the bounded AVX2 window in the current controller for comparison. This bypasses the manager's production policy, but still checks hardware support.
- `scalar-window`: same UInt16 representation, accumulator and schedule as `simd`, with AVX2/SSE2 loops replaced by scalar products in a generated copy. This isolates vectorization from representation and scheduling.
- `cut64/128/256/512`: cap custom square input length and adjust predictive handoff in generated copies.
- `win3`, `win4`, `no-window`: change the exponent window size, leaving the terminal square batching policy intact.
- `runtime-prefix`: diagnostic copy forcing runtime arithmetic with the shared small-prefix scheduling boundary.
- `immediate`: start runtime batching immediately, the earlier alternative that did not preserve the small-prefix schedule.
- `direct`: direct BigInteger.Pow reference for timing only; it has no intermediate progress/cancellation contract.
- `profile`: generated instrumentation around custom square/multiply, conversion, and runtime square/Pow/multiply. Do not compare its whole-power times as an uninstrumented candidate.

`Prepare.ps1` writes all experimental source copies under `artifacts/single-thread-power/generated`, including a pinned historical baseline. It never changes production source. A generated scalar-only kernel has an intentional unreachable-code warning because vector loops are disabled.

Validation compares 41 powers with BigInteger.Pow, including signed limits, random bases, small limb/exponent boundaries and 999,999/1,000,000/1,000,001. It also checks identical progress sequences between `runtime` and `simd`, monotonic/exact progress totals, cancellation before work, during work and at final progress, and negative exponent rejection.

```powershell
$env:DOTNET_EnableAVX2 = '0'
dotnet tests/SingleThreadPowerAudit/bin/Release/net10.0/SingleThreadPowerAudit.dll validate 3 1000000 1 runtime,simd
Remove-Item Env:DOTNET_EnableAVX2

dotnet tests/SingleThreadPowerAudit/bin/Release/net10.0/SingleThreadPowerAudit.dll bench 999999999999999999 1000000 3 profile
dotnet tests/SingleThreadPowerAudit/bin/Release/net10.0/SingleThreadPowerAudit.dll micro
powershell -NoProfile -ExecutionPolicy Bypass -File tests/SingleThreadPowerAudit/Summarize.ps1 -Paths artifacts/single-thread-power/screen-base18.jsonl
```

Check the hardware banner when testing fallbacks. Disabling the application's custom SIMD is distinct from globally disabling runtime CPU intrinsics.

`micro` validates random/all-ones squares and cleared workspace, then measures AVX2, scalar UInt16 convolution, and native BigInteger squares. Results include output allocation but exclude converting the input to BigInteger. The native timing consumes its low limb through a BigInteger bitwise operation; do not interpret tiny-input timings as isolated instruction throughput. Full-power comparisons include conversion/workspace overhead and govern production decisions.

For a diagnostic without tiered compilation, set `DOTNET_TieredCompilation=0` only for that process. Optional `SINGLE_POWER_CPU=0` pins the harness on Windows; the application is never pinned. Pinning CPU 0 did not eliminate timing outliers in this session, so those results are not used to claim a winning threshold. Keep diagnostic sessions separate from normal-runtime confirmation data.

Raw JSONL and build logs are under the ignored `artifacts/single-thread-power/` directory. The dated audit note describes the accepted dispatch policy, evidence and limits; a smaller microbenchmark result alone is insufficient to enable a kernel.

Historical log naming: `screen-*`, `handoff-*`, `confirm-*`, `accepted-*`, `small-powers`, and `prefix-*` were taken before the final shared batching-boundary change. In those files, `runtime` means immediate runtime batching; `runtime-prefix` is the candidate later adopted. `accepted-base18` contains a 357-second timing outlier and is not a clean final confirmation. Only `production-*` logs use the final controller semantics described above. Do not pool these stages into one median.
