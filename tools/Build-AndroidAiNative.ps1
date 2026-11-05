param(
    [Parameter(Mandatory=$true)][string]$NdkRoot,
    [Parameter(Mandatory=$true)][string]$LlamaSource,
    [ValidateSet('arm64-v8a','x86_64')][string]$Abi = 'arm64-v8a',
    [ValidateSet('Baseline','KleidiAI')][string]$CpuKernel = 'Baseline',
    [string]$BuildRoot,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if (!$BuildRoot) { $BuildRoot = Join-Path $PSScriptRoot '../artifacts/android-warning-cleanup/native' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '../MathSolver/Platforms/Android/Native/llama' }
$vsRoot = & 'C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe' -latest -property installationPath
if (!$vsRoot) { throw 'Visual Studio C++ build tools are required.' }
$cmake = "$vsRoot/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe"
$ninja = "$vsRoot/Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe"
$NdkRoot = (Resolve-Path -LiteralPath $NdkRoot).Path.Replace('\','/')
$LlamaSource = (Resolve-Path -LiteralPath $LlamaSource).Path.Replace('\','/')
# Do not silently reuse a source tree from the former Android hybrid build.
$modelSourceHash=(Get-FileHash -LiteralPath (Join-Path $LlamaSource 'src/llama-model.cpp') -Algorithm SHA256).Hash.ToLowerInvariant()
if ($modelSourceHash -ne '2daef19dabafa913a149c4bb1ed52f84cd5ee52abdb5fc64c528009909e5cc51') {
    throw 'CPU-only Android build requires pristine llama-model.cpp from pinned llama.cpp 815a2a5. Restore that source before rebuilding.'
}
$buildName = if ($CpuKernel -eq 'KleidiAI') { "$Abi-kleidiai" } else { $Abi }
$buildDirectory = [IO.Path]::GetFullPath((Join-Path $BuildRoot $buildName)).Replace('\','/')
if ($CpuKernel -eq 'KleidiAI' -and $Abi -ne 'arm64-v8a') { throw 'KleidiAI variant requires ARM64.' }
# Match LLamaSharp 0.30.0's Android instruction flags and minimum API. The
# warning fix changes NDK/linker alignment, not the existing ARM CPU tuning.
[string[]]$architectureFlags = if ($Abi -eq 'arm64-v8a') {
    @('-DCMAKE_C_FLAGS=-march=armv8.7a')
} else { @('-DCMAKE_C_FLAGS=-march=x86-64','-DCMAKE_CXX_FLAGS=-march=x86-64') }
if ($CpuKernel -eq 'KleidiAI') {
    # Optional, runtime-gated variant. Do not raise the packaged baseline ISA.
    $architectureFlags = @('-DCMAKE_C_FLAGS=','-DCMAKE_CXX_FLAGS=',
        '-DGGML_CPU_ARM_ARCH=armv8.2-a+fp16+dotprod+i8mm','-DGGML_CPU_KLEIDIAI=ON')
} else { $architectureFlags += @('-DGGML_CPU_ARM_ARCH=','-DGGML_CPU_KLEIDIAI=OFF') }
& $cmake -S $LlamaSource -B $buildDirectory -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" `
    "-DCMAKE_TOOLCHAIN_FILE=$NdkRoot/build/cmake/android.toolchain.cmake" "-DANDROID_ABI=$Abi" `
    -DANDROID_PLATFORM=android-23 -DANDROID_STL=c++_static -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON `
    -DCMAKE_PLATFORM_NO_VERSIONED_SONAME=ON `
    '-DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384' `
    -DGGML_BACKEND_DL=OFF -DGGML_CPU=ON -DGGML_NATIVE=OFF -DGGML_OPENMP=OFF -DGGML_LLAMAFILE=OFF `
    -DGGML_VULKAN=OFF -DLLAMA_BUILD_COMMON=OFF -DLLAMA_BUILD_TESTS=OFF -DLLAMA_BUILD_EXAMPLES=OFF `
    -DLLAMA_BUILD_SERVER=OFF -DLLAMA_BUILD_APP=OFF -DLLAMA_BUILD_TOOLS=OFF -DLLAMA_BUILD_MTMD=ON `
    -DLLAMA_CURL=OFF -DLLAMA_OPENSSL=OFF @architectureFlags
if ($LASTEXITCODE) { throw "Android $Abi configuration failed." }
& $cmake --build $buildDirectory --config Release --target ggml ggml-base ggml-cpu llama mtmd --parallel 8
if ($LASTEXITCODE) { throw "Android $Abi compilation failed." }
$destination = Join-Path $OutputDirectory $Abi
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in @('ggml-base','ggml-cpu','ggml','llama','mtmd')) {
    if ($CpuKernel -eq 'KleidiAI' -and $name -notin @('ggml-cpu','llama')) { continue }
    $filename = if ($CpuKernel -eq 'KleidiAI' -and $name -eq 'ggml-cpu') { 'libggml-cpu-kleidiai.so' } else { "lib$name.so" }
    $library = Join-Path $destination $filename
    Copy-Item -LiteralPath "$buildDirectory/bin/lib$name.so" -Destination $library -Force
    & "$NdkRoot/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-strip.exe" --strip-unneeded $library
    if ($LASTEXITCODE) { throw "Could not strip $library" }
    Get-FileHash -LiteralPath $library -Algorithm SHA256
}
if ($CpuKernel -eq 'KleidiAI') {
    foreach ($licenseName in @('Apache-2.0','BSD-3-Clause')) {
        $license = Join-Path $buildDirectory "_deps/kleidiai_download-src/LICENSES/$licenseName.txt"
        if (!(Test-Path -LiteralPath $license)) { throw "KleidiAI $licenseName license not found." }
        Copy-Item -LiteralPath $license -Destination (Join-Path $destination "LICENSE-KleidiAI-$licenseName.txt") -Force
    }
}
Get-ChildItem -LiteralPath $destination -Filter '*.so' -File | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS') -Encoding ASCII
& (Join-Path $PSScriptRoot 'Test-AndroidNativeAlignment.ps1') -Path $destination
if (!$?) { throw "Android $Abi alignment validation failed." }
exit 0
