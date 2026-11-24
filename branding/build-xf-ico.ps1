Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot 'Monogramma XF in bianco fluido.png'
$icoPath = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\src\Flix')).Path 'flix-xf.ico'
$sizes = @(16, 24, 32, 48, 64, 128, 256)

$srcImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $sourcePath))
$src = New-Object System.Drawing.Bitmap $srcImage
$srcImage.Dispose()
if ($src.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) {
    throw "PixelFormat inatteso: $($src.PixelFormat)"
}

function Get-Pixels([System.Drawing.Bitmap] $bitmap) {
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $bytes = New-Object byte[] ($data.Stride * $bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        return @{ Bytes = $bytes; Stride = $data.Stride }
    } finally { $bitmap.UnlockBits($data) }
}

function Set-Pixels([System.Drawing.Bitmap] $bitmap, [byte[]] $bytes, [int] $stride) {
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bitmap.Width, $bitmap.Height
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try { [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $stride * $bitmap.Height) }
    finally { $bitmap.UnlockBits($data) }
}

$raw = Get-Pixels $src
$preBytes = New-Object byte[] ($raw.Bytes.Length)
for ($i = 0; $i -lt $raw.Bytes.Length; $i += 4) {
    $b = $raw.Bytes[$i]
    $g = $raw.Bytes[$i + 1]
    $r = $raw.Bytes[$i + 2]
    $a = $raw.Bytes[$i + 3]
    $preBytes[$i] = [byte](($b * $a) / 255)
    $preBytes[$i + 1] = [byte](($g * $a) / 255)
    $preBytes[$i + 2] = [byte](($r * $a) / 255)
    $preBytes[$i + 3] = $a
}
$pre = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
Set-Pixels $pre $preBytes $raw.Stride
$src.Dispose()

function Scale-Premultiplied([System.Drawing.Bitmap] $premultiplied, [int] $size) {
    $scaled = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($scaled)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($premultiplied, (New-Object System.Drawing.Rectangle 0, 0, $size, $size))
    $g.Dispose()
    $pixels = Get-Pixels $scaled
    $bytes = $pixels.Bytes
    $stride = $pixels.Stride
    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            $i = $y * $stride + $x * 4
            $a = $bytes[$i + 3]
            if ($a -eq 0) { $bytes[$i] = 0; $bytes[$i + 1] = 0; $bytes[$i + 2] = 0; continue }
            $bytes[$i] = [byte][Math]::Min(255, ([int]$bytes[$i] * 255 + ($a / 2)) / $a)
            $bytes[$i + 1] = [byte][Math]::Min(255, ([int]$bytes[$i + 1] * 255 + ($a / 2)) / $a)
            $bytes[$i + 2] = [byte][Math]::Min(255, ([int]$bytes[$i + 2] * 255 + ($a / 2)) / $a)
        }
    }
    Set-Pixels $scaled $bytes $stride
    return $scaled
}

$frames = @()
foreach ($size in $sizes) {
    $frame = Scale-Premultiplied $pre $size
    $png = New-Object System.IO.MemoryStream
    $frame.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{ Size = $size; Png = $png.ToArray(); Bitmap = $frame }
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dim = if ($frame.Size -ge 256) { [byte]0 } else { [byte]$frame.Size }
    $bw.Write($dim)
    $bw.Write($dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$frame.Png.Length)
    $bw.Write([uint32]$offset)
    $offset += $frame.Png.Length
}
foreach ($frame in $frames) { $bw.Write($frame.Png) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$bw.Dispose()

function Save-Plate([System.Drawing.Bitmap] $bitmap, [System.Drawing.Color] $back, [string] $path, [int] $scale) {
    $plate = New-Object System.Drawing.Bitmap ($bitmap.Width * $scale), ($bitmap.Height * $scale), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($plate)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear($back)
    $g.DrawImage($bitmap, 0, 0, $plate.Width, $plate.Height)
    $g.Dispose()
    $plate.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $plate.Dispose()
}

foreach ($frame in $frames) {
    if ($frame.Size -ne 16 -and $frame.Size -ne 24 -and $frame.Size -ne 32) { continue }
    $dark = Join-Path $PSScriptRoot ("preview-{0}-dark.png" -f $frame.Size)
    $light = Join-Path $PSScriptRoot ("preview-{0}-light.png" -f $frame.Size)
    Save-Plate $frame.Bitmap ([System.Drawing.Color]::FromArgb(255, 32, 33, 36)) $dark 8
    Save-Plate $frame.Bitmap ([System.Drawing.Color]::FromArgb(255, 243, 243, 243)) $light 8
    $pix = Get-Pixels $frame.Bitmap
    $opaque = 0; $clear = 0; $halo = 0; $white = 0
    for ($i = 0; $i -lt $pix.Bytes.Length; $i += 4) {
        $a = $pix.Bytes[$i + 3]
        $r = $pix.Bytes[$i + 2]
        if ($a -eq 0) { $clear++; continue }
        if ($a -gt 200 -and $r -gt 220) { $white++ }
        if ($a -gt 16 -and $r -lt 30) { $halo++ }
        if ($a -gt 200) { $opaque++ }
    }
    Write-Output ("size={0} clear={1} opaque={2} white={3} darkHalo={4}" -f $frame.Size, $clear, $opaque, $white, $halo)
    $frame.Bitmap.Dispose()
}
foreach ($frame in $frames) { if ($frame.Bitmap) { $frame.Bitmap.Dispose() } }
$pre.Dispose()

$loaded = New-Object System.Drawing.Icon $icoPath
$probe = New-Object System.Drawing.Icon $loaded, (New-Object System.Drawing.Size 16, 16)
$bmp = $probe.ToBitmap()
Write-Output ("drawing16={0}x{1} format={2} centerA={3}" -f $bmp.Width, $bmp.Height, $bmp.PixelFormat, $bmp.GetPixel(8, 8).A)
$bmp.Dispose(); $probe.Dispose(); $loaded.Dispose()
Write-Output "ICO=$icoPath bytes=$((Get-Item -LiteralPath $icoPath).Length)"
