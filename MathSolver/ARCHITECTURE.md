# v26 - BitShift + Parallel Decimal Preparation

The power-of-two shortcut keeps its arithmetic path unchanged:

1. `|a| = 2^k` is detected.
2. The exact result is produced by single-threaded `BigInteger.One << (k * n)`.
3. For results below the TXT threshold, no parallel decimal preparation is performed.
4. For results requiring TXT export (`>= 100001` digits), phase 3 prepares the reusable base-10,000 magnitude through the same `ParallelBigUnsigned.Pow(|a|, n, workers, ...)` NTT/CRT pipeline used by normal parallel powers.
5. TXT export then reuses `ParallelMagnitude.WriteDecimalBlocks()` and the optional AVX2 decimal formatter.

This deliberately separates arithmetic from representation preparation: the calculation shortcut remains bit-shift based and single-threaded, while only the decimal/base-10,000 preparation phase uses the configured worker budget.

The old giant `BigInteger` binary-to-decimal `DivRem` import is no longer used by the bit-shift calculation path.

## v27 - Confirmable cancellation and safe Windows close

The Power/Root tab now treats cancellation as an explicit user decision:

- Pressing **Stop calculation** shows a Yes/No confirmation before cancelling the calculation token.
- Pressing **Stop creating TXT** shows a Yes/No confirmation before cancelling the export token.
- On Windows, **X** and **Alt+F4** are intercepted through `WindowStateManager` while either operation is active.
- Choosing **No** cancels the close request and leaves the active calculation/export untouched.
- Choosing **Yes** requests cancellation and awaits the active operation completion before Windows is allowed to close.
- Task completion sources are used only for shutdown coordination; they do not change the NTT/CRT, bit-shift, base-10,000, SIMD, or export algorithms.

## v28 - NTT Buffer Pool + Lifetime Reuse

Large NTT value workspaces are no longer allocated with `new uint[transformLength]`
for every modulus pass. One `NttBufferPool` is created for the complete `Pow`
operation and is shared by both PowSplit branches and the final combine.

Policy:

- cache at most two `uint[]` workspaces, matching the maximum two live transforms
  of a non-square convolution;
- retain only buffers whose length equals the largest transform length seen so far;
- release references to smaller cached workspaces as soon as a larger transform is
  requested;
- reuse the same workspaces between P1 and P2 and across later multiplications;
- on reuse, overwrite the compact limb prefix and clear only the stale zero-padding
  tail rather than clearing the whole array;
- return the right transform immediately after pointwise multiplication because it
  is dead before the inverse transform;
- use `try/finally` so cancellation or an exception cannot leak a leased workspace.

Lifetime reuse also removes the full `secondResidues[coefficientCount]` allocation.
The inverse P2 workspace stays leased while CRT reads its valid coefficient prefix
directly; CRT then returns that workspace to the pool. The first-modulus residue
array remains compact because keeping a full power-of-two P1 transform alive would
consume more RAM than the exact coefficient-length copy.

This version deliberately does not yet fuse CRT with Carry or change PowSplit
scheduling. Those remain separate future memory-optimization steps so RAM and
performance effects can be benchmarked independently.

## v29 - Scoped NTT Pool + Aggressive Lifetime Release

v29 keeps the v28 NTT buffer-reuse architecture but makes the end-of-calculation lifetime explicit and observable:

- PowSplit branches and final combine still share one `NttBufferPool` for exactly one `ParallelBigUnsigned.Pow()` call.
- After every worker team is disposed, v29 records peak leased/cache bytes plus rent/reuse counts, then calls `ReleaseCachedBuffers()` before the final magnitude leaves `Pow()`.
- `NttBufferPool.Dispose()` remains the cancellation/exception safety net.
- `FixedWorkerTeam.Dispose()` explicitly drops the last scheduled delegate/failure references so lambdas cannot keep a transform array reachable after the team finishes.
- Large twiddle tables now come from a small Pow-scoped `NttTwiddleBufferPool` instead of process-wide `ArrayPool.Shared`. Split branches may return tables to this local pool and the final-combine team can reuse up to four arrays; the whole twiddle pool is then released before `Pow()` returns. Fresh tables use `GC.AllocateUninitializedArray<uint>()` because each stage is fully initialized before its ready flag is published.
- For large prepared base-10,000 results (estimated workspace >= 512 MiB), `PowerRootView` performs one blocking Gen2 sweep only after the displayed calculation stopwatch has stopped. LOH compaction is deliberately disabled so the live final magnitude is not copied merely to reclaim dead workspaces.
- Result diagnostics now separate estimated algorithm workspace from current process Private Memory and expose NTT pool peak leased/cache bytes plus the reuse ratio.

The NTT/CRT arithmetic, v19 stage fusion, v21 global adaptive 8-way, L3/L2/L1 cache kernels, PowSplit scheduling, Carry, and SIMD TXT formatting are unchanged.

## v30 - CRT -> Carry Streaming In Place

v30 removes the full `ulong[coefficientCount]` CRT materialization. CRT is
reconstructed in bounded blocks, carry consumes each block immediately, and the
normalized base-10,000 limbs overwrite the no-longer-needed P1 residue array.
The P2 inverse workspace is read directly, so no compact P2 residue array is
created. DIF/DIT, primes, PowSplit scheduling and worker topology are unchanged.

## v31 - Shared Pow-Scoped Twiddle Plans + Zero-Fill Elision

v31 shares one immutable twiddle plan per modulus across both split branches and
the final combine. Fresh NTT/twiddle arrays use uninitialized allocation where
every element is guaranteed to be overwritten; `PrepareNttBuffer` clears only
the required zero-padding tail and overwrites the source prefix. The NTT/CRT
worker topology and arithmetic kernels are unchanged.

## v32 - NTT Workspace Lifetime / Dead-Tail Reuse

v32 keeps the v31.1 arithmetic and SMT topology intact and changes only storage
lifetime/reuse:

- the compact P1/result array is allocated only after inverse P1 has completed,
  rather than while the forward pass may still have two transform workspaces
  leased;
- the non-square right transform lives in a dedicated helper and is returned to
  the Pow-scoped NTT pool before inverse DIT begins, making that lifetime boundary
  structural rather than dependent on a nullable local;
- after each fixed-worker range completes, both the team field and every worker's
  local delegate reference are cleared immediately so a completed closure cannot
  keep 128-256 MiB transform arrays reachable until the next stage;
- after inverse P2, the unused transform tail is reinterpreted as the bounded CRT
  `ulong` scratch whenever at least one complete 1 Mi-coefficient block fits.
  This reuses already-live transform storage and avoids a separate 8 MiB scratch
  allocation for the common large-transform case; any fallback scratch retained
  by earlier smaller transforms is dropped as soon as tail reuse becomes valid;
- the dedicated CRT scratch fallback now uses uninitialized allocation because
  every block is fully overwritten before carry reads it.

No DIF/DIT stage order, modulus, primitive root, CRT formula, PowSplit scheduling,
worker count, or SMT behavior is changed in v32.

## v33 - Final Inverse Prefix Materialization

v33 keeps the v32 SMT topology, DIF/DIT stage ordering, NTT primes, twiddle
strategy, CRT streaming, and worker counts unchanged. The optimization is
limited to the last inverse-DIT stage, where only the linear-convolution prefix
is externally observable.

- P1 no longer normalizes/stores the full inverse transform and then copies
  `coefficientCount` residues into a compact array. The compact array is
  allocated only when the final DIT stage is reached, and that stage writes the
  valid normalized prefix directly into it. This removes one large
  `Array.Copy` read/write pass per P1 convolution while preserving v32's late
  allocation boundary.
- P2 remains in-place, but the final DIT stage skips normalization and stores for
  indices `>= coefficientCount`. That suffix is outside the exact linear
  convolution and is dead; v32 already reuses part of it as CRT scratch.
- The final-stage butterfly partition still uses the same fixed worker team and
  the same contiguous per-worker ranges. No SMT scheduling, worker topology,
  modulus, primitive root, or transform ordering is changed.
- Compact P1 allocation time is excluded from the inverse-transform diagnostic
  counter so timing remains comparable to the v32 arithmetic counters; total
  wall-clock time still includes the allocation, as before.

Expected effect: workspace size should remain approximately at the v32 level,
while memory traffic falls because the largest P1 residue copy disappears and
P2 no longer writes a dead normalized suffix. The gain is intentionally
benchmark-driven; v32 remains the fallback baseline if v33 does not improve or
match its 48.5-48.7 s wall-clock range.
