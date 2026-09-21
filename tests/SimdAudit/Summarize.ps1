param([string]$Directory = 'artifacts/simd-audit')
$ErrorActionPreference = 'Stop'
$rows = foreach ($file in Get-ChildItem $Directory -Filter '*.jsonl' -File) {
    foreach ($line in Get-Content $file.FullName) {
        if (-not $line.StartsWith('{')) { continue }
        $data = $line | ConvertFrom-Json
        if ($data.Kind -ne 'power') { continue }
        $row = [ordered]@{ Sample = $file.BaseName; Exponent = $data.Exponent; Workers = $data.Workers;
            Seconds = $data.Seconds; Hash = $data.Hash; NttCount = $data.Diagnostics.NttMultiplicationCount;
            SegmentPairs = $data.Diagnostics.SegmentedNttPairCount }
        foreach ($name in @('ForwardLocalL1','ForwardLocalL2','ForwardLocalL3','ForwardGlobalCached',
            'ForwardGlobalUncached','ForwardGlobalTwiddlePreparation','ForwardLocalTwiddlePreparation',
            'InverseLocalL1','InverseL1Packed816','InverseL1GenericStagePair','InverseL1Radix4Tail',
            'InverseLocalL2','InverseLocalL3','InverseGlobalCached','InverseGlobalUncached',
            'InverseFinalPrefix','Pointwise','Crt','Carry')) {
            $row[$name] = [timespan]::Parse($data.Diagnostics.$name).TotalSeconds
        }
        [pscustomobject]$row
    }
}
$rows | Export-Csv "$Directory/results.csv" -NoTypeInformation -Encoding UTF8
$rows | Select-Object Sample,Seconds | Format-Table -AutoSize
