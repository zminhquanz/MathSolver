# Math Solver Architecture Reference

Math Solver shares its C# calculation and practice engines across Android and
Windows. [AppShell](AppShell.xaml) hosts the .NET MAUI pages for solving, practice,
formulas and multiplication tables.

## Core Numerics Engine

- [Calculations](Services/README.md#calculations) provides the solver engines.
  Basic arithmetic and rational expression evaluation are also used in practice.
  The calculator and formula reference share the [geometry catalog](Models/Formulas/GeometryFormulaItem.cs).
- [MatrixEngine](Services/Calculations/MatrixEngine.cs) takes signed 64-bit
  integers. It keeps addition and subtraction exact with 128-bit results and
  uses wide accumulators and `BigInteger` results for multiplication. The work
  plan checks memory before starting and can process large results in blocks.
- [ParallelBigUnsigned](Numerics/BigIntegers/ParallelBigUnsigned.cs) uses exact
  number-theoretic transforms (NTT) and Chinese remainder reconstruction (CRT)
  for large integer products. Powers above 10 million that use NTT go through
  a memory-bounded path with segmented transforms and a persistent worker team.
- [Extended-precision types](Numerics/README.md#floatingpoint) support roots and
  geometry. The quadratic solver computes the discriminant exactly with
  `BigInteger` and uses `OctoDouble` for its square root and the real roots.

### Power and root input limits

Powers accept integer bases from −10¹⁸ to 10¹⁸ and exponents from 0 to
100,000,000. `0^0` is undefined. Roots accept a signed 128-bit radicand and a
nonzero signed 32-bit degree. A negative degree gives a reciprocal root. A
negative radicand with an even degree can produce a complex result.

The calculator offers TXT export for powers with 19 or more digits. It streams
large results in decimal blocks.

## SIMD Acceleration

[CalculationAccelerationManager](Services/Performance/CalculationAccelerationManager.cs)
combines the hardware acceleration switch, the selected mode and runtime
support. Worker limits are managed separately by
[CalculationThreadingManager](Services/Performance/CalculationThreadingManager.cs).

### Android ARM64

For math kernels, SIMD execution requires an optimized Android Release build
with CoreCLR, ARM64 NEON hardware and runtime intrinsics. **Auto** chooses
**NEON (AdvSIMD)** when eligible. NEON uses direct `AdvSimd.Arm64` intrinsics
when available, or an accelerated `Vector128` path with scalar work for parts
of the kernel. **SVE** and **SVE2** appear as separate choices when CoreCLR
exposes direct `AdvSimd.Arm64`, SVE intrinsics and a 128-bit SVE vector width.
The SVE2 mode also requires SVE2 intrinsics.

| Workload | Backend and fallback |
| --- | --- |
| Integer matrices | Eligible matrices can use NEON, SVE or SVE2. At the engine level, a request for SVE2 can fall back to SVE, then NEON, then scalar. Uniformity scans use NEON when an SVE mode is selected. The engine uses scalar when any input dimension is below 16. |
| Large integer powers | The Android NTT/CRT SIMD path is eligible for exponents above 1,000,000. Explicit SVE/SVE2 modes use supported kernels and retain NEON or scalar for other stages. At or below 1,000,000, Android NTT/CRT uses scalar. |
| Parabola and TXT formatting | These paths use NEON where eligible, including when the selected calculation mode is SVE/SVE2. |

If a saved SIMD mode becomes unavailable, the app resolves it to the
automatic choice. Debug builds, an ineligible runtime or a disabled
acceleration switch leave Android math kernels on scalar execution.
[Power diagnostics](Numerics/BigIntegers/NttKernelUsage.cs) record the
instruction set actually used by phase.

### Windows x86-64

**Auto** prefers AVX-512, then AVX2, then SSE2 as supported by the runtime.
`AVX/AVX2` is the shared mode label. Integer matrix and power kernels use
AVX2 for 256-bit integer work, while AVX serves floating-point benchmarks.
Individual stages can use narrower kernels or scalar code.

## Practice and Local AI

The [quiz curriculum](Services/Quizzes/QuizCurriculumLayer.cs) controls what
appears in mixed or skill practice. C# generates the facts, computes answers
and grades responses. Optional local inference uses LLamaSharp and llama.cpp
to write templates for supported question types. C# validates the templates
before SQLite storage. The practice engine can reuse matching templates
without running inference. See the [question bank guide](Services/QuestionBank/README.md).

Local inference has its own backend selection. Android ARM64 uses Arm
KleidiAI on compatible CPUs and the baseline CPU backend elsewhere. On
Windows, compatible GPUs can use CUDA or Vulkan, with CPU fallback. See the
[Android native backend notes](Platforms/Android/Native/llama/README.md).

## Memory & Lifetime Management

- [NttPowerMemoryGuard](Services/Performance/NttPowerMemoryGuard.cs) requires
  at least 12 GB of installed physical RAM for NTT powers above 10 million.
  It checks capacity before workspace allocation. Matrix calculations have
  a separate [memory guard](Services/Calculations/MatrixMemoryGuard.cs).
- [Power result storage](Views/Calculators/PowerRootView.Memory.cs) writes
  completed results occupying at least 32 MiB to a private TXT file after
  60 seconds away from the power tab or when the app goes to the background.
  It releases the in-memory result after the file is complete. A failed
  archive keeps the data in memory. Export can stream the saved file, and
  abandoned files older than seven days are pruned when storage opens.
- [AppMemoryPolicy](Services/Memory/AppMemoryPolicy.cs) coordinates background
  cleanup with page-owned results and wallpaper resources.

## UI Architecture

[Views](Views/README.md) implement the platform-adaptive pages.
[Localization](../TRANSLATING.md) documents the Vietnamese and English packs
and how to add translations. [Wallpaper](Services/Wallpaper/LiveWallpaperManager.cs)
owns the background lifecycle. Imported H.264 MP4 wallpapers are capped at
120 seconds, and Android limits decoded frames to 3,686,400 pixels.

## Build & Distribution

See [Build from source](../README.md#build-from-source) for the SDK and build
commands. [MathSolver.csproj](MathSolver.csproj) defines the platform targets.

### Windows portable Release

The Windows Release build places its portable output in `portable/` under the
SDK build directory. Publishing writes the same layout to the publish directory.
Open the top-level `MathSolver.exe` and keep the `app/` directory beside it.
The runtime, WinUI resources and native AI dependencies live in `app/` with
their required relative paths. The Windows icon is embedded in the executable,
and the in-app splash uses the logo stored in `mathsolver-artwork.pack`.

### Project layout

| Directory | Responsibility |
| --- | --- |
| [Views](Views/README.md), [Controls](Controls/README.md), [Graphics](Graphics/README.md) | Pages, reusable controls and diagrams |
| [Models](Models/README.md), [Services](Services/README.md), [Numerics](Numerics/README.md) | Data, application services and math algorithms |
| `Platforms/`, `Resources/` | Platform-specific code and bundled assets |

### Android APK signing

To build a development-signed ARM64 APK for device testing, run the following.

```powershell
dotnet build MathSolver/MathSolver.csproj -f net11.0-android -c Release -r android-arm64 -t:SignAndroidPackage
```

For distribution, use your release keystore in Visual Studio or run these
commands with the password environment variables set locally. Use the same
key for updates to an already distributed APK.

```powershell
dotnet publish MathSolver/MathSolver.csproj -f net11.0-android -c Release -r android-arm64 `
  -p:AndroidKeyStore=true `
  -p:AndroidSigningKeyStore="C:/private/MathSolver.keystore" `
  -p:AndroidSigningKeyAlias=mathsolver `
  -p:AndroidSigningStorePass=env:MATHSOLVER_STORE_PASSWORD `
  -p:AndroidSigningKeyPass=env:MATHSOLVER_KEY_PASSWORD
```

### Cleaning build artifacts

Run `dotnet clean` if a rebuild shows stale generated resources. If necessary,
close the app, remove the project's `bin` and `obj` directories and rebuild.

The [test guide](../UnitTest/README.md) covers verification. The
[previous architecture notes](https://github.com/zminhquanz/MathSolver/blob/a0d6d853ef851e8f31935c1e0224105d75e33102/MathSolver/ARCHITECTURE.md)
preserve kernel experiments and historical benchmarks.
