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
