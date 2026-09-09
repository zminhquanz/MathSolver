# AVX-512 power validation and profiling

This package-free .NET 10 console harness links the production arithmetic source.
Its sole service stub replaces the MAUI hardware-acceleration preference, allowing
the same production power engine to run with acceleration enabled and disabled.

```powershell
dotnet run --project tests/Radix4Validation -c Release -- --require-avx512
dotnet run --project tests/Radix4Validation -c Release -- --require-avx512 --kernels-only --benchmark
dotnet run --project tests/Radix4Validation -c Release -- --require-avx512 --prefix-benchmark
dotnet run --project tests/Radix4Validation -c Release -- --power-benchmark 3 1000000 24 5
```

The default run compares both radix-4 directions with a direct modular four-point
DFT and the scalar kernels for both production primes. It covers every combination
of seven boundary values, deterministic random values, SIMD boundaries and scalar
tails, unaligned offsets, untouched guards, and forward/inverse round trips. Full
powers also compare exactly with `BigInteger`, with 1, 3, 4 and up to 24 workers and
the acceleration switch enabled and disabled. The 18-digit base raised to 100,000
exercises the profiled L2 path and the single-worker inverse bridge boundary.
A balanced conversion of the result limbs avoids a giant decimal-string conversion.
The final inverse-prefix kernel is also checked against independent modular
arithmetic for both primes, compact/in-place output, vector tails and guard values.
Power cases include exponent 1 and 65535/65536/65537, check progress completion,
and test cancellation before computation and from a progress callback.
Unsupported CPUs skip the AVX-512
kernel checks; `--require-avx512` makes that condition fail explicitly.

Use `--powers-only` with AVX-512 disabled at process startup to validate fallback
dispatch on an AVX-512 host:

```powershell
$env:DOTNET_EnableAVX512 = '0'
dotnet run --project tests/Radix4Validation -c Release -- --powers-only
Remove-Item Env:DOTNET_EnableAVX512
```

Kernel timing excludes allocation and reports medians of alternating scalar and
AVX-512 samples. The full-power timing command is a measurement tool, not a
correctness check; compare it with an earlier arithmetic source using identical
inputs/runtime/worker counts. It warms the complete requested power once, times
only `Pow`, and hashes the canonical result limbs after timing. Each sample also
prints the engine's phase counters. Kernel speedup alone does not establish an
end-to-end power speedup.

The 2026-09-09 review passed 3,216 radix-4 cases, 1,920 final inverse-prefix cases,
112 exact power comparisons and 12 cancellation cases on Ryzen AI 9 HX 370 /
.NET 10.0.11. The power and cancellation cases also passed with
`DOTNET_EnableAVX512=0`, exercising the AVX2 fallback. Optimization scope,
algorithm comparisons, timings and the baseline revision are in
[the review notes](../../MathSolver/AVX512_SUB10M_REVIEW_NOTES.md).
