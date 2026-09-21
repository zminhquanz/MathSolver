# AVX-512 <=10M: forward uncached single-stage experiment

## Decision

Keep the existing production dispatch. Widening this remaining AVX2 path to
AVX-512 improves the isolated work but does not establish an end-to-end win.
The test harness and candidate generator are retained for reproducibility.

## Candidate

In `ForwardDifTransform`, allow `workers.UseAvx512Ntt` in the gate invoking
`ExecuteForwardUncachedStageAvx512` for an uncached single stage. Other kernels,
inverse fusion, power scheduling, worker counts and >10M dispatch are unchanged.
The existing kernel includes narrower-vector fallback for tiny stages.

## Results

Local Windows .NET 10.0.12, AVX-512F/DQ available, 24 logical processors;
Release console harness with tiered compilation disabled. These numbers are
not MAUI UI timings and are not a speed prediction for another machine.

Input: `999999999999999999^10000000`, 24 workers, nine alternating samples
per version, exponent-100,000 warmup per process. Formatting is excluded;
residue checks and hashing happen after the stopwatch.

| Median | Baseline | Candidate |
| --- | ---: | ---: |
| Whole power | 1.738324 s | 1.766411 s |
| Sum of affected forward uncached single-stage profile times | 27.4340 ms | 22.2637 ms |

The targeted stages improved by about 18.8%, but the whole-power median was
about 1.6% slower. Sample ranges overlap (baseline 1.670449–2.453953 s;
candidate 1.632581–1.871849 s), so this is not evidence of a stable regression
either. The affected stage savings are too small to justify enabling the
candidate on this evidence. Do not interpret the first baseline sample's
2.454 seconds versus the first candidate's 1.871 seconds as a speedup.

All 18 full-power hashes matched:
`82CF1F8D668AFCD45BD6C2AC915211E2B7EE8B5B1A3F72195FEA93F4A7E81D25`.
Three independent modular checks also passed. Kernel/reference checks passed
for both NTT primes, 1/3/24 workers, width cascades, tails, guard regions and
cancellation; small full-power BigInteger oracles passed.

Raw logs and the two compiled variants are under `artifacts/avx512-sub10m`.
`tests/Avx512Sub10MValidation/Prepare.ps1` regenerates source variants.
The harness build reported two existing CA1857 warnings in BinaryRadix16.cs;
no arithmetic build errors occurred. No app deployment or benchmark UI change.

## Next direction

Prefer reducing full-array passes or improving existing fused global kernels
over widening this small remaining path. AVX2/AVX-512 inverse final fusion is
already enabled in <=10M; implementing it again would not add coverage.
Measure any further change independently and require a repeatable full-power
benefit before changing production routing.
