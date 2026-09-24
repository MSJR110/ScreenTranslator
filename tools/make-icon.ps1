# Renders the app logo (warm coral→amber glass tile with a frosted lens around "ت") to Assets/logo.png and a
# multi-size Assets/app.ico. Re-run after changing the design; both files are committed so builds don't depend on it.
#   -Preview <dir>  renders a contact sheet of the variants (lens / bubble / glyph) at several sizes instead of writing assets.
#   -Variant <name> picks the design written to the assets (default: lens).
param(
    [string]$Preview = '',
    [ValidateSet('lens', 'bubble', 'glyph')] [string]$Variant = 'lens'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assets = Join-Path $root 'src\ScreenTranslator\Assets'
$fontFile = Join-Path $assets 'Fonts\Vazirmatn-Bold.ttf'

$fonts = New-Object System.Drawing.Text.PrivateFontCollection
$fonts.AddFontFile($fontFile)
$family = $fonts.Families[0]
$alef = [string][char]0x062A   # ت (code point so the script survives non-UTF8 editors)

function Col([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
function PtF([double]$x, [double]$y) { New-Object System.Drawing.PointF $x, $y }

function Squircle([double]$x, [double]$y, [double]$w, [double]$h, [double]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function RadialBrush([System.Drawing.Drawing2D.GraphicsPath]$path, [System.Drawing.Color]$center, [System.Drawing.Color]$edge, [double]$cx, [double]$cy) {
    $b = New-Object System.Drawing.Drawing2D.PathGradientBrush $path
    $b.CenterColor = $center
    $b.SurroundColors = [System.Drawing.Color[]]@($edge)
    $b.CenterPoint = (PtF $cx $cy)
    return $b
}

# The warm glass tile every variant sits on.
function DrawTile([System.Drawing.Graphics]$g, [int]$size) {
    $pad = [Math]::Max(1, [int]($size * 0.03))
    $side = $size - 2 * $pad
    $tile = Squircle $pad $pad $side $side ($side * 0.24)

    # coral → amber diagonal
    $base = New-Object System.Drawing.Drawing2D.LinearGradientBrush (PtF 0 0), (PtF $size $size), (Col 255 255 88 60), (Col 255 255 170 48)
    $g.FillPath($base, $tile)
    $g.SetClip($tile)

    # rose bloom top-right, amber bloom bottom-left: the "soft gradient" depth
    $bloom = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bloom.AddEllipse(($size * 0.35), (-$size * 0.35), ($size * 1.0), ($size * 1.0))
    $g.FillPath((RadialBrush $bloom (Col 150 255 92 150) (Col 0 255 92 150) ($size * 0.85) ($size * 0.15)), $bloom)
    $bloom2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bloom2.AddEllipse((-$size * 0.3), ($size * 0.45), ($size * 0.95), ($size * 0.95))
    $g.FillPath((RadialBrush $bloom2 (Col 120 255 214 120) (Col 0 255 214 120) ($size * 0.15) ($size * 0.9)), $bloom2)

    # glass sheen: bright top-left fading out by the middle
    $sheen = New-Object System.Drawing.Drawing2D.LinearGradientBrush (PtF 0 0), (PtF 0 $size), (Col 72 255 255 255), (Col 0 255 255 255)
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = [System.Drawing.Color[]]@((Col 72 255 255 255), (Col 0 255 255 255), (Col 0 255 255 255))
    $blend.Positions = [single[]]@(0.0, 0.62, 1.0)
    $sheen.InterpolationColors = $blend
    $g.FillPath($sheen, $tile)
    $g.ResetClip()

    # 1px inner rim so the tile reads as a glass slab on light backgrounds
    if ($size -ge 32) {
        $rimW = [Math]::Max(1.0, $size * 0.012)
        $rim = Squircle ($pad + $rimW / 2) ($pad + $rimW / 2) ($side - $rimW) ($side - $rimW) ($side * 0.24 - $rimW / 2)
        $g.DrawPath((New-Object System.Drawing.Pen (Col 110 255 255 255), $rimW), $rim)
    }
}

function DrawLetter([System.Drawing.Graphics]$g, [string]$text, [System.Drawing.Brush]$brush, [double]$px, [double]$cx, [double]$cy, [System.Drawing.FontFamily]$fam, [double]$shadow) {
    $font = New-Object System.Drawing.Font $fam, $px, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $box = New-Object System.Drawing.RectangleF ($cx - $px), ($cy - $px), ($px * 2), ($px * 2)
    if ($shadow -gt 0) {
        $sb = New-Object System.Drawing.SolidBrush (Col 70 90 20 0)
        $g.DrawString($text, $font, $sb, (New-Object System.Drawing.RectangleF ($cx - $px), ($cy - $px + $shadow), ($px * 2), ($px * 2)), $sf)
    }
    $g.DrawString($text, $font, $brush, $box, $sf)
}

function Render([int]$size, [string]$variant) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    DrawTile $g $size

    switch ($variant) {
        'lens' {
            # frosted lens slightly up-left, handle to the bottom-right; the letter sits inside the glass
            $cx = $size * 0.46; $cy = $size * 0.45; $r = $size * 0.29
            $ring = [Math]::Max(2.0, $size * 0.06)
            $handleW = [Math]::Max(2.0, $size * 0.095)

            $lens = New-Object System.Drawing.Drawing2D.GraphicsPath
            $lens.AddEllipse(($cx - $r), ($cy - $r), ($r * 2), ($r * 2))
            $g.FillPath((New-Object System.Drawing.SolidBrush (Col 66 255 255 255)), $lens)
            $frost = New-Object System.Drawing.Drawing2D.LinearGradientBrush (PtF ($cx - $r) ($cy - $r)), (PtF ($cx + $r) ($cy + $r)), (Col 70 255 255 255), (Col 0 255 255 255)
            $g.FillPath($frost, $lens)

            # handle first so the ring overlaps its root
            $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $handleW
            $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
            $ang = [Math]::PI / 4
            $g.DrawLine($pen, ($cx + [Math]::Cos($ang) * ($r + $ring * 0.2)), ($cy + [Math]::Sin($ang) * ($r + $ring * 0.2)),
                              ($cx + [Math]::Cos($ang) * ($r + $size * 0.19)), ($cy + [Math]::Sin($ang) * ($r + $size * 0.19)))
            $g.DrawEllipse((New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $ring), ($cx - $r), ($cy - $r), ($r * 2), ($r * 2))

            $px = if ($size -ge 32) { $size * 0.36 } else { $size * 0.42 }
            DrawLetter $g $alef ([System.Drawing.Brushes]::White) $px $cx ($cy - $size * 0.02) $family ($(if ($size -ge 48) { $size * 0.012 } else { 0 }))
        }
        'bubble' {
            # white speech bubble with the letter in coral; a faint Latin A behind it hints at "translate"
            if ($size -ge 32) {
                $latin = New-Object System.Drawing.FontFamily 'Segoe UI'
                DrawLetter $g 'A' (New-Object System.Drawing.SolidBrush (Col 120 255 255 255)) ($size * 0.36) ($size * 0.30) ($size * 0.32) $latin 0
            }
            $bx = $size * 0.30; $by = $size * 0.34; $bw = $size * 0.52; $bh = $size * 0.42
            $bubble = Squircle $bx $by $bw $bh ($bh * 0.34)
            $tail = New-Object System.Drawing.Drawing2D.GraphicsPath
            $tail.AddPolygon([System.Drawing.PointF[]]@((PtF ($bx + $bw * 0.78) ($by + $bh - 1)), (PtF ($bx + $bw * 0.86) ($by + $bh + $size * 0.10)), (PtF ($bx + $bw * 0.55) ($by + $bh - 1))))
            $g.FillPath([System.Drawing.Brushes]::White, $tail)
            $g.FillPath([System.Drawing.Brushes]::White, $bubble)
            DrawLetter $g $alef (New-Object System.Drawing.SolidBrush (Col 255 236 84 52)) ($bh * 0.66) ($bx + $bw / 2) ($by + $bh / 2 + $bh * 0.02) $family 0
        }
        'glyph' {
            $px = $size * 0.64
            DrawLetter $g $alef ([System.Drawing.Brushes]::White) $px ($size * 0.5) ($size * 0.47) $family ($(if ($size -ge 48) { $size * 0.015 } else { 0 }))
            if ($size -ge 32) {
                $sx = $size * 0.79; $sy = $size * 0.21; $sr = $size * 0.075
                $spark = New-Object System.Drawing.Drawing2D.GraphicsPath
                $spark.AddPolygon([System.Drawing.PointF[]]@(
                    (PtF $sx ($sy - $sr)), (PtF ($sx + $sr*0.28) ($sy - $sr*0.28)), (PtF ($sx + $sr) $sy), (PtF ($sx + $sr*0.28) ($sy + $sr*0.28)),
                    (PtF $sx ($sy + $sr)), (PtF ($sx - $sr*0.28) ($sy + $sr*0.28)), (PtF ($sx - $sr) $sy), (PtF ($sx - $sr*0.28) ($sy - $sr*0.28))))
                $g.FillPath((New-Object System.Drawing.SolidBrush (Col 235 255 255 255)), $spark)
            }
        }
    }

    $g.Dispose()
    return $bmp
}

if ($Preview) {
    New-Item -ItemType Directory -Force $Preview | Out-Null
    $variants = 'lens', 'bubble', 'glyph'
    $sheet = New-Object System.Drawing.Bitmap 1000, 420
    $sg = [System.Drawing.Graphics]::FromImage($sheet)
    $sg.InterpolationMode = 'NearestNeighbor'
    $sg.FillRectangle((New-Object System.Drawing.SolidBrush (Col 255 32 32 36)), 0, 0, 1000, 210)
    $sg.FillRectangle((New-Object System.Drawing.SolidBrush (Col 255 240 240 244)), 0, 210, 1000, 210)
    $x = 20
    foreach ($v in $variants) {
        foreach ($row in 0, 1) {
            $y = 20 + $row * 210
            $big = Render 160 $v;  $sg.DrawImage($big, $x, $y, 160, 160);       $big.Dispose()
            $m = Render 48 $v;     $sg.DrawImage($m, $x + 176, $y + 8, 48, 48);   $m.Dispose()
            $s = Render 32 $v;     $sg.DrawImage($s, $x + 176, $y + 68, 32, 32);  $s.Dispose()
            $t = Render 16 $v;     $sg.DrawImage($t, $x + 176, $y + 112, 16, 16); $t.Dispose()
            $t2 = Render 16 $v;    $sg.DrawImage($t2, $x + 176, $y + 140, 32, 32); $t2.Dispose()   # 16px shown 2x
        }
        $x += 330
    }
    $sg.Dispose()
    $sheet.Save((Join-Path $Preview 'icon-variants.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Wrote $Preview\icon-variants.png"
    return
}

# logo.png (256)
$logo = Render 256 $Variant
$logo.Save((Join-Path $assets 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()

# app.ico with PNG-compressed frames
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $b = Render $s $Variant
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    ,@{ Size = $s; Bytes = $ms.ToArray() }
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([Byte]$dim); $w.Write([Byte]$dim); $w.Write([Byte]0); $w.Write([Byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$f.Bytes.Length); $w.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write($f.Bytes) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'app.ico'), $out.ToArray())

Write-Host "Wrote $assets\logo.png and app.ico ($($frames.Count) sizes, variant=$Variant)"
