# Draws the Ticket Timer icon (white stopwatch on a purple rounded square)
# and writes app.ico with PNG frames at the standard Windows sizes.
# Run from the repo root:  pwsh tools/make-icon.ps1
param([string]$Out = "$PSScriptRoot\..\app.ico", [string]$Preview = "")

Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256

function RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); $p
}

function Draw([int]$S) {
    $bmp = New-Object Drawing.Bitmap $S, $S
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
    $small = $S -le 24

    # Background: rounded square with a soft purple gradient
    $inset = [Math]::Max(0.5, $S * 0.03)
    $bgRect = New-Object Drawing.RectangleF $inset, $inset, ($S - 2 * $inset), ($S - 2 * $inset)
    $bg = RoundRect $bgRect.X $bgRect.Y $bgRect.Width $bgRect.Height ($S * 0.22)
    $grad = New-Object Drawing.Drawing2D.LinearGradientBrush $bgRect, ([Drawing.Color]::FromArgb(0x83, 0x6B, 0xC4)), ([Drawing.Color]::FromArgb(0x4F, 0x37, 0x8B)), 90
    $g.FillPath($grad, $bg)

    $white = [Drawing.Color]::White
    $cx = $S * 0.5; $cy = $S * 0.565; $r = $S * ($(if ($small) { 0.30 } else { 0.28 }))
    $stroke = $S * ($(if ($small) { 0.11 } else { 0.07 }))

    # Elapsed-time wedge (12 → 4 o'clock) in light lavender
    $wedge = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(0xEA, 0xDD, 0xFF))
    $g.FillPie($wedge, $cx - $r, $cy - $r, 2 * $r, 2 * $r, -90, 120)

    # Dial ring
    $pen = New-Object Drawing.Pen $white, $stroke
    $g.DrawEllipse($pen, $cx - $r, $cy - $r, 2 * $r, 2 * $r)

    # Crown (button on top) and stem
    $white_b = New-Object Drawing.SolidBrush $white
    $stemW = $S * ($(if ($small) { 0.12 } else { 0.08 }))
    $g.FillRectangle($white_b, $cx - $stemW / 2, $cy - $r - $S * 0.09, $stemW, $S * 0.09)
    $capW = $S * ($(if ($small) { 0.30 } else { 0.22 })); $capH = $S * ($(if ($small) { 0.09 } else { 0.07 }))
    $cap = RoundRect ($cx - $capW / 2) ($cy - $r - $S * 0.09 - $capH * 0.8) $capW $capH ($capH / 2)
    $g.FillPath($white_b, $cap)

    if (-not $small) {
        # Side button at about 1:30
        $g.TranslateTransform($cx, $cy); $g.RotateTransform(45)
        $side = RoundRect (-$S * 0.04) (-$r - $S * 0.085) ($S * 0.08) ($S * 0.07) ($S * 0.02)
        $g.FillPath($white_b, $side); $g.ResetTransform()
    }

    # Hand pointing to 4 o'clock (end of the wedge) and a center hub
    $hand = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(0x4F, 0x37, 0x8B)), ($S * ($(if ($small) { 0.10 } else { 0.065 })))
    $hand.StartCap = 'Round'; $hand.EndCap = 'Round'
    $len = $r * 0.68; $a = 30 * [Math]::PI / 180
    $g.DrawLine($hand, $cx, $cy, $cx + $len * [Math]::Cos($a), $cy + $len * [Math]::Sin($a))
    $hub = $S * ($(if ($small) { 0.13 } else { 0.09 }))
    $g.FillEllipse((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(0x4F, 0x37, 0x8B))), $cx - $hub / 2, $cy - $hub / 2, $hub, $hub)

    $g.Dispose()
    $bmp
}

# Build the .ico: header, directory, then one PNG per size
$frames = foreach ($s in $sizes) {
    $bmp = Draw $s
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Bytes = $ms.ToArray() }
}
$fs = [IO.File]::Create((Resolve-Path -LiteralPath (Split-Path $Out)).Path + "\" + (Split-Path $Out -Leaf))
$w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$f.Bytes.Length); $w.Write([uint32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write($f.Bytes) }
$w.Dispose()

# Optional preview strip of every size on light and dark backgrounds
if ($Preview) {
    $width = [int](($sizes | Measure-Object -Sum).Sum + 20 * ($sizes.Count + 1))
    $strip = New-Object Drawing.Bitmap $width, 600
    $g = [Drawing.Graphics]::FromImage($strip)
    $g.FillRectangle([Drawing.Brushes]::WhiteSmoke, 0, 0, $width, 300)
    $g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(32, 32, 32))), 0, 300, $width, 300)
    foreach ($row in 0, 300) {
        $x = 20
        foreach ($s in $sizes) { $b = Draw $s; $g.DrawImage($b, $x, $row + 280 - $s, $s, $s); $b.Dispose(); $x += $s + 20 }
    }
    $g.Dispose(); $strip.Save($Preview); $strip.Dispose()
}
"Wrote $Out"
