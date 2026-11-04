# Android AI native stack, 16 KiB pages

Five baseline shared libraries per ABI replace the 4 KiB-aligned binaries from
`LLamaSharp.Backend.Cpu.Android` 0.30.0. The managed LLamaSharp package stays at
0.30.0; these libraries use its exact llama.cpp submodule revision
`815a2a5915f22ce6a760c676389c5dfe8535c08f`.

ARM64 additionally packages `libggml-cpu-kleidiai.so`. It is built with KleidiAI
v1.24.0 and `GGML_CPU_ARM_ARCH=armv8.2-a+fp16+dotprod+i8mm`. Its SONAME stays
`libggml-cpu.so`: the managed loader preloads it before ggml/llama only when
Android HWCAP confirms the entire ISA floor (including CRC32/LSE/RDM and scalar/
vector FP16). Unsupported CPUs retain the baseline. This changes native CPU
kernels, independently of the managed app's SIMD setting.

Android AI is CPU-only. ARM64 `libllama.so` uses the pristine pinned source;
the former hybrid GPU host-buffer overlay has been removed. There is no Android
Vulkan plugin, detection, layer configuration or GPU executor. Windows continues
to use its own CUDA/Vulkan packages. The x86_64 CPU binaries are unchanged.
The CPU build recipe verifies the original llama-model.cpp hash and rejects a
tree that still contains the old hybrid overlay.

- ABIs: `arm64-v8a` and `x86_64`; Android API 23, same as the upstream package.
- Android NDK r28c (28.2.13676358), Clang/LLVM 19, Release, static C++ runtime.
- Shared-library LOAD alignment and RELRO boundaries: 16384 bytes. No versioned
  SONAME suffixes and no external libc++_shared.so dependency.
- CPU registry remains linked (`GGML_BACKEND_DL=OFF`, `GGML_VULKAN=OFF`).
- CPU tuning matches upstream: ARM C sources use `-march=armv8.7a`, C++ uses the
  toolchain default; x86_64 C/C++ use `-march=x86-64`. This warning fix does not
  broaden the upstream instruction-set/minimum-Android compatibility promise.
- OpenMP, llamafile and extra tools are off; llama and mtmd libraries are on.
- `SHA256SUMS` in each ABI folder records the exact binary hashes. Build sources
  and tools stay under ignored artifacts, not in the APK.

Build each ABI from the same pinned source directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-AndroidAiNative.ps1 `
  -NdkRoot <android-ndk-r28c> -LlamaSource <llama.cpp-pinned-revision> -Abi arm64-v8a
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-AndroidAiNative.ps1 `
  -NdkRoot <android-ndk-r28c> -LlamaSource <llama.cpp-pinned-revision> -Abi x86_64
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-AndroidAiNative.ps1 `
  -NdkRoot <android-ndk-r28c> -LlamaSource <llama.cpp-pinned-revision> -Abi arm64-v8a -CpuKernel KleidiAI
```

Run `tools/Test-AndroidNativeAlignment.ps1 -Path <directory-or-apk>` to check all
ELF LOAD segments and RELRO boundaries. Also run Android SDK zipalign with
`-c -P 16 4 <apk>` to verify APK packaging. On-device page-size behavior needs a
16 KiB Android system; ELF/ZIP checks alone do not prove it.

MIT/LLVM runtime and KleidiAI Apache-2.0/BSD-3-Clause notices are bundled from
the ARM64 folder. Rebuild this CPU stack when updating the managed LLamaSharp ABI.
