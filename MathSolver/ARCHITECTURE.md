# Math Solver Architecture Reference

## Overview

Math Solver is a cross-platform .NET MAUI desktop application providing arbitrary-precision arithmetic, scientific solvers, and educational math tools. The codebase is organized into layers: Numerics (core algorithms), Services (orchestration & utilities), Views (UI components), Models (data structures), and Platform-specific adapters for Windows/iOS/Android.

---

## Core Numerics Engine

### Arbitrary-Precision Integers — `Numerics/ParallelBigUnsigned.cs`

The heart of the application's computation engine:

- **Base**: Digits are stored in base 10,000 so TXT export never needs a giant binary-to-decimal division tree.
- **Multiplication**: Large products use two exact NTTs and CRT; butterfly work inside every transform is shared by the configured logical-processor worker budget.
- **Bit-shift shortcut**: `|a| = 2^k` powers use `BigInteger.One << (k * n)` — single-threaded, exact result. Binary BigInteger intermediate results are imported into the same base-10,000 representation before TXT export. A 1,024-limb leaf is exactly 4,096 decimal digits, matching the export block size and keeping each leaf conversion small.
- **TXT preparation**: For results requiring TXT export (`>= 100_001` digits), phase 3 prepares the reusable base-10,000 magnitude through the same `ParallelBigUnsigned.Pow(|a|, n, workers, ...)` NTT/CRT pipeline used by normal parallel powers.
- **TXT export**: Unified through `ParallelBigUnsigned.WriteDecimalBlocks()`. Decimal formatting uses AVX2 on Windows/x86 when available, NEON/AdvSIMD on Android ARM64, and scalar fallback otherwise. Both SIMD paths are controlled by the shared Hardware acceleration switch.

The old giant `BigInteger` binary-to-decimal `DivRem` import is no longer used by the bit-shift calculation path.

### Extended-Precision Floating Point — `Numerics/QuadDouble.cs` / `OctoDouble.cs` / `DoubleDouble.cs`

Three non-IEEE floating-point types that expand precision beyond IEEE 754:

| Type | Significant Bits | Typical Use |
|------|------------------|-------------|
| `DoubleDouble` | ~106 bits (32 digits) | Basic extended arithmetic, fallback paths |
| `QuadDouble` | ~212 bits (64 digits) | Standard solver precision for power/root, quadratic, geometry |
| `OctoDouble` | ~424 bits (128 digits) | Specialized high-precision solvers where extra guard digits matter |

All three use fused-multiply-add (`Math.FusedMultiplyAdd`) to capture rounding error in a separate component. Constants like `Pi`, `Sqrt(3)` are pre-parsed from string literals at static initialization.

---

## Memory & Lifetime Management

### NTT Buffer Pool

Large NTT value workspaces (`uint[]`) were previously allocated with `new uint[transformLength]` for every modulus pass, wasting RAM and GC pressure. The architecture now:

- Creates one `NttBufferPool` per complete `Pow()` operation.
- P1/P2 branches and the final combine share this pool — buffers are rented/returned deterministically via `try/finally`.
- **Policy**: cache at most two workspaces matching the maximum live transform length; release smaller cached buffers immediately when a larger one is requested.
- On reuse, overwrite the compact limb prefix and clear only the stale zero-padding tail instead of clearing the whole array.
- Return the right-transform workspace before inverse DIT begins (making that lifetime boundary structural, not dependent on nullable locals).

### Twiddle Table Pool

Fresh NTT/twiddle arrays use uninitialized allocation (`GC.AllocateUninitializedArray`) because every element is guaranteed to be overwritten before its ready flag is published. Split branches may return twiddle tables to a local pool and the final-combine team can reuse up to four arrays; the whole pool is then released before `Pow()` returns.

### Large-Result GC Cleanup

For large prepared base-10,000 results (estimated workspace >= 512 MiB), a blocking Gen2 sweep runs only after the displayed calculation stopwatch has stopped. LOH compaction is deliberately disabled so the live final magnitude is not copied merely to reclaim dead workspaces.

### CRT Streaming

Removes full `ulong[coefficientCount]` materialization. CRT is reconstructed in bounded blocks, carry consumes each block immediately. The compact P1 residue array is overwritten by the normalized base-10,000 limbs (in place). P2 inverse workspace is read directly — no compact P2 residue array is created.

### Final Inverse Split-Range ILP

The final inverse-DIT prefix kernel splits each worker's contiguous range at `validRightCount`, running two branch-free hot loops instead of testing a condition per butterfly. For large stages, both loops use four independent twiddle lanes advancing by `root^4` to remove the long dependency chain on `twiddle = twiddle * root % modulus`.

---

## Threading Model

### `Services/CalculationThreadingManager.cs`

- Reads user preference for multithreading via `Preferences.Default.Get()`.
- Exposes: `IsMultithreadingAvailable`, `LogicalProcessorCount`, `PhysicalCoreCount`, `RecommendedWorkerCount`, `MaxDegreeOfParallelism`.
- Default: 1 worker if no hardware threading detected; otherwise all logical processors.

### Worker Scheduling — `Numerics/ParallelBigUnsigned.cs`

- **Small SMT**: 8–19 thread CPUs (e.g., i7-8700) use 2,048-value L1 fused blocks to leave room for sibling threads in shared L1D.
- **Medium Thread**: 20+ threads use 4,096-value (16 KiB) fused blocks.
- **Large SMT**: 24+ thread CPUs (e.g., HX 370) keep the proven 8,192-value (32 KiB) block.

- Second cache-blocking level keeps several L1-sized fused blocks inside one L2-resident tile sized per logical-thread class so two SMT siblings do not consume the whole private L2 with values plus the largest twiddle stage.

- Third-level-cache tile removes more full-array sweeps before work reaches the L2 tile. Tile is conservative enough that all active SMT workers can retain useful L3 residency simultaneously.

---

## SIMD Acceleration

### `Services/CalculationAccelerationManager.cs`

Detects hardware capabilities and exposes:

| Mode | Hardware Requirement |
|------|---------------------|
| `Portable` | Any vector unit with count > 1 |
| `Sse` | SSE2 + AVX (x86) |
| `AvxAvx2` | AVX/AVX-512 support |
| `Avx512` | AVX-512 + hardware acceleration |

**Usage policy**: the proven in-place DIF/DIT scalar kernel remains the exact fallback. For powers `<= 10,000,000`, Windows x86/x64 can optionally dispatch cached butterfly work to an AVX2 256-bit kernel when the shared **Hardware acceleration** switch is enabled and `Avx2.IsSupported` is true. The AVX2 path keeps the existing uint32 layout, L1/L2/L3 cache blocking and fused DIF/DIT traversal; cached twiddles gain uint32 Shoup companions so eight residue lanes can perform exact modular twiddle multiplication without scalar `%` in the vector body. Small tails and uncached/global edge stages retain the proven scalar arithmetic. Turning Hardware acceleration off does not allocate Shoup companion tables and follows the original scalar scheduling/arithmetic path. The memory-bounded `>10M` PersistentStatic engine deliberately remains scalar until the <=10M AVX2 experiment is benchmarked and accepted. SIMD production paths for Parabola evaluation and decimal TXT formatting remain unchanged. Benchmark SIMD mode selection does not select the production NTT backend; only the Hardware acceleration switch plus actual CPU capability does.

---

## UI Architecture

### Shell & Navigation — `AppShell.cs` / `CalculationPage.cs`

```
App
 └── AppShell
      ├── MainTabBar  (Calculation, Settings, Hardware Performance, About)
      │   ├── CalculationPage
      │   │   ├── Basic tab (Add/Subtract/Multiply/Divide)
      │   │   ├── Power/Root tab → PowerRootView
      │   │   ├── QuadraticEquation tab → QuadraticEquationView
      │   │   ├── Fraction tab → FractionView / FractionCalculator
      │   │   ├── Geometry tab → GeometryCalculatorView
      │   │   └── Long Division tab → LongDivisionCalculator + Drawable
      │   ├── MathPuzzlePage
      │   │   ├── Practice tab → ArithmeticQuizGenerator + ArithmeticQuizValidator
      │   │   └── Learn tab → Basic arithmetic lesson content
      │   └── SettingsPage (locale, theme, threading config)
      └── AboutPage
```

`WindowStateManager` intercepts Windows X / Alt+F4 while calculations or TXT exports are active. Pressing Stop shows a Yes/No confirmation before cancelling the calculation token. Task completion sources coordinate with the OS close path.

### Shared Basic Arithmetic & Quiz Validation

`BasicArithmeticEngine` is the single source of truth for integer and decimal
addition, subtraction, multiplication, and division. `CalculationPage` uses it
for the Basic solver; `ArithmeticQuizGenerator` uses the same engine to create
practice questions. Quiz questions are accepted only after
`ArithmeticQuizValidator` recalculates the expression and verifies all shape
invariants: exact division, correct answer key, true/false flag consistency,
and four unique multiple-choice answers containing the correct answer once.

### Average & Percentage Word Problems

`AverageQuizGenerator` and `PercentageQuizGenerator` are shared by the Algorithm
and Windows AI/LLM paths. C# owns all numeric facts, subtype semantics, answer
unit, equation and correct answer; the local model may only rewrite the supplied
contract into natural `problem_text`. Average supports direct average, total to
average, average to total, missing value, indirect data and two-group weighted
average. Percentage supports percentage ratio, percentage value and whole from
a known percentage value. The UI exposes a subtype Picker for each family, with
a mixed-subtype option. `LlmWordProblemValidator` checks every numeric occurrence
in order, the final-question semantic family, and the final-question answer unit
before an AI question can reach the learner.

### Localization — `Services/LocalizationService.cs` / `TRANSLATING.md`

Language packs are UTF-8 JSON files following `culture.json` format: metadata + strings + templates. Placeholders (`{field}`) are preserved in all template files for runtime interpolation.

### Theming & Fonts — `Services/AppThemeManager.cs` / `Services/AppFontManager.cs`

Theme toggles between light/dark and color schemes; applied via resource overrides and XAML stylesheets. Font catalog caches system fonts by weight/style per culture; `AppFontManager` applies the selected font family to all text elements.

---

## Services Layer

| Class | Responsibility |
|-------|----------------|
| `CalculationThreadingManager` | Hardware threading detection, user preference persistence, worker budget calculation |
| `CalculationAccelerationManager` | SIMD feature detection, benchmark mode selection, hardware capability flags |
| `FractionCalculator` | Arbitrary-precision fraction arithmetic (Add/Subtract/Multiply/Divide/CommonDenominator) — all operations return a normalized result |
| `LongDivisionCalculator` | Decimal long division with step-by-step output; supports integer or decimal input |
| `ResultClipboardService` | Copies formatted calculation results to clipboard |
| `SelectionButtonStyler` | Consistent button styling across all views |

---

## Power/Root Solver — `Views/PowerRootView.cs`

The most performance-critical UI component:

- **Power mode**: Base ^ Exponent → uses `ParallelBigUnsigned.Pow()` with full worker budget.
- **Root mode**: Root(n, d) → solves x^d = n via binary search over QuadDouble range; precision controlled by `MaxRootDecimalPlaces`.
- **Cancellation**: `CancellationTokenSource` shared across the entire power operation and any active TXT export token. Cancellation is confirmed with a dialog before terminating worker teams.
- **TXT export**: For results >= 100,001 digits, the engine prepares base-10,000 magnitude through the parallel NTT/CRT pipeline, then streams decimal blocks via `ParallelBigUnsigned.WriteDecimalBlocks()`. With Hardware acceleration enabled, formatting uses AVX2 on Windows/x86 or NEON/AdvSIMD on Android ARM64; disabling it forces scalar formatting. A progress bar shows the worker count during preparation.

Key thresholds:
- `MaxBaseInputDigits = 19` (fits in Int64 magnitude check)
- `MaxExponent = 100,000,000`
- `LegacyNttMaximumExponent = 10,000,000`: exponents at or below this keep the existing production NTT/CRT path unchanged.
- `10,000,001..100,000,000`: `PowMemoryBounded()` keeps the public <=10M `Pow()` baseline unchanged, but large mode executes its <=10M seed/remainder and all later merges on one persistent physical worker pool. After HX 370 profiling showed that both fine-grained work stealing and coarse tail-help regressed the memory-bound NTT kernel, large mode now uses **persistent static scheduling**: every NTT/CRT generation gives each worker exactly one contiguous range and the complete worker team advances to the next `LargePowTaskGraph` node together. There is no butterfly-range stealing and no tail atomic cursor in the hot transform path. Full transforms above 2^26 are still decomposed into 2^25-limb segment pairs and reuse the existing exact two-prime uint32 DIF/DIT NTT/CRT arithmetic. A `MemoryBudgetGate` still caps transform leases at four while the pool retains at most three, keeping the measured large-mode RAM target around the ~6 GB class rather than trading scheduler activity for aggressive RAM growth.
- Large-mode forward-cache v3 keeps the existing outer-segment spectrum cache and adds one bounded cross-outer carry for the 3-segment square: the final right segment spectra from pair `(1,2)` are reused by diagonal `(2,2)`. P1(left) is consumed/returned before P2(right) is rented, so the optimization remains at the existing three-transform-buffer peak. For the `999999999999999999^100,000,000` topology this raises the real saved-forward count from 98 to exactly 100 without recomputing spectra, changing segment size, or adding another 256 MiB transform buffer.
- Stage completion is counted once per persistent worker generation. The coordinator cannot publish the next generation until all workers have fully exited the current static partition, preserving the `CountdownEvent` fix and eliminating queue/deque/tail-cursor races from the hot path.
- Static range boundaries use 64-bit intermediates before casting valid endpoints back to `int`. Large NTT stages can contain tens of millions of butterfly items, so the scheduler never multiplies a worker index by `itemCount` in Int32. This preserves the partition-overflow fix and prevents negative/overlapping spans.
- Workers remain alive through seed, remainder, merge tasks, segmented pair jobs, CRT and carry ranges. Between adjacent NTT generations they spin for only 16 short `SpinWait` iterations before parking; the previous 96-iteration window was intentionally reduced to avoid extra scheduler/cache pressure during memory-bound transforms. Stage dependency barriers remain exact.
- Large transforms remain serialized at the graph level: one memory-heavy NTT consumes the worker team at a time instead of launching competing transforms that fight for cache and memory bandwidth. The worker pool is persistent, but the arithmetic kernel is static and locality-first. A forced Gen2 collection is still used only at large-mode phase boundaries to reclaim dead magnitudes while pooled NTT/twiddle resources remain owned.
- Large-mode forward NTT reuse (2026-08-24): segmented multiplication keeps the current outer segment's already-transformed P1 and P2 spectra while it is paired with multiple right segments. Only the mutable right/product transform is rebuilt per pair; diagonal squares form the pointwise square directly from the cached spectrum. This removes repeated forward transforms without changing modulus arithmetic, DIF/DIT ordering, CRT, carry, segment size, or the <=10M production path. The large-mode NTT pool may retain three 2^26 uint32 buffers (two immutable cached spectra + one mutable product workspace); the legacy pool cap remains two. Non-square segmented products place the operand with fewer segments on the cached outer loop. Expected peak RAM rises only by one 2^26 uint32 buffer (~256 MiB) while forward-transform time drops substantially on repeated segment pairs.
- Every exponent above 10,000,000 shows an explicit RAM/storage confirmation, including direct ±10^k zero generation and |a| = 2^k bit-shift shortcuts.
- `ExportDigitThreshold = 100_001` → triggers parallel preparation
- `FullResultDigitThreshold = 18` → below this, results are cached and displayed immediately without export

---

## Error Handling & Edge Cases

| Scenario | Behavior |
|----------|----------|
| Division by zero (fractions) | Returns error with contextual message; no NaN produced |
| Invalid input format | Entry-level validation in `IntegerInputFormatter`; error messages shown inline |
| Overflow (Int128 range) | Input ranges are enforced via constants (`MaxIntegerInputDigits = 39`, `Min/MaxInt128InputValue`) |
| Large results (>1 MiB workspace) | Triggered Gen2 GC sweep after stopwatch stops; LOH compaction disabled to avoid copying the live result |

---

## Build & Distribution

- **SDK**: .NET MAUI (`Microsoft.Net.Sdk`) targeting Windows 10+, iOS, Android.
- **Platforms**: Single project with conditional `TargetFrameworks` and platform-specific adapters (`Platforms/Windows`, `Platforms/iOS`, `Platforms/MacCatalyst`, `Platforms/Android`).
- **Output type**: Native executable; no additional runtime dependencies beyond the .NET SDK.

## Android Material You / Material 3 migration

### Phase 2 (Android-only)

- Windows/WinUI is a locked UI/UX baseline and is not redesigned by this phase.
- Shell bottom navigation is rendered by MAUI Material 3 on Android and maps Math Solver's palette to Material surface/primary/on-surface-variant roles.
- Calculation and Formula secondary tabs keep the shared MAUI page architecture, while Android uses 48dp MaterialButton state layers and an animated 3dp selection indicator.
- Android alert/confirmation dialogs are routed through `MaterialDialogService`, which uses `MaterialAlertDialogBuilder`; non-Android platforms continue to call MAUI `DisplayAlertAsync`.
- The Android settings overflow remains a compact native popup menu. Destructive reset now requires a Material confirmation and reports completion with a Snackbar.
- Entry remains on the legacy handler on Android so the `EmojiCompatEnabled = false` numeric-format crash workaround is preserved.

## Android Material You Phase 3 (2026-08-18)

Android is now the platform-specific Material You surface while WinUI remains the stable Windows baseline.

- `UseMaterial3=true` remains Android-only.
- Optional Dynamic Color is off by default and available on Android 12+. Enabling it recreates only the Android activity so `DynamicColors.ApplyToActivityIfAvailable` can run before the view hierarchy is inflated.
- `AndroidMaterialYouManager` mirrors Material semantic colors (`primary`, `surface`, surface containers, outline, error) into MAUI DynamicResources. Custom Math Solver accent colors remain active when Dynamic Color is off.
- Android application chrome uses Material typography metrics, surface containers, subtle elevation, native ripple/state layers, and larger Material shape tokens.
- Custom SGK/math renderers and `GraphicsView` content remain shared and are not Materialized.
- WinUI values are preserved through `#if ANDROID` and `OnPlatform ... WinUI=<previous value>` branches.

## AI/LLM platform split (2026-08-20)

LLamaSharp/GGUF inference is Windows-only. The Android target does not restore or package LLamaSharp, does not probe a saved GGUF model path, and hides the entire quiz source-selection / AI card. Android goes directly to the deterministic Algorithm workflow, with visible steps renumbered to question mode and problem type, until a dedicated LiteRT-LM backend is implemented. The obsolete non-Windows LocalLlmQuizGenerator placeholder is removed; all live LocalLlmQuizGenerator references are guarded by `#if WINDOWS`.



## Windows local-LLM baseline

- The supported GGUF family is Gemma 4 only (E2B/E4B QAT Q4_0).
- The Hugging Face model catalog contains only the E2B and E4B one-click download cards.
- The main download action is labeled generically as “Download AI model from HuggingFace” / “Tải model AI từ HuggingFace”.
- While local inference is active, the Create-with-AI button becomes a red Stop-generation button that cancels the current token generation without unloading the selected model.

## Hardware AI/LLM benchmark (2026-08-21)

The Hardware Information page has two benchmark modes on Windows: **Raw performance** and **AI/LLM**. Android keeps Raw performance only because LLamaSharp is not packaged there.

The AI/LLM benchmark reuses the shared `LocalLlmRuntime.Generator`, so Math Puzzle and Hardware benchmarking share one GGUF weight cache instead of loading the model twice. It reports the Windows LLamaSharp/llama.cpp CPU backend, the highest available x86 ISA tier (AVX, AVX2/FMA, or AVX-512), configured decode/batch thread counts, and average decode throughput in token/s.

Accuracy is measured with the same C# contracts, parser, and `LlmWordProblemValidator` used by production generation. Eight categories are tested: basic arithmetic, fractions, Find x, geometry, direct/inverse proportion, motion, arithmetic mean, and percentage. Each category runs exactly 10 independent samples. The AI/LLM Hardware view has a benchmark-scope picker: **Overall** runs all 8 categories (80 samples total), while selecting one category runs only its 10 samples. Benchmark generation forces `maximumAttempts = 1`, so every sample is scored from one model response and retry logic cannot inflate measured accuracy. Overall mode reports valid questions / 80 plus the per-category chart/table; single-category mode reports valid questions / 10 for the selected category only. Subtypes are cycled deterministically within each 10-sample set (for example Find x operations, Geometry shapes, Motion scenarios, Average types, and Percentage types) so a category benchmark covers its internal domain instead of depending entirely on random selection.

## AI generation interaction lock (2026-08-21)

While a Windows local-LLM question is actively generating, Math Puzzle enters an interaction lock. The three other Shell main tabs and the Settings action are disabled, and Math Puzzle disables source selection, model download/open/select/eject, question mode, problem type, basic-operation/proportion/motion/average/percentage subtype selectors, answer controls, and Next Question. The JSON & Log diagnostics toggle intentionally remains available because it is read-only. The only state-changing generation action left enabled is the primary Create-with-AI button, which switches to the red Stop action and cancels through the existing inference `CancellationToken`.

The lock stays active across all validator retries in the same generation request and is released only after generation succeeds, is cancelled, or exhausts its attempts and returns a failure. Model import/download/eject busy states continue to use the existing local busy handling and do not use this app-wide AI-generation lock.

## Windows local-AI interaction lock

- While Math Puzzle is generating with the Windows LLamaSharp backend, AppShell disables both the non-selected `ShellContent` objects and their implicit `ShellSection` wrappers. On .NET MAUI 10.0.60, WinUI's `ShellItemHandler` caches top-tab `NavigationView` view models and does not reliably propagate a later `IsEnabled` change for direct/implicit `ShellContent` tabs. AppShell therefore forces the existing Windows `ShellItemHandler.MapTitle()` path to remap `MapMenuItems()` after every lock/unlock, then synchronizes any already-realized `NavigationViewItem` containers. This uses MAUI's existing handler rather than a custom renderer and makes the native tabs non-clickable on the first AI run. `Shell.Navigating` remains a second guard for keyboard/programmatic route navigation.
- Lock/unlock application is idempotent: every unlock re-enables the `ShellContent`, corresponding implicit `ShellSection`, cached WinUI menu model, and realized navigation-item containers, repairing stale native enabled state instead of returning early from a cached Boolean.
- The JSON & Log diagnostics toggle intentionally remains interactive during generation because it is read-only and useful for observing the live stream.
- Windows X / Alt+F4 is guarded while AI generation is active. The user can keep the app open and continue inference, or confirm stopping generation; the application closes only after the LLamaSharp generation task has observed cancellation and fully unwound.

## Power calculation interaction lock (2026-08-21)

Long-running power calculations reuse the same AppShell native-tab lock used by local AI. Once a power calculation starts, the Calculation tab remains selected while Formula, Multiplication Table, Math Puzzle, Settings, and every Calculation sub-tab button are disabled. Inside Power/Root, base/exponent inputs, Power/Root mode selection, Calculate/Clear, result actions, export, and diagnostics actions are disabled; the red Stop Calculation action remains available. The lock is released only after the calculation completes, fails, or cancellation has fully unwound.

On Windows, the existing `WindowStateManager` close guard also owns X / Alt+F4 during an active power calculation. The confirmation text is localized as “Bạn có muốn dừng tính toán và thoát chương trình không?” / “Do you want to stop the calculation and exit the application?”. Choosing No leaves the calculation running. Choosing Yes requests cancellation, awaits the calculation completion source, and only then reissues the native window close. Root calculations are synchronous/short and do not install this long-running interaction lock.

## Raw performance comparison charts (2026-08-21)

The Hardware Information → Raw performance tab exposes its benchmark variants through one compact localized Picker instead of three stacked cards. Windows offers **Calculation performance**, **Single-thread / Multi-thread**, and **SIMD 128 / 256 / 512-bit**; Android keeps the applicable non-x86 choices.

- **Scalar thread comparison** runs the same four-type benchmark twice, once with one worker and once with the recommended multi-thread worker count. SIMD is forced off so the vertical chart measures CPU thread scaling without vector-width changes.
- **Windows x86 SIMD comparison** benchmarks floating point only (Float + Double), because the existing Int32/Int64 paths are intentionally scalar. It runs each supported tier — 128-bit SSE, 256-bit AVX/AVX2, and 512-bit AVX-512 — once single-threaded and once multi-threaded, for up to six passes. Unsupported tiers are never executed and are omitted from both the comparison chart and the score summary; the availability status shows a cross mark for unsupported tiers.
- Both comparison charts use `Graphics/BenchmarkVerticalChartDrawable.cs`; the Picker, raw benchmark controls, and the AI/LLM benchmark tab are locked while a raw benchmark is running, and the existing cancellation/close guard is reused.
- Benchmark buttons use red only while active and explicitly clear that local brush before restoring the theme `PrimaryColor`, preventing WinUI from leaving a completed/cancelled button red.
- Human-facing ISA text is standardized as **AVX-512** (the internal enum remains `Avx512`).

## Animated wallpaper (MP4)

- Settings can import one user-selected MP4 into `FileSystem.AppDataDirectory/Wallpapers/live_wallpaper.mp4`; the original external path is never required after import.
- The optimized wallpaper path accepts H.264 / AVC video only. New imports are inspected before replacing the current wallpaper; legacy wallpapers are validated once on first use after upgrade.
- Windows playback stays on CommunityToolkit `MediaElement` -> WinUI `MediaPlayer` / Media Foundation, which uses the OS hardware-accelerated DXVA/D3D decode path when the GPU/driver/profile supports it. Android playback stays on `MediaElement` -> ExoPlayer / MediaCodec and requires an H.264 hardware decoder to be present. No FFmpeg/software decoder is added to Math Solver.
- `LiveWallpaperView` is layered behind the four main learning tabs (Calculation, Math Puzzle, Formula, Multiplication Table), loops silently, hides playback controls and releases its media source whenever the owning tab disappears. Android keeps `TextureView` because the glass UI requires correct sibling Z-order/transparency.
- Local AI/LLM inference no longer suspends either animated-background backend. Validated H.264 MP4 stays on its hardware-preferred video path, and the 24 FPS Math GraphicsView animation is lightweight enough to remain active during generation/benchmark runs. Both backends still stop when their owning learning tab is inactive.
- A theme-aware `LiveWallpaperScrimColor` sits above the video for readability; Light uses a lighter veil and Dark uses a darker veil. Future wallpaper formats/intensity controls should extend this service/control boundary instead of duplicating player logic in pages.


## Live wallpaper glass surfaces (2026-08)
- The four main learning tabs use adaptive `Wallpaper*` resource keys for cards, inputs, inactive sub-tabs and secondary actions.
- Wallpaper OFF maps those keys exactly to the normal opaque palette; wallpaper ON maps them to translucent Light/Dark glass surfaces. Settings and Hardware stay opaque.
- `LiveWallpaperScrimColor` is theme-aware and replaces the previous fixed `PageBackgroundColor` + 0.52 opacity overlay.
- Primary actions stay solid accent for hierarchy. Secondary surfaces, soft semantic states and section cards become translucent while preserving readable text/input surfaces.
- `LiveWallpaperManager` refreshes visual resources immediately after enable/import/remove.
- WinUI model actions (`Select model`, `Open in File Explorer`, `Eject model`) explicitly re-resolve current palette colors after visual-state/theme transitions to avoid cached Light/Dark brushes.

## Animated background modes (2026-08-22)

`LiveWallpaperManager` now supports two mutually exclusive animated-background modes for the four main learning tabs:

- `MathAnimation`: built-in `GraphicsView` ambient math animation at 24 FPS. No external file, bitmap, shader, or media decoder is required. It keeps running during local AI inference and stops only when the owning tab is inactive or animated backgrounds are disabled.
- Runtime memory is lifecycle-bound for both backends. `GraphicsView`, its drawable, and its 24 FPS timer are now created lazily and removed entirely when unused; `MediaElement` is created lazily only on an active MP4 page, its `Source` is detached as soon as that page becomes inactive, and the player object is retired after a short grace period when MP4 mode is no longer needed.
- `Mp4`: user-selected MP4 copied to app data. The video stream must be H.264/AVC, must have a compatible hardware-preferred decoding path, and must not exceed 120 seconds. Validation policy version 3 forces older saved wallpapers to be rechecked against the duration rule and the Android decoded-pixel memory budget.

The Settings UI exposes the mode with one Picker. MP4 controls are shown only for MP4 mode; the Choose MP4 and Remove wallpaper buttons use equal 50/50 columns on Windows and Android. Glass resources remain driven by `LiveWallpaperManager.IsEnabled`; the built-in math animation uses a lighter readability scrim than arbitrary user video.


### MP4 adaptive frame contrast

- MP4 import uses a two-stage fast-accept path: copy + H.264/duration/hardware validation is awaited, then the valid wallpaper is committed and the Settings UI updates immediately. Low-resolution luminance analysis is optional background work and must never block file acceptance. On WinUI, native metadata inspection starts in parallel with the OS-level file copy and the system H.264 codec query is cached after the first use.
- Brightness analysis caps native thumbnail extraction at 16 samples per clip: short clips keep ~1-second resolution while longer clips increase the interval automatically (about 7.5 seconds for a 120-second clip). This keeps adaptive polarity useful while sharply reducing native decoder/thumbnail churn. Optional analysis is never allowed to overlap live MP4 playback; if playback starts, analysis is canceled/deferred until the player becomes inactive. Windows clears composition clip references and requests a post-analysis managed/native-wrapper cleanup; Android disposes MediaMetadataRetriever/bitmaps promptly.
- The timeline is stored beside the private wallpaper file. Runtime playback does **not** decode a second video stream: `LiveWallpaperView` only reads `MediaElement.Position` every 500 ms and looks up the nearest precomputed luminance byte.
- `AppThemeManager` uses hysteresis before switching polarity, preventing bright/dark text from flickering around a threshold. Only wallpaper-specific resources are updated: `WallpaperTextPrimary/Secondary`, glass surfaces, borders, and the readability scrim. The global app theme, Settings, and Hardware UI are not changed.
- When MP4 is dark, the learning area uses light text + dark glass; on bright frames it uses dark text + light glass. Math Animation continues to follow the selected app Light/Dark theme.

### Animated wallpaper runtime mode switching

- Switching between the built-in `GraphicsView` math animation and H.264 MP4 is applied live without restarting the app.
- `LiveWallpaperView` coalesces settings/policy refreshes to the next UI frame so WinUI Picker selection, MediaElement state changes, and the GraphicsView timer do not run re-entrantly.
- A mode switch never tears down the native player synchronously while the Picker is closing. When leaving MP4 for Math Animation, the source is detached immediately and only the empty `MediaElement` shell gets a short 250 ms grace period so Picker switching stays re-entrancy-safe. Page inactivity/unload tears down the player immediately.
- `AppThemeManager.RefreshVisualResources()` refreshes only wallpaper/glass/scrim resources. It must not reapply `UserAppTheme` or raise the global `ThemeChanged` event for a wallpaper-only setting change.

### Windows minimize / restore wallpaper lifecycle (2026-08-22)

- `WindowStateManager` now reports native minimize/restore transitions through `LiveWallpaperManager` without changing the user's enabled/mode preference.
- On minimize, the active `LiveWallpaperView` releases transient `MediaElement`/GraphicsView resources and native handlers while preserving the active-page state. This reduces minimized RAM and avoids WinUI/Media Foundation retaining a composition surface that Windows may invalidate during a long minimize.
- On restore/maximize, the active owner recreates the background after a short dispatcher delay. MP4 therefore receives a fresh `MediaElement` + H.264 surface/source instead of calling `Play()` on a reclaimed native surface; Math Animation recreates its GraphicsView/timer similarly. No Settings round-trip is required.

### Live wallpaper memory ownership (2026-08-22)

- Shell keeps multiple learning pages cached, but only **one** `LiveWallpaperView` may own an animated background at a time. Ownership is guarded by a static weak reference; a newly appearing tab immediately retires native resources held by a stale previous owner. This prevents multiple cached `MediaElement`/ExoPlayer/MediaPlayer decoders from accumulating.
- Removing a MediaElement from the MAUI visual tree is not treated as sufficient cleanup. Math Solver first detaches `Source`, disables autoplay/loop, removes the view, then explicitly disconnects its MAUI handler so native decoder queues, surfaces and textures are released without waiting for GC. GraphicsView handlers are also disconnected when Math Animation is disabled/inactive.
- Android MP4 wallpapers use a 1440p-class decoded-pixel cap (3,686,400 pixels). 4K clips are rejected on Android because their decoder surface pools are wasteful for an in-app background and can cause RAM pressure/UI jank even with hardware decode. Windows keeps its desktop resolution freedom.
- Frame-profile generation and live MP4 playback are mutually exclusive decoder workloads. Adaptive metadata is deferred while the wallpaper is actively playing and resumes only when playback becomes inactive, avoiding a second decoder competing for RAM.

### Adaptive wallpaper text polarity fine-tuning (2026-08)

- Wallpaper-area neutral text must bind to `WallpaperTextPrimaryColor` / `WallpaperTextSecondaryColor`, never directly to the app-wide `TextPrimaryColor` / `TextSecondaryColor`. This includes shared solver hero/section styles and unselected buttons styled by `SelectionButtonStyler`.
- MP4 adaptive contrast uses hard text polarity: dark frame/glass -> primary text is pure white and secondary text is near-white; bright frame/glass -> primary text is pure black and secondary text is near-black. Selected/primary actions continue to use `OnPrimaryColor` so accent buttons keep their intended contrast.
- When animated backgrounds are disabled, the wallpaper text tokens map back to the ordinary app palette, so these bindings do not alter the static Light/Dark appearance.

## Local LLM semantic question validation (2026-08-22)

For item-based word problems (basic arithmetic, fractions, and Find X), validator acceptance now requires the **final interrogative clause itself** to name the same `answer_unit`/story item used by the C# contract. It is no longer enough for the required item to appear somewhere in the facts. This prevents mixed-object outputs such as facts about stamps followed by a question asking for books. Vietnamese matching reuses `WordProblemUnitEquivalence`, so classifier variants such as `cây/cái/chiếc bút` remain valid while a different noun is rejected. Retry feedback explicitly tells the model to rewrite the final question with the contract item.

`LlmWordProblemParser` also classifies output that contains only Gemma control/channel tokens after stripping as `EmptyModelOutput`. Production generation therefore follows the existing fresh-context retry path instead of treating control-token-only output as generic malformed JSON. Hardware accuracy benchmarking still preserves its explicit `maximumAttempts: 1` no-retry behavior.

### Average missing-value LLM validation refinement
- `AverageQuizType.MissingValue` no longer requires the structural test count/index to be repeated an identical number of times in `problem_text`; natural phrasing may mention the `4` once or twice.
- The three known scores and the target average remain immutable contract facts. A model output that substitutes the test index/count for the target average is still rejected because it changes the mathematics and can invalidate the C# answer/options.
- Retry feedback is role-aware and explicitly distinguishes known-test count, score values, test index/total test count, and target average.

## Percentage ratio semantic consistency (2026-08-22)
- `FindPercentageRatio` now stores only the two real numeric facts `[whole, part]`; repeated copies of the same numbers are no longer part of the LLM contract.
- Ratio reference contexts use a concrete subset noun (for example female students, story books, mango trees, red marbles) and ask for that subset's percentage of the whole without repeating the numeric values in the final question.
- The LLM prompt explicitly forbids inventing a complementary/secondary subgroup or assigning another count to it.
- `ValidatePercentageRatioFacts` rejects any extra/repeated numeric occurrence with `PercentageRatioFactsMismatch`; this prevents internally inconsistent stories such as total 200 students + 50 female + 50 male unless the remaining 100 students are explained (such extra partition data is not part of this basic ratio contract).

## Math Puzzle subtype expansion (2026-08-22)

- Motion now has a second-level Picker shared by Algorithm and AI/LLM: Mixed, Basic single-object motion, same-direction catch-up, opposite-direction meeting, and river downstream/upstream motion. `QuizProblemRequest.MotionType` carries the requested `MotionQuizType?` into the existing `MotionQuizGenerator`; null preserves mixed random generation. The generator already owns all four mathematical contracts, and the LLM validator continues to enforce the generated contract's exact motion family.
- Find X now has a second-level Picker shared by Algorithm and AI/LLM: Mixed, Sum, Difference, Product, and Quotient. `QuizProblemRequest.FindXOperation` carries the selected relationship through `QuizProblemTypeCatalog` into both `FindXQuizGenerator` and `LocalLlmQuizGenerator`; Mixed keeps it null so C# chooses randomly.
- Geometry now has a second-level shape Picker shared by Algorithm and AI/LLM: Mixed, Square, Rectangle, Triangle, Trapezoid, Rhombus, Parallelogram, Cube, and Rectangular Prism. `QuizProblemRequest.GeometryShape` filters the C# geometry story-template catalog before the contract is generated, so the AI cannot silently switch to a different shape. Only exact-integer-friendly quiz shapes are included; pi-based circle/sphere/cylinder/cone questions remain outside this quiz path for now.
- Average and Percentage generators use broader C# context catalogs. Average problems rotate through notebooks/books/fruit mass/pen boxes, distribution contexts, names, indirect-data objects, and two-group contexts while preserving the same six mathematical contracts. Percentage problems rotate through books, trees, students, marbles, flowers, pens, oranges, and tickets, with concrete subset nouns for ratio/value/whole problems.
- Diversity is owned by C#, not by free-form model creativity. The LLM continues to receive one authoritative reference contract and the existing role-aware validators still own numeric facts, requested quantity, unit, and semantic family.

### Live wallpaper static-restore resource safety

- Wallpaper glass/scrim resource refreshes are coalesced and dispatched after a short UI-frame delay instead of mutating the application `ResourceDictionary` inline from Switch/Picker callbacks.
- This allows `LiveWallpaperView` to disconnect `MediaElement`/`GraphicsView` native handlers before WinUI `DynamicResource` targets are repainted when animated wallpaper is disabled.
- Transient WinUI `COMException` during native teardown is retried on the dispatcher and is never allowed to crash the app.
- Existing `SolidColorBrush` instances are reused and only updated when their color actually changes, reducing allocations and resource churn during adaptive MP4 contrast transitions.

### Hardware AI/LLM benchmark scope display
- The per-category 10-sample benchmark intentionally hides the category-breakdown chart/table because the selected scope already represents exactly one category; only average decode speed and that category's `x/10 • %` accuracy remain visible.
- The horizontal accuracy chart and 8-row category breakdown are rendered only for the overall 80-sample benchmark (`8 categories × 10 samples`).

### WinUI theme restore after live wallpaper teardown
- When MP4/Math animated wallpaper is disabled or its backend is switched on Windows, `AppThemeManager` opens a short native-transition gate before mutating `Application.Resources`.
- Full Light/Dark/accent palette application and wallpaper glass-token refreshes are generation-coalesced behind that gate, so `MediaElement.Handler.DisconnectHandler()` / Media Foundation teardown cannot race `DynamicResource` propagation.
- Theme preferences are persisted immediately; only the visual resource mutation is deferred. Transient WinUI `COMException` is retried and never allowed to terminate the app.

### Formula / Arithmetic Mean tab
- `FormulaPage` now exposes a fifth sub-tab, `Average`, on Windows and Android.
- `AverageFormulaView` is a study/reference view (not quiz state). It reuses the six `AverageQuizType` concepts already used by Math Puzzle: Direct, TotalToAverage, AverageToTotal, MissingValue, IndirectData, and TwoGroups.
- The top interactive `GraphicsView` (`AverageDistributionDrawable`) visualizes arithmetic mean as conservation of the same total redistributed into equal shares. Users can adjust 2–5 integer values with sliders; the displayed fraction/mixed-number result updates immediately.
- Formula cards provide a rule, formula, word-problem example, and worked solution in Vietnamese and English. The view uses wallpaper-aware `Wallpaper*` resources so it remains readable with static themes, Math Animation, and adaptive MP4 live wallpaper.

### Power-of-two results beyond the .NET BigInteger bit ceiling (2026-08-24)

- .NET 9+ caps `BigInteger` values at `Int32.MaxValue` significant bits. Therefore a shortcut such as `(2^36)^100,000,000 = 2^3,600,000,000` cannot be materialized as `BigInteger`, even though the mathematical bit-shift strategy itself is valid.
- `PowerRootEngine` now treats `2^k` with `k <= Int32.MaxValue - 1` as the normal materialized `BigInteger` bit-shift path. Larger `k` uses an exact virtual representation (`sign + 2^k`) for calculation/result UI, so the calculation never attempts the guaranteed-overflow `BigInteger` allocation.
- The virtual path still reports the exact decimal digit count/compact scientific preview and keeps the existing >10M RAM warning. It does not allocate an NTT workspace merely to show the result.
- Full TXT export is intentionally deferred: if the user explicitly exports a virtual `2^k` result, the existing exact memory-bounded segmented/in-place NTT/CRT engine materializes base-10,000 limbs from the original `|base|^exponent`, streams them to TXT, then releases the temporary large workspace. This preserves exact export without creating an oversized `BigInteger`.
- The production <=10M NTT/CRT path is unchanged.

### AVX2 cache-resident stage-pair fusion experiment (<=10M)

When Hardware acceleration is enabled on AVX2-capable x86/x64 CPUs, the <=10M NTT path keeps the original 24-logical-worker policy from the measured ~26.5 s AVX2 baseline, but reduces value-buffer traffic inside the existing L1/L2/L3 hierarchy. Adjacent DIF stages (`S` then `S/2`) and the matching DIT stages (`S` then `2S`) are fused inside each already-owned cache tile. Four quarter streams remain live in AVX2 registers across both stages, so the intermediate residues are not written and then reloaded between the paired stages. Scalar DIF/DIT, Hardware acceleration OFF, and the >10M PersistentStatic/100-cache path remain unchanged.


## AVX2 <=10M cache-locality experiment (2026-08-25)

The accepted AVX2 comparison baseline is the 24-logical-worker cache-resident stage-pair build (~25.3 s on the HX 370 test case). This experiment keeps that worker policy and does not touch the >10M PersistentStatic/100-forward-cache path.

The next AVX2 pass reduces twiddle/Shoup traffic inside cache-resident regions by changing L1/L2/L3 local traversal from group-major to twiddle-major when a tile contains multiple independent groups. One 8-lane twiddle + Shoup block is loaded once and reused across all groups/parents in the resident tile. Global stages remain group-partitioned so value locality and worker occupancy are not traded away for twiddle reuse.

Inside each L1 block, every stage from the block size down through length 8 is now consumed as an adjacent stage pair. The remaining DIF 4+2 and inverse DIT 2+4 stages use a fused radix-4 scalar tail, removing the final intermediate value round trip. This is deliberately AVX2-only; Hardware acceleration OFF retains the scalar DIF/DIT baseline unchanged.


## AVX2 <=10M hybrid cache-resident pass (2026-08-25)

The measured AVX2 baseline after tile-local twiddle/Shoup reuse and L1 multi-stage fusion is ~23.03 s on the HX 370 reference case. The remaining gap to the ~21.705 s scalar baseline is concentrated mostly in Forward DIF.

This pass keeps AVX2 only where the existing L1/L2/L3 blocking guarantees cache residency. Global stages above the LLC boundary deliberately use the lower-traffic scalar cached DIF/DIT kernels, so they no longer stream the extra Shoup companion table through DRAM. The AVX2 cache-resident kernels still reuse twiddle/Shoup vectors tile-locally and keep the existing stage-pair/radix-4 fusion.

AVX2 modular add/sub and the final Shoup correction now use unsigned `VPMINUD` selection. Because both NTT primes satisfy `2p < 2^32`, `min(sum, sum-p)` and `min(diff, diff+p)` are exact modulo-p reductions with uint32 wraparound. This replaces compare/xor/mask correction chains with add/sub/min sequences while preserving exact residues. The Shoup product remains exact and still reduces back below `p` before values leave the butterfly.

Scalar Hardware-acceleration-OFF behavior and the >10M PersistentStatic/100-forward-cache engine remain unchanged.

### <=10M AVX2 local-Shoup-only experiment

The <=10M AVX2 kernel remains cache-resident only. `NttTwiddlePlan` now exposes a
`MaximumShoupHalfLength` equal to half the selected L3 NTT tile. Cached global
DIF/DIT stages still build/reuse the normal uint32 twiddle table, but they no
longer generate or write Shoup companions that the scalar global kernel never
reads. L1/L2/L3 stages keep the same Shoup layout and AVX2 arithmetic. This is a
forward-setup/memory-traffic optimization only; transform mathematics, worker
count, CRT/carry, and the >10M PersistentStatic path are unchanged.

### <=10M AVX2 Forward-DIF radix-8 cache fusion experiment

The <=10M hardware-accelerated NTT keeps the proven hybrid policy: global stages remain scalar to avoid Shoup/DRAM traffic, while AVX2 is confined to cache-resident L1/L2/L3 work.  Forward DIF now has an additional L1-local three-stage (radix-8 style) micro-kernel for stage triples with at most eight groups per fused block.  For stages S, S/2 and S/4 it keeps the four upper S-stage sums in YMM registers, writes only the four lower twiddled residues, completes the upper two child stages, then reloads the lower half once and completes the same child stages.  This reduces value-buffer traffic without requiring all eight value vectors plus seven twiddle/Shoup vectors to remain live simultaneously on AVX2's 16 architectural YMM registers.  When group count grows beyond eight, the engine returns to the existing twiddle-major stage-pair kernels because cross-group twiddle reuse becomes more valuable.  The inverse DIT path is intentionally unchanged so this experiment isolates the remaining Forward NTT bottleneck.

### <=10M AVX2 hybrid: global Forward scalar stage-pair fusion

The <=10M hardware-accelerated path keeps AVX2 restricted to cache-resident L1/L2/L3 work, but the RAM-sized Forward-DIF stages above the LLC boundary now avoid redundant whole-array sweeps as well.  When both global stages fit the bounded twiddle cache, cached stage-pair fusion can split one large group into independent quarter-stream segments, so the full worker team remains occupied even before group count reaches the worker budget.  For the earliest stages that are too large for the twiddle cache, a separate scalar segmented pair kernel fuses S and S/2 while maintaining only three per-segment twiddle recurrences: w^j, w^(j+S/4), and (w^2)^j.  This removes one value-buffer pass and one stage barrier per fused pair without allocating a DRAM-sized twiddle or Shoup companion stream.  Scalar-only <=10M and >10M PersistentStatic plans retain their previous global-stage policy; this experimental fusion is gated by the AVX2 hardware-acceleration plan.

### <=10M AVX2 Forward local/cache profiling + L2 direct-dispatch experiment (2026-08-26)

The `MathSolver_AVX2.zip` arithmetic/layout baseline remains the reference.  Forward diagnostics now split the cache-resident portion into L3-local, L2-local, and L1-fused critical-path buckets in addition to the existing global cached/uncached scalar buckets.  Timing is kept outside butterfly helpers: each fixed worker measures only cache-hierarchy phase boundaries and the profile reduces concurrent worker totals with `Max`, avoiding summed CPU-time inflation.  The remaining local/setup value is derived from total Forward time after subtracting the measured buckets.

The first local/cache optimization is intentionally narrow: when the L3 hierarchy descends into an L2 tile, each worker keeps one Forward Shoup table reference and one `Avx2NttModContext` alive across all of its tiles and passes them directly into the existing L2/L1 AVX2 traversal instead of repeating AVX2 dispatch and rebuilding `Vector256(modulus)` for every tile.  The direct L2 cache-blocked path also reuses that same proven AVX2 tile traversal instead of falling through the legacy scalar L2 implementation when hardware acceleration is active.  The radix-8, stage-pair, radix-4, twiddle layout, global scalar stage-pair kernels, 24/24 worker policy, and >10M PersistentStatic/100-forward-cache engine are unchanged.  No new `Unsafe.Add`, unsafe pointer arithmetic, or `MemoryMarshal` byref path is introduced.


### <=10M AVX2 Forward global managed-byref + unroll-2 + dual-lane revisit (2026-08-26)

This experiment is layered on top of the accepted Local/cache ContextHoist build rather than replacing it.  The cache-resident L3/L2/L1 AVX2 traversal and its detailed Forward profile are kept unchanged.  Only the two global Forward scalar stage-pair kernels are revisited so their contribution remains separately visible in the existing `Global cached scalar stages` and `Global uncached scalar stages` buckets.

The cached global scalar pair now consumes two adjacent butterflies per loop iteration.  Each butterfly forms ordinary managed `ref uint` aliases to its four residue slots and reuses those checked references for both the load and the final store.  The six cached twiddles for the pair are loaded before the arithmetic to expose independent cache hits without keeping eight residue byrefs live at once.

The uncached global scalar pair uses two independent even/odd twiddle recurrence lanes.  First-stage twiddles advance by `root^2`; second-stage twiddles advance by `root^4`.  The recurrence state is kept as uint32 residues and widens only for the exact uint64 modular product.  This shortens the serial recurrence dependency chain while retaining the existing segmented stage-pair memory topology and cancellation cadence.

No new `Unsafe.Add`, unsafe block, pointer arithmetic, or `MemoryMarshal.GetArrayDataReference` path is introduced by this experiment.  The existing AVX2 baseline intrinsics remain untouched.  Hardware acceleration OFF, the 24/24 worker policy, global no-Shoup policy, CRT/carry, and the >10M PersistentStatic/100-forward-cache engine are unchanged.


### <=10M AVX2 L1 small-stage packing + scalar-Shoup tail (2026-08-26)

The accepted ~17.9 s HX 370 checkpoint still spends most Forward time inside the L1 fused tail.  The hot S=16/S=8 stage-pair is only four butterflies wide per group, so the old generic path could not fill a 256-bit vector and fell back to scalar `% modulus` arithmetic.  The Forward-only L1 path now packs two adjacent 16-value groups into the low/high 128-bit halves of one `Vector256<uint>`, duplicates the same four cached twiddle/Shoup lanes into both halves, and executes the existing eight-lane AVX2 Shoup butterfly once for both groups.  This keeps the small stage pair on the SIMD path without increasing the live YMM set beyond the existing stage-pair kernel.

The final fused stages 4+2 remain scalar because each radix-4 group is tiny, but their one quarter-turn multiplication now uses the already-cached scalar Shoup companion instead of a variable 64-bit remainder.  Two independent radix-4 groups are consumed per loop iteration for modest ILP.  Global cached/uncached Forward kernels, 24/24 worker topology, Inverse DIT, CRT/carry, memory topology, and the >10M PersistentStatic/100-forward-cache engine are unchanged.

### <=10M AVX2 Inverse-DIT profiler-only checkpoint (2026-08-26)

The accepted HX 370 production checkpoint remains the ~17.7-17.8 s build. This diagnostic variant adds **Inverse NTT profiling only** and deliberately leaves all Inverse arithmetic, twiddle recurrence, normalization, cache traversal, worker topology, and Forward kernels unchanged.

Inverse diagnostics split the critical path into L3-local, L2-local, L1-fused, global cached scalar, global uncached scalar, final-prefix/normalization, and derived setup/other buckets. The 10M AVX2 L3 path uses phase-boundary timestamps around the existing L3 merge and around an exact copy of the existing L2-tile AVX2 traversal; no butterfly-level timing is inserted. Global cached/uncached and final-prefix measurements are coarse wrappers around the existing kernels.

No scalar-Shoup normalization experiment, Inverse context-hoist experiment, or inverse radix-4 unroll is present in this build. No new `Unsafe.Add`, unsafe pointer arithmetic, or `MemoryMarshal.GetArrayDataReference` path is introduced. Forward managed-byref/unroll-2/dual-lane, packed small-stage L1 optimization, 24/24 workers, CRT/carry, and the >10M PersistentStatic/100-forward-cache engine are unchanged.

### <=10M AVX2 inverse L1 packed 8+16 stage-pair experiment (2026-08-26)

The 17.7–17.8 s checkpoint keeps Forward NTT unchanged and targets only the
first Inverse DIT L1 stage-pair.  The 8+16 pair exposes four butterflies per
16-value parent, too narrow for a single Vector256; the generic AVX2 parent
kernel therefore fell through to scalar `uint64 % modulus` arithmetic for this
pair.  Two adjacent parents are now packed into the low/high 128-bit halves of
one Vector256 and processed with the existing cached Shoup AVX2 arithmetic.
Twiddle/Shoup vectors are invariant across the complete L1 block, while output
pairs are stored as soon as their second-stage multiply completes to keep YMM
live ranges bounded.  No Inverse normalization, L2/L3 traversal, global stage,
Forward kernel, worker topology, or >10M PersistentStatic policy is changed.
No `Unsafe.Add` or pointer arithmetic is introduced.


### <=10M AVX2 inverse cache stage-pair bounded-register schedule (2026-08-26)

Starting from the accepted 17.6–17.7 s packed-8+16 checkpoint, the generic cache-resident Inverse DIT stage-pair keeps exactly the same AVX2 Shoup arithmetic, twiddle-major traversal, value loads/stores, and worker topology, but shortens the live range of value-side vectors inside each parent.  The first child pair is loaded and reduced before the second child pair is loaded; after the even parent merge completes, its two output vectors are stored immediately before the odd merge starts.  Six cached twiddle/Shoup vectors still remain invariant across the parent loop, but fewer temporary value vectors compete with them for the 16 architectural YMM registers.  No extra unrolling, no additional memory traffic, no new `Unsafe.Add`, and no pointer arithmetic are introduced.  Forward NTT, the packed Inverse 8+16 kernel, global stages, normalization, CRT/carry, 24/24 workers, and the >10M PersistentStatic/100-forward-cache engine remain unchanged.

### <=10M AVX2 Forward L1 stage-pair/radix-4-only experiment (2026-08-26)

The measured HX 370 checkpoint before this experiment is approximately 17.5-17.6 s for `999,999,999,999,999,999^10,000,000`.  Forward NTT remains the largest transform hot path.  To test a simpler L1 policy matching the successful inverse DIT structure, the Forward DIF L1 traversal no longer dispatches the radix-8/three-stage micro-kernel.  All cache-resident Forward stages from the L1 block boundary down through length 8 are consumed by the existing AVX2 stage-pair path (including the packed 16+8 specialization), followed by the existing fused radix-4/radix-2 scalar-Shoup tail.  The radix-8 implementation is retained but unreachable in this experiment for quick rollback.  L2/L3 traversal, global Forward managed-byref/unroll-2/dual-lane kernels, Inverse NTT bounded-register scheduling, worker topology, twiddle/Shoup layout, and the >10M PersistentStatic/100-forward-cache path are unchanged.


### <=10M Inverse global cached scalar managed-byref/unroll-2 pass (2026-08-26)

After the stage-pair/radix-4-only Forward L1 policy became the preferred stable checkpoint, the next low-risk experiment targets only the cached global Inverse DIT stage-pair.  Two adjacent scalar butterflies are consumed per loop iteration, but each butterfly is completed before the next begins.  Four managed `ref uint` aliases are reused for load/store within one butterfly, reducing repeated indexing/range-check pressure without `Unsafe.Add`, pointer arithmetic, or a global Shoup stream.  The arithmetic, twiddle order, cancellation cadence, worker topology, Forward kernels, cache-resident Inverse AVX2 kernels, and >10M PersistentStatic/100-forward-cache path are unchanged.

### <=10M AVX2 Forward L1 stage-pair bounded-register schedule (2026-08-27)

Starting from the preferred ~17.4-17.5 s checkpoint, this experiment changes only the cache-resident Forward DIF **L1** stage-pair schedule.  L2/L3 stage-pair traversal keeps the accepted twiddle-major kernel unchanged.  Inside an L1 stage-pair, the first quarter-pair is loaded/reduced before the second quarter-pair is opened; after the upper S/2 branch completes, its two outputs are stored immediately before the lower branch performs its second-stage Shoup multiply.  The six twiddle/Shoup vectors remain invariant across the L1 region, but the value-side live set is shortened so RyuJIT has more room within AVX2's 16 architectural YMM registers.

The accepted packed 16+8 specialization is unchanged, as are the scalar-Shoup radix-4/radix-2 tail, Forward global managed-byref/unroll-2/dual-lane kernels, Inverse packed 8+16 and bounded-register kernels, Inverse global managed-byref/unroll-2 path, 24/24 worker topology, CRT/carry, and the >10M PersistentStatic/100-forward-cache engine.  No new `Unsafe.Add`, unsafe pointer arithmetic, or `MemoryMarshal.GetArrayDataReference` path is introduced.


### <=10M AVX2 Forward L2 stage-pair bounded-register schedule (2026-08-27)

Starting from the measured ~17.3 s HX 370 checkpoint, the next isolated experiment extends the already accepted Forward L1 bounded-register stage-pair schedule to the **L2-local** Forward DIF stage-pairs.  The L2 traversal still remains twiddle-major: the same three twiddle vectors plus three Shoup vectors are loaded once per eight butterfly lanes and reused across every group in the resident L2 tile.  Only the value-side schedule changes: one quarter-pair is completed before the next is opened, and the upper S/2 outputs are stored before the lower second-stage multiply, reducing peak live YMM state without changing arithmetic, stage fusion, twiddle order, or buffer traffic.

The L3 Forward path deliberately remains on the previous accepted twiddle-major schedule so the L2 change can be benchmarked independently.  The Forward L1 stage-pair/radix-4-only policy, packed 16+8 specialization, global managed-byref/unroll-2/dual-lane path, all Inverse optimizations, 24/24 worker topology, CRT/carry, and the >10M PersistentStatic/100-forward-cache engine are unchanged.  No new `Unsafe.Add`, unsafe pointer arithmetic, or `MemoryMarshal.GetArrayDataReference` path is introduced.
