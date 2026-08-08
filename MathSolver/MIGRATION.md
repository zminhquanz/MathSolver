# v25 -> v26

- Keep the exact `BigInteger` bit-shift shortcut unchanged.
- Replace phase-3 `BigInteger -> base-10,000` recursive DivRem import with the existing parallel NTT/CRT power pipeline.
- Use all configured logical workers for decimal preparation when multithreading is enabled; use one worker when it is disabled.
- Formatting progress now displays the worker count while this parallel preparation is active.
- TXT export remains unified through `ParallelBigUnsigned.WriteDecimalBlocks()` and the existing SIMD formatter.

## v27 - Stop/close confirmation

No API migration is required. `PowerRootView` now installs a temporary Windows close guard only while a calculation or TXT export is active. New localization keys cover confirmation title/messages and Yes/No buttons.
