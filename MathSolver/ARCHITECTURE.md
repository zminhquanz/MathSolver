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

**Usage policy**: NTT/CRT arithmetic: **scalar only**. SIMD production paths are used for the Parabola evaluator and decimal formatting after Carry normalization, where the base-10,000 limbs are fully independent. TXT export dispatches AVX2 on Windows/x86 and NEON/AdvSIMD on Android ARM64. The shared Hardware acceleration switch enables/disables these production SIMD paths; benchmark mode selection itself does not alter runtime algorithm behavior.

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
- `MaxExponent = 10,000,000`
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

Accuracy is measured with the same C# contracts, parser, and `LlmWordProblemValidator` used by production generation. Six categories are tested: basic arithmetic, fractions, Find x, geometry, direct/inverse proportion, and motion. Each category runs exactly 10 independent samples. Benchmark generation forces `maximumAttempts = 1`, so every sample is scored from one model response and retry logic cannot inflate the measured accuracy. Results are shown as valid questions / 10 and percentage for each category, plus overall valid questions / 60 and overall percentage.

## AI generation interaction lock (2026-08-21)

While a Windows local-LLM question is actively generating, Math Puzzle enters an interaction lock. The three other Shell main tabs and the Settings action are disabled, and Math Puzzle disables source selection, model download/open/select/eject, question mode, problem type, basic-operation/proportion selectors, answer controls, and Next Question. The JSON & Log diagnostics toggle intentionally remains available because it is read-only. The only state-changing generation action left enabled is the primary Create-with-AI button, which switches to the red Stop action and cancels through the existing inference `CancellationToken`.

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
- `Mp4`: user-selected MP4 copied to app data. The video stream must be H.264/AVC, must have a compatible hardware-preferred decoding path, and must not exceed 120 seconds. Validation policy version 2 forces older saved wallpapers to be rechecked against the duration rule.

The Settings UI exposes the mode with one Picker. MP4 controls are shown only for MP4 mode; the Choose MP4 and Remove wallpaper buttons use equal 50/50 columns on Windows and Android. Glass resources remain driven by `LiveWallpaperManager.IsEnabled`; the built-in math animation uses a lighter readability scrim than arbitrary user video.


### MP4 adaptive frame contrast

- MP4 import uses a two-stage fast-accept path: copy + H.264/duration/hardware validation is awaited, then the valid wallpaper is committed and the Settings UI updates immediately. Low-resolution luminance analysis is optional background work and must never block file acceptance. On WinUI, native metadata inspection starts in parallel with the OS-level file copy and the system H.264 codec query is cached after the first use.
- Brightness analysis caps native thumbnail extraction at 48 samples per clip: short clips keep ~1-second resolution while longer clips increase the interval automatically (about 2.5 seconds for a 120-second clip). This reduces transient decoder buffers/RAM churn. Windows uses MediaComposition thumbnails and clears composition clip references promptly; Android uses MediaMetadataRetriever scaled frames when available.
- The timeline is stored beside the private wallpaper file. Runtime playback does **not** decode a second video stream: `LiveWallpaperView` only reads `MediaElement.Position` every 500 ms and looks up the nearest precomputed luminance byte.
- `AppThemeManager` uses hysteresis before switching polarity, preventing bright/dark text from flickering around a threshold. Only wallpaper-specific resources are updated: `WallpaperTextPrimary/Secondary`, glass surfaces, borders, and the readability scrim. The global app theme, Settings, and Hardware UI are not changed.
- When MP4 is dark, the learning area uses light text + dark glass; on bright frames it uses dark text + light glass. Math Animation continues to follow the selected app Light/Dark theme.

### Animated wallpaper runtime mode switching

- Switching between the built-in `GraphicsView` math animation and H.264 MP4 is applied live without restarting the app.
- `LiveWallpaperView` coalesces settings/policy refreshes to the next UI frame so WinUI Picker selection, MediaElement state changes, and the GraphicsView timer do not run re-entrantly.
- A mode switch never tears down the native player synchronously while the Picker is closing. When leaving MP4, playback pauses first and a 750 ms grace period retires `MediaElement` + `Source` if MP4 is still unused; switching back quickly cancels that retirement. Page inactivity/unload releases the source immediately.
- `AppThemeManager.RefreshVisualResources()` refreshes only wallpaper/glass/scrim resources. It must not reapply `UserAppTheme` or raise the global `ThemeChanged` event for a wallpaper-only setting change.

### Adaptive wallpaper text polarity fine-tuning (2026-08)

- Wallpaper-area neutral text must bind to `WallpaperTextPrimaryColor` / `WallpaperTextSecondaryColor`, never directly to the app-wide `TextPrimaryColor` / `TextSecondaryColor`. This includes shared solver hero/section styles and unselected buttons styled by `SelectionButtonStyler`.
- MP4 adaptive contrast uses hard text polarity: dark frame/glass -> primary text is pure white and secondary text is near-white; bright frame/glass -> primary text is pure black and secondary text is near-black. Selected/primary actions continue to use `OnPrimaryColor` so accent buttons keep their intended contrast.
- When animated backgrounds are disabled, the wallpaper text tokens map back to the ordinary app palette, so these bindings do not alter the static Light/Dark appearance.

## Local LLM semantic question validation (2026-08-22)

For item-based word problems (basic arithmetic, fractions, and Find X), validator acceptance now requires the **final interrogative clause itself** to name the same `answer_unit`/story item used by the C# contract. It is no longer enough for the required item to appear somewhere in the facts. This prevents mixed-object outputs such as facts about stamps followed by a question asking for books. Vietnamese matching reuses `WordProblemUnitEquivalence`, so classifier variants such as `cây/cái/chiếc bút` remain valid while a different noun is rejected. Retry feedback explicitly tells the model to rewrite the final question with the contract item.

`LlmWordProblemParser` also classifies output that contains only Gemma control/channel tokens after stripping as `EmptyModelOutput`. Production generation therefore follows the existing fresh-context retry path instead of treating control-token-only output as generic malformed JSON. Hardware accuracy benchmarking still preserves its explicit `maximumAttempts: 1` no-retry behavior.
