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
