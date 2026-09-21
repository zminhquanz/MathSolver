$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = Join-Path $repo 'MathSolver/Numerics/BigIntegers/ParallelBigUnsigned.cs'
$destination = Join-Path $repo 'artifacts/avx512-sub10m'
New-Item -ItemType Directory -Force $destination | Out-Null
$text = [IO.File]::ReadAllText($source)
$old = 'if (workers.UseLargeModeAvx512ForwardGlobalUncached && !useTwiddleCache)'
$new = 'if ((workers.UseAvx512Ntt || workers.UseLargeModeAvx512ForwardGlobalUncached) && !useTwiddleCache)'
if ([regex]::Matches($text, [regex]::Escape($old)).Count -ne 1) {
    throw 'Expected exactly one uncached single-stage dispatch; review current source before benchmarking.'
}
[IO.File]::WriteAllText((Join-Path $destination 'Baseline.cs'), $text)
[IO.File]::WriteAllText((Join-Path $destination 'Candidate.cs'), $text.Replace($old, $new))
