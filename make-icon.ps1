# Generates plink.ico (multi-size water-drop ripple icon) plus a PNG preview.
# Run with Windows PowerShell 5.1:
#   powershell.exe -ExecutionPolicy Bypass -File make-icon.ps1
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 48, 64, 128, 256

function New-RippleBitmap([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)

    $c = $S / 2.0
    $margin = [Math]::Max(0.5, $S * 0.01)
    $R = $S / 2.0 - $margin

    $drop  = [System.Drawing.Color]::FromArgb(255, 0, 120, 212)
    $ring1 = [System.Drawing.Color]::FromArgb(235, 0, 120, 212)
    $ring2 = [System.Drawing.Color]::FromArgb(135, 0, 120, 212)

    if ($S -ge 24) {
        $dropR = $R * 0.30
        $r1 = $R * 0.60; $w1 = $S * 0.085
        $r2 = $R * 0.95; $w2 = $S * 0.07
        $pen2 = New-Object System.Drawing.Pen($ring2, [float]$w2)
        $g.DrawEllipse($pen2, [float]($c - $r2), [float]($c - $r2), [float]($r2 * 2), [float]($r2 * 2))
        $pen2.Dispose()
        $pen1 = New-Object System.Drawing.Pen($ring1, [float]$w1)
        $g.DrawEllipse($pen1, [float]($c - $r1), [float]($c - $r1), [float]($r1 * 2), [float]($r1 * 2))
        $pen1.Dispose()
    } else {
        $dropR = $R * 0.33
        $r1 = $R * 0.66; $w1 = $S * 0.10
        $r2 = $R * 0.95; $w2 = $S * 0.09
        $pen2 = New-Object System.Drawing.Pen($ring2, [float]$w2)
        $g.DrawEllipse($pen2, [float]($c - $r2), [float]($c - $r2), [float]($r2 * 2), [float]($r2 * 2))
        $pen2.Dispose()
        $pen1 = New-Object System.Drawing.Pen($ring1, [float]$w1)
        $g.DrawEllipse($pen1, [float]($c - $r1), [float]($c - $r1), [float]($r1 * 2), [float]($r1 * 2))
        $pen1.Dispose()
    }

    $brush = New-Object System.Drawing.SolidBrush($drop)
    $g.FillEllipse($brush, [float]($c - $dropR), [float]($c - $dropR), [float]($dropR * 2), [float]($dropR * 2))
    $brush.Dispose()
    $g.Dispose()
    return $bmp
}

$pngs = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-RippleBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$pngs.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$icoPath = Join-Path $PSScriptRoot "plink.ico"
$fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = $sizes[$i]
    if ($dim -ge 256) { $dim = 0 }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
$bw.Close()
$fs.Close()

$preview = New-RippleBitmap 256
$preview.Save((Join-Path $PSScriptRoot "plink-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

Write-Host ("wrote " + $icoPath + " (" + (Get-Item $icoPath).Length + " bytes, " + $sizes.Count + " sizes)")
