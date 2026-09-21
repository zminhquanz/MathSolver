param([string]$BaselineRevision = '91bb63a58f0c3c55534b696b4a0a14a3f2ebddd6')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $root 'artifacts/single-thread-power/generated'
[IO.Directory]::CreateDirectory($output) | Out-Null
$controller = [IO.File]::ReadAllText((Join-Path $root 'tests/LegacySingleThreadSimd/SingleThreadBigIntegerPower.cs')).Replace("`r`n", "`n")
$kernel = [IO.File]::ReadAllText((Join-Path $root 'tests/LegacySingleThreadSimd/Avx2BigIntegerPower.cs')).Replace("`r`n", "`n")
$original = & git -c "safe.directory=$($root.Replace('\','/'))" show "${BaselineRevision}:MathSolver/Numerics/Powers/Avx2BigIntegerPower.cs"
if ($LASTEXITCODE -ne 0) { throw 'Cannot read baseline source' }
[IO.File]::WriteAllText((Join-Path $output 'Legacy.cs'), ($original -join "`n").Replace('Avx2BigIntegerPower', 'LegacyAvx2BigIntegerPower'))
$scalar = $kernel.Replace('Avx2BigIntegerPower', 'ScalarWindowKernel')
$scalar = [regex]::Replace($scalar, 'right.Length /\s*VectorUShortCount', '0')
$scalar = [regex]::Replace($scalar, 'remaining /\s*VectorUShortCount', '0')
$scalar = $scalar.Replace('right.Length - tailStart >= 8', 'false').Replace('if (value.Length - j >= 8)', 'if (false)')
[IO.File]::WriteAllText((Join-Path $output 'ScalarWindowKernel.cs'), $scalar)
$variants = @(
    @{ Name='ScalarWindowPower'; Kernel='ScalarWindowKernel' },
    @{ Name='Cutoff64Power'; Cutoff=64 },
    @{ Name='Cutoff128Power'; Cutoff=128 },
    @{ Name='Cutoff256Power'; Cutoff=256 },
    @{ Name='Cutoff512Power'; Cutoff=512 },
    @{ Name='Window3Power'; Window=3 },
    @{ Name='Window4Power'; Window=4 },
    @{ Name='NoWindowPower'; Window=1 },
    @{ Name='RuntimePrefixPower'; Prefix=$true },
    @{ Name='ImmediateRuntimePower'; Immediate=$true },
    @{ Name='ProfilePower'; Profile=$true }
)
foreach ($variant in $variants) {
    $copy = $controller.Replace('SingleThreadBigIntegerPower', $variant.Name)
    if ($variant.Kernel) { $copy = $copy.Replace('using static MathSolver.Numerics.Avx2BigIntegerPower;', "using static MathSolver.Numerics.$($variant.Kernel);") }
    if ($variant.Cutoff) {
        $copy = $copy.Replace('!CanSquareInCustomWindow(length) ||', "length > $($variant.Cutoff) || !CanSquareInCustomWindow(length) ||")
        $copy = $copy.Replace('PredictiveRuntimeWindowHandoffMinimumLimbCount = 272', "PredictiveRuntimeWindowHandoffMinimumLimbCount = $($variant.Cutoff)")
    }
    if ($variant.Window) { $copy = $copy.Replace('MaximumRuntimeExponentWindowBitCount = 5', "MaximumRuntimeExponentWindowBitCount = $($variant.Window)") }
    if ($variant.Prefix) {
        $copy = $copy.Replace('bool useCustomWindow = useSimd && IsSupported;', 'bool useCustomWindow = false;')
    }
    if ($variant.Immediate) { $copy = $copy.Replace('bool runtimeBatchingEnabled = false;', 'bool runtimeBatchingEnabled = !useCustomWindow;') }
    if ($variant.Profile) {
        $copy = $copy.Replace('BigInteger.Pow(', 'AuditMeasure.Pow(')
        $copy = [regex]::Replace($copy, 'resultBigInteger \*=\s*(\w+);', 'resultBigInteger = AuditMeasure.Multiply(resultBigInteger, $1);')
        foreach ($method in @('SquareMagnitude','MultiplyMagnitude','ToBigInteger')) { $copy = $copy.Replace("$method(", "AuditMeasure.$method(") }
    }
    [IO.File]::WriteAllText((Join-Path $output "$($variant.Name).cs"), $copy)
}
Write-Output "Prepared variants from $BaselineRevision. Baseline SHA256: $((Get-FileHash (Join-Path $output 'Legacy.cs')).Hash)"
