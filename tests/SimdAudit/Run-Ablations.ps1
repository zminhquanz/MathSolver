param(
    [int]$Exponent = 100000000,
    [int]$Workers = 24,
    [int]$Rounds = 1,
    [string[]]$Kernels = @('ForwardL1Generic','InverseL1Generic','Radix4','ForwardL2',
        'InverseL2StagePair','InverseL3Bridge','ForwardGlobalCached','ForwardGlobalUncached',
        'InverseGlobal','FinalInversePrefix','Pointwise','Crt'),
    [string]$Tag = 'ablation',
    [string]$Assembly = 'artifacts/simd-audit/bench/SimdAudit.dll'
)
$ErrorActionPreference = 'Stop'
$originalDisable = $env:SIMD_AUDIT_DISABLE
$originalMode = $env:SIMD_AUDIT_MODE
$originalAvx512 = $env:DOTNET_EnableAVX512
try {
    $env:SIMD_AUDIT_MODE = 'auto'
    $env:DOTNET_EnableAVX512 = '1'
    foreach ($round in 1..$Rounds) {
        $order = @('control')
        for ($i = 0; $i -lt $Kernels.Length; $i++) {
            $order += $Kernels[$i]
            if (($i + 1) % 3 -eq 0) { $order += "control-$i" }
        }
        $order += 'control-end'
        if ($round % 2 -eq 0) { [array]::Reverse($order) }
        foreach ($kernel in $order) {
            $env:SIMD_AUDIT_DISABLE = if ($kernel.StartsWith('control')) { '' } else {
                ($kernel.Split('+') | ForEach-Object { 'UseLargeModeAvx512' + $_ }) -join ','
            }
            $log = "artifacts/simd-audit/$Tag-$kernel-$Exponent-w$Workers-$round.jsonl"
            dotnet $Assembly bench 999999999999999999 $Exponent $Workers 1 > $log
            if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $log" }
            $data = Get-Content $log -Tail 1 | ConvertFrom-Json
            if ($Exponent -eq 100000000 -and $data.Hash -ne '339BFD8415AC8B78D28F34042C20AB957AFEC6F98E824AD647820B8EC7E45C76') {
                throw "Incorrect 100M output: $log"
            }
            Write-Output "$Tag disabled=$kernel exponent=$Exponent workers=$Workers round=$round seconds=$($data.Seconds)"
        }
    }
} finally {
    $env:SIMD_AUDIT_DISABLE = $originalDisable
    $env:SIMD_AUDIT_MODE = $originalMode
    $env:DOTNET_EnableAVX512 = $originalAvx512
}
