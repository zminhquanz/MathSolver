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
