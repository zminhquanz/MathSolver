param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference = 'Stop'

function Test-Elf([byte[]]$Bytes, [string]$Name) {
    if ($Bytes.Length -lt 64 -or [BitConverter]::ToUInt32($Bytes,0) -ne 0x464c457f -or $Bytes[4] -ne 2 -or $Bytes[5] -ne 1) {
        throw "Not a little-endian ELF64 library: $Name"
    }
    $offset = [BitConverter]::ToUInt64($Bytes,32)
    $size = [BitConverter]::ToUInt16($Bytes,54)
    $count = [BitConverter]::ToUInt16($Bytes,56)
    $loads = 0
    $writable = @()
    $relro = @()
    for ($index=0; $index -lt $count; $index++) {
        $position = [int]($offset + $index*$size)
        $type = [BitConverter]::ToUInt32($Bytes,$position)
        $flags = [BitConverter]::ToUInt32($Bytes,$position+4)
        $fileOffset = [BitConverter]::ToUInt64($Bytes,$position+8)
        $address = [BitConverter]::ToUInt64($Bytes,$position+16)
        $memorySize = [BitConverter]::ToUInt64($Bytes,$position+40)
        $alignment = [BitConverter]::ToUInt64($Bytes,$position+48)
        if ($type -eq 1) {
            $loads++
            if ($alignment -lt 16384 -or $fileOffset%16384 -ne $address%16384) {
                throw "16 KiB LOAD alignment failed: $Name (alignment=$alignment)"
            }
            if (($flags -band 2) -ne 0) { $writable += @{ Start=$address; End=$address+$memorySize } }
        }
        if ($type -eq 0x6474e552) { $relro += @{ Start=$address; End=$address+$memorySize } }
    }
    if ($loads -eq 0) { throw "No ELF LOAD segments: $Name" }
    # Android also permits RELRO in its own LOAD segment, with an unmapped gap
    # up to the next 16 KiB boundary (including MAUI's managed assembly DSOs).
    # Reject only actual writable memory outside RELRO that rounding protects.
    foreach ($region in $relro) {
        $pageStart = $region.Start - $region.Start%16384
        $pageEnd = $region.End + (16384-$region.End%16384)%16384
        foreach ($segment in $writable) {
            $start = [Math]::Max([long]$pageStart,[long]$segment.Start)
            $end = [Math]::Min([long]$pageEnd,[long]$segment.End)
            if ($start -lt $end -and ($start -lt $region.Start -or $end -gt $region.End)) {
                throw "16 KiB RELRO protects non-RELRO writable memory: $Name"
            }
        }
    }
    Write-Output "PASS 16 KiB ELF/RELRO: $Name"
}

$resolved = (Resolve-Path -LiteralPath $Path).Path
if ([IO.Path]::GetExtension($resolved) -eq '.apk') {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($resolved)
    try {
        $libraries = @($archive.Entries | Where-Object FullName -Match '^lib/.+\.so$')
        if (!$libraries.Count) { throw 'APK contains no native libraries.' }
        foreach ($entry in $libraries) {
            $stream = $entry.Open(); $memory = [IO.MemoryStream]::new()
            try { $stream.CopyTo($memory); Test-Elf $memory.ToArray() $entry.FullName }
            finally { $stream.Dispose(); $memory.Dispose() }
        }
    } finally { $archive.Dispose() }
} else {
    $libraries = @(Get-ChildItem -LiteralPath $resolved -Filter '*.so' -File -Recurse)
    if (!$libraries.Count) { throw 'Directory contains no native libraries.' }
    foreach ($library in $libraries) { Test-Elf ([IO.File]::ReadAllBytes($library.FullName)) $library.FullName }
}
