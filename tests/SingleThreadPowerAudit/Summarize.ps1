param([Parameter(Mandatory=$true)][string[]]$Paths)
$ErrorActionPreference = 'Stop'
$rows = foreach ($path in $Paths) {
    foreach ($line in [IO.File]::ReadLines((Resolve-Path $path))) {
        if ($line.StartsWith('{')) { $line | ConvertFrom-Json }
    }
}
function Median($values) {
    $sorted = @($values | Sort-Object)
    $n = $sorted.Count
    if ($n % 2) { $sorted[[int][Math]::Floor($n / 2)] }
    else { ($sorted[$n / 2 - 1] + $sorted[$n / 2]) / 2 }
}
$rows | Where-Object Kind -eq 'power' | Group-Object Base,Exponent,Mode | ForEach-Object {
    [pscustomobject]@{ Group=$_.Name; N=$_.Count; MedianMs=[Math]::Round((Median $_.Group.Milliseconds),4);
        MinMs=($_.Group.Milliseconds | Measure-Object -Minimum).Minimum;
        MaxMs=($_.Group.Milliseconds | Measure-Object -Maximum).Maximum;
        MedianBytes=Median $_.Group.AllocatedBytes }
} | Format-Table -AutoSize
$rows | Where-Object Kind -eq 'kernel' | Group-Object Limbs,Mode | ForEach-Object {
    [pscustomobject]@{ Group=$_.Name; MedianNs=[Math]::Round((Median $_.Group.Nanoseconds),2); Bytes=Median $_.Group.BytesPerOp }
} | Format-Table -AutoSize
