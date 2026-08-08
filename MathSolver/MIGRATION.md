# v25 -> v26

- Keep the exact `BigInteger` bit-shift shortcut unchanged.
- Replace phase-3 `BigInteger -> base-10,000` recursive DivRem import with the existing parallel NTT/CRT power pipeline.
- Use all configured logical workers for decimal preparation when multithreading is enabled; use one worker when it is disabled.
- Formatting progress now displays the worker count while this parallel preparation is active.
- TXT export remains unified through `ParallelBigUnsigned.WriteDecimalBlocks()` and the existing SIMD formatter.

## v27 - Stop/close confirmation

No API migration is required. `PowerRootView` now installs a temporary Windows close guard only while a calculation or TXT export is active. New localization keys cover confirmation title/messages and Yes/No buttons.

## v28 - NTT Buffer Pool + Lifetime Reuse

No public API migration is required.

`ParallelBigUnsigned.Pow()` now owns one internal `NttBufferPool` for the lifetime
of the complete power operation. `FixedWorkerTeam` receives that shared pool, so
PowSplit branches and the final combine can recycle large transform buffers.
`ConvolveModulus` now rents/returns transform workspaces with deterministic
`try/finally` lifetime and the second modulus feeds CRT directly from its inverse
workspace instead of allocating a separate second-residue array.

Arithmetic, NTT primes, stage fusion, cache tiling, scalar unroll policies, Carry,
SIMD TXT formatting, and cancellation semantics are unchanged.

## v29 - Scoped NTT Pool + Aggressive Lifetime Release

No public API migration is required.

`NttBufferPool` is still scoped per `ParallelBigUnsigned.Pow()`, but v29 now drops cached transform references before returning the final result and reports pool peak/reuse telemetry. Worker-team twiddle arrays no longer enter `ArrayPool.Shared`; a Pow-scoped twiddle pool lets the final-combine team reuse branch tables and then releases the cache with the calculation, preventing large tables from being retained globally afterwards. For large NTT/base-10,000 results, one forced Gen2 sweep runs after the calculation stopwatch has stopped, so it can reclaim dead LOH workspaces without changing the benchmarked NTT time.

Large-result diagnostics now distinguish estimated algorithm workspace from actual process Private Memory.
