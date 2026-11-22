param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (-not [System.IO.Directory]::Exists($SourceDirectory)) { throw 'Artwork source directory is missing.' }
if (-not [System.IO.Directory]::Exists($OutputDirectory)) { throw 'Build output directory is missing.' }

$source = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$output = [System.IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if ([string]::Equals($source, $output, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to prune the artwork source directory.'
}

# Only remove copies of assets that are now inside mathsolver-artwork.pack.
# WinUI app icon and splash assets are not source MauiImage files and are kept.
$shellIcons = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$shellPath = [System.IO.Path]::GetFullPath((Join-Path $source '../../AppShell.xaml'))
if (Test-Path -LiteralPath $shellPath) {
    [xml]$shell = Get-Content -LiteralPath $shellPath -Raw -Encoding UTF8
    foreach ($node in $shell.SelectNodes('//*[@Icon]')) {
        [void]$shellIcons.Add([System.IO.Path]::GetFileNameWithoutExtension($node.Icon))
    }
}
# These PNGs are generated from SVGs for native Shell navigation. Unlike regular
# Image controls, Shell cannot resolve their URI through mathsolver-artwork.pack.
$bases = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -LiteralPath $SourceDirectory -File |
    Where-Object { $_.Extension -in @('.png', '.svg') } |
    ForEach-Object { if (!$shellIcons.Contains($_.BaseName)) { [void]$bases.Add($_.BaseName) } }

$removed = 0
foreach ($file in Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.png' -File) {
    if ($file.Name -match '^(?<base>.+?)(?:\.scale-\d+)?\.png$' -and $bases.Contains($Matches['base'])) {
        Remove-Item -LiteralPath $file.FullName -Force
        $removed++
    }
}
Write-Host "Pruned $removed legacy loose artwork PNGs from $OutputDirectory"
