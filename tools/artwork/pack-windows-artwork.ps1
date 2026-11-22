param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][string]$OutputFile,
    [string]$AndroidSvgRasterDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression

# Match the MAUI BaseSize rules using one high-resolution (3x) RGBA master.
# At normal 100-200% DPI, the artwork retains more pixels than its UI slot.
$files = @(Get-ChildItem -LiteralPath $SourceDirectory -File |
    Where-Object { $_.Extension -in @('.png', '.svg') } | Sort-Object Name)
if ($files.Count -eq 0) { throw 'No artwork images found.' }

$directory = [System.IO.Path]::GetDirectoryName($OutputFile)
[System.IO.Directory]::CreateDirectory($directory) | Out-Null
$temporary = $OutputFile + '.tmp'
$archiveStream = [System.IO.File]::Create($temporary)
try {
    $archive = [System.IO.Compression.ZipArchive]::new($archiveStream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in $files) {
            $maxWidth = 0
            $maxHeight = 0
            if ($file.Name.StartsWith('quiz_story_', [System.StringComparison]::OrdinalIgnoreCase)) {
                $maxWidth = 720; $maxHeight = 480
            } elseif ($file.Name -eq 'ai_guide_stationery.png') {
                $maxWidth = 576; $maxHeight = 384
            } elseif ($file.Name -in @('review_empty_tray.png', 'pdf_blank_sheet.png',
                'excel_template_sheet.png', 'excel_fill_sheet.png', 'excel_review_sheet.png',
                'excel_motion_sheet.png', 'excel_motion_pencil.png')) {
                $maxWidth = 540; $maxHeight = 360
            }

            $entry = $archive.CreateEntry($file.Name, [System.IO.Compression.CompressionLevel]::Optimal)
            $target = $entry.Open()
            try {
                if ($maxWidth -gt 0) {
                    $original = [System.Drawing.Image]::FromFile($file.FullName)
                    try {
                        $ratio = [Math]::Min(1.0, [Math]::Min($maxWidth / $original.Width, $maxHeight / $original.Height))
                        $width = [Math]::Max(1, [int][Math]::Round($original.Width * $ratio))
                        $height = [Math]::Max(1, [int][Math]::Round($original.Height * $ratio))
                        if ($ratio -lt 1.0) {
                            $resized = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                            try {
                                $drawing = [System.Drawing.Graphics]::FromImage($resized)
                                try {
                                    $drawing.Clear([System.Drawing.Color]::Transparent)
                                    $drawing.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                                    $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                                    $drawing.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                                    $drawing.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                                    $drawing.DrawImage($original, [System.Drawing.Rectangle]::new(0, 0, $width, $height),
                                        [System.Drawing.Rectangle]::new(0, 0, $original.Width, $original.Height),
                                        [System.Drawing.GraphicsUnit]::Pixel)
                                } finally { $drawing.Dispose() }
                                $resized.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
                            } finally { $resized.Dispose() }
                        } else {
                            $source = [System.IO.File]::OpenRead($file.FullName)
                            try { $source.CopyTo($target) } finally { $source.Dispose() }
                        }
                    } finally { $original.Dispose() }
                } else {
                    $source = [System.IO.File]::OpenRead($file.FullName)
                    try { $source.CopyTo($target) } finally { $source.Dispose() }
                }
            } finally { $target.Dispose() }
        }

        # On Android, ImageSource uses bitmap decoding. Store the original SVG
        # as well as its high-density PNG alias rendered by MAUI Resizetizer.
        if ($AndroidSvgRasterDirectory) {
            foreach ($file in @($files | Where-Object Extension -eq '.svg')) {
                $alias = $file.BaseName + '.png'
                if (@($files | Where-Object Name -eq $alias).Count -ne 0) {
                    throw "SVG alias conflicts with an original PNG: $alias"
                }
                $generated = Join-Path $AndroidSvgRasterDirectory $alias
                if (!(Test-Path -LiteralPath $generated -PathType Leaf)) {
                    throw "Missing MAUI Resizetizer output for $($file.Name): $generated"
                }
                $entry = $archive.CreateEntry($alias, [System.IO.Compression.CompressionLevel]::Optimal)
                $target = $entry.Open()
                try {
                    $source = [System.IO.File]::OpenRead($generated)
                    try { $source.CopyTo($target) } finally { $source.Dispose() }
                } finally { $target.Dispose() }
            }
        }
    } finally { $archive.Dispose() }
} finally { $archiveStream.Dispose() }

Move-Item -LiteralPath $temporary -Destination $OutputFile -Force
Write-Host "Packed $($files.Count) PNG/SVG files into $OutputFile ($([Math]::Round((Get-Item -LiteralPath $OutputFile).Length / 1MB, 1)) MiB)"
