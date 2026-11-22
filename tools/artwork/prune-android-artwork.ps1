param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][string]$ResizetizerDirectory
)

$ErrorActionPreference = 'Stop'

# Native Shell and Android PopupMenu obtain drawables through resource IDs.
$native = @('calculate', 'quiz', 'function', 'grid_on',
    'settings', 'database', 'benchmark', 'info', 'reset_settings')
$nativeSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($name in $native) { [void]$nativeSet.Add($name) }

$artwork = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -LiteralPath $SourceDirectory -File |
    Where-Object Extension -in @('.svg', '.png') |
    ForEach-Object { if (!$nativeSet.Contains($_.BaseName)) { [void]$artwork.Add($_.BaseName) } }

$removed = 0
Get-ChildItem -LiteralPath $ResizetizerDirectory -Directory -Filter 'drawable-*' |
    ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -File -Filter '*.png' |
            ForEach-Object {
                if ($artwork.Contains($_.BaseName)) {
                    Remove-Item -LiteralPath $_.FullName -Force
                    $removed++
                }
            }
    }
Write-Host "Pruned $removed redundant Android drawable PNG variants; native Shell/menu icons retained."
