param(
    [string]$Destination = 'artifacts/simd-audit/Instrumented.cs',
    [string]$SourceFile = 'MathSolver/Numerics/BigIntegers/ParallelBigUnsigned.cs',
    [ValidateSet('none','packed','inverse-l2','both')][string]$SmallCandidate = 'none'
)
$ErrorActionPreference = 'Stop'
$source = Get-Content $SourceFile -Raw -Encoding UTF8
$gates = [regex]::Matches($source, 'public bool (UseLargeModeAvx(?:2|512)\w+) =>')
if ($gates.Count -lt 15) { throw 'Large-mode gate layout changed; review instrumentation.' }
foreach ($gate in $gates) {
    $name = $gate.Groups[1].Value
    $source = $source.Replace($gate.Value, ($gate.Value + "`n            SimdAuditPolicy.Allows(`"$name`") &&"))
}
# Fix the power chain for optional kernel comparisons. Normal runs retain production dispatch.
$source = $source.Replace('if (workers.UseAvx512Ntt && exponent > 0)',
    'if (exponent > 0 && SimdAuditPolicy.UseLeftToRight(workers.UseAvx512Ntt))')
$source = $source.Replace('if (exponent > 1)',
    'if (exponent > 0 && SimdAuditPolicy.UseLeftToRight(true))')
if ($SmallCandidate -in @('packed','both')) {
    foreach ($stage in @(16,8)) {
        $pattern = "else if \(useAvx512LocalStagePair && stageLength == $stage\)\s*\{[^}]+\}"
        $matches = [regex]::Matches($source, $pattern)
        if ($matches.Count -ne 1) { throw "Packed dispatch layout changed: $stage" }
        $old = $matches[0].Value
        $new = $old.Replace('RegionQuarter4Avx512(', 'RegionQuarter4Low32Avx2(').
            Replace('RegionHalf4Avx512(', 'RegionHalf4Low32Avx2(').Replace('avx512Context', 'context')
        $source = $source.Replace($old, $new)
    }
}
if ($SmallCandidate -in @('inverse-l2','both')) {
    $pattern = 'if \(useAvx512LocalStagePair\)\s*\{\s*ExecuteInverseCachedStagePairRegionTwiddleMajorAvx512\('
    $matches = [regex]::Matches($source, $pattern)
    if ($matches.Count -ne 1) { throw 'Inverse L2 dispatch layout changed.' }
    $old = $matches[0].Value
    $source = $source.Replace($old, $old.Replace('TwiddleMajorAvx512(', 'TwiddleMajorLow32Avx512('))
}
$parent = Split-Path $Destination -Parent
New-Item -ItemType Directory -Force $parent | Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Destination), $source, [Text.UTF8Encoding]::new($false))
Write-Output "Instrumented $($gates.Count) gates: $Destination"
